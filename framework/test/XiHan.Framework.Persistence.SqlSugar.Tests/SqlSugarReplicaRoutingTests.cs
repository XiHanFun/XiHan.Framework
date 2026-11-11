// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 读写分离的路由（RFC-0005「读写分离」、ADR-0118）
/// </summary>
/// <remarks>
/// 只读工作单元读从库；会写的工作单元读写都走主库，写入前的判重、写入后的读取都看得到真实数据；一个作用域读从库不影响别的作用域的写入
/// </remarks>
public sealed class SqlSugarReplicaRoutingTests : IDisposable
{
    /// <summary>
    /// 一主两从
    /// </summary>
    private readonly ReplicatedSqlite _site = new();

    /// <inheritdoc />
    public void Dispose() => _site.Dispose();

    [Fact]
    public async Task ReadOnReplica_DoesNotRedirectAnotherScopesWrite()
    {
        // SqlSugar 读从库时把连接配置的 ConnectionString 改成从库的、读完再改回来。配置在作用域之间共用的话，这期间别的作用域建连接就连到了从库
        Action? duringRead = null;
        var feature = ReplicatedSqlite.CreateFeature([(_site.ReplicaA.ConnectionString, 10)], config => config.AopEvents = new AopEvents
        {
            OnLogExecuting = (sql, _) =>
            {
                if (sql.Contains("from marker", StringComparison.Ordinal))
                {
                    var action = duringRead;
                    duringRead = null;
                    action?.Invoke();
                }
            },
        });
        await using var host = await TestHost.StartAsync(builder => builder.UseFeature(feature), _site.Configuration);
        duringRead = () =>
        {
            using var other = host.Services.CreateScope();
            other.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.ExecuteCommand("insert into marker(name) values ('written')");
        };

        await using var scope = host.Services.CreateAsyncScope();
        var seen = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Ado.GetString(ReplicatedSqlite.ReadMarker);

        Assert.Equal("replica-a", seen);
        Assert.Equal(["primary", "written"], ReplicatedSqlite.Markers(_site.Primary));
        Assert.Equal(["replica-a"], ReplicatedSqlite.Markers(_site.ReplicaA));
    }

    [Fact]
    public async Task ReadOnlyUnitOfWork_ReadsTheReplica()
    {
        await using var host = await StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true }, CancellationToken.None);

        Assert.Equal("replica-a", unitOfWork.ConnectionFor(typeof(ConformanceOrder)).Ado.GetString(ReplicatedSqlite.ReadMarker));
        Assert.Equal("replica-a", unitOfWork.Client.Ado.GetString(ReplicatedSqlite.ReadMarker));
    }

    [Fact]
    public async Task ReadsPrimary_ReadsThePrimary_AndStaysReadOnly()
    {
        // 授予与租户目录决定放不放行，读到落后的从库等于撤销还没生效；只改读哪里，只读的其余语义不变
        await using var host = await StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true, ReadsPrimary = true }, CancellationToken.None);

        Assert.Equal("primary", unitOfWork.ConnectionFor(typeof(ConformanceOrder)).Ado.GetString(ReplicatedSqlite.ReadMarker));
        Assert.Equal("primary", unitOfWork.Client.Ado.GetString(ReplicatedSqlite.ReadMarker));
        Assert.True(unitOfWork.IsReadOnly);
        Assert.False(unitOfWork.HasTransaction);
        var refused = await Assert.ThrowsAsync<FrameworkException>(async () => await unitOfWork.EnsureTransactionAsync(CancellationToken.None));
        Assert.Equal(PersistenceErrors.ReadOnly, refused.ToError().Code);
        Assert.False(unitOfWork.HasTransaction);
    }

    [Fact]
    public async Task ReadsPrimary_DoesNotLeakIntoTheNextScope()
    {
        // 钉主库的标记留在这个作用域的客户端上：下一个只读作用域照常读从库
        await using var host = await StartAsync();
        await using (var pinned = host.Services.CreateAsyncScope())
        {
            var unitOfWork = pinned.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true, ReadsPrimary = true }, CancellationToken.None);
            Assert.Equal("primary", unitOfWork.Client.Ado.GetString(ReplicatedSqlite.ReadMarker));
        }

        await using var scope = host.Services.CreateAsyncScope();
        var next = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        await next.BeginAsync(new UnitOfWorkOptions { IsReadOnly = true }, CancellationToken.None);
        Assert.Equal("replica-a", next.Client.Ado.GetString(ReplicatedSqlite.ReadMarker));
    }

    [Fact]
    public async Task WritingUnitOfWork_ReadsThePrimary_EvenBeforeItsFirstWrite()
    {
        // 事务在第一次写入时才开，SqlSugar 只按事务判断。写入前的读（判重、并发戳）读到落后的从库就会做错决定
        await using var host = await StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(), CancellationToken.None);

        Assert.False(unitOfWork.HasTransaction);
        Assert.Equal("primary", unitOfWork.ConnectionFor(typeof(ConformanceOrder)).Ado.GetString(ReplicatedSqlite.ReadMarker));
        Assert.Equal("primary", unitOfWork.Client.Ado.GetString(ReplicatedSqlite.ReadMarker));
    }

    [Fact]
    public async Task WritingUnitOfWork_ReadsItsOwnWrite_InsideTheTransaction_AndAfterCommit()
    {
        await using var host = await StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        var orders = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(), CancellationToken.None);
        var id = Guid.CreateVersion7();

        (await orders.AddAsync(new ConformanceOrder { Id = id, Name = "mine" }, CancellationToken.None)).ThrowIfFailure();

        Assert.True(unitOfWork.HasTransaction);
        Assert.Equal("mine", (await orders.FindAsync(id, CancellationToken.None)).ValueOrThrow().Name);
        Assert.Equal("primary", unitOfWork.Client.Ado.GetString(ReplicatedSqlite.ReadMarker));

        (await unitOfWork.CommitAsync(CancellationToken.None)).ThrowIfFailure();

        // 提交之后事务没了，同一作用域的读仍然看得到刚写的：从库可能还没复制到
        Assert.False(unitOfWork.HasTransaction);
        Assert.Equal("primary", unitOfWork.Client.Ado.GetString(ReplicatedSqlite.ReadMarker));
    }

    [Fact]
    public async Task ConflictCheck_ReadsThePrimary_ThroughItsFreshConnection()
    {
        // 主键冲突后事务已中止，仓储另开一个连接查是谁撞了：这个连接也要读主库，从库上还没有那一行时会判断错
        var blind = new BlindedIdCheck();
        await using var host = await TestHost.StartAsync(
            builder => builder.UseFeature(ReplicatedSqlite.CreateFeature([(_site.ReplicaA.ConnectionString, 10)], config => config.AopEvents = new AopEvents { OnExecutingChangeSql = blind.Rewrite })),
            [.. _site.Configuration, new("XiHan:Persistence:SqlSugar:InitTables", "true")],
            static services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddRepository<ConformanceOrder, Guid>();
            });
        var id = Guid.CreateVersion7();
        await using (var first = host.Services.CreateAsyncScope())
        {
            var unitOfWork = first.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions(), CancellationToken.None);
            (await first.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>().AddAsync(new ConformanceOrder { Id = id, Name = "first" }, CancellationToken.None)).ThrowIfFailure();
            (await unitOfWork.CommitAsync(CancellationToken.None)).ThrowIfFailure();
        }

        blind.Arm(id);
        await using var second = host.Services.CreateAsyncScope();
        var secondUnitOfWork = second.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        await secondUnitOfWork.BeginAsync(new UnitOfWorkOptions(), CancellationToken.None);

        var added = await second.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>().AddAsync(new ConformanceOrder { Id = id, Name = "mine" }, CancellationToken.None);

        Assert.True(blind.Fired);
        Assert.Equal(PersistenceErrors.Duplicate, added.Error?.Code);
    }

    [Fact]
    public async Task InitTables_CreatesTablesOnThePrimary_AndFindsThemThereOnTheNextStart()
    {
        // 建表前要查表在不在：查到从库上会以为还没建，第二次启动就重复建表
        IEnumerable<KeyValuePair<string, string?>> configuration = [.. _site.Configuration, new("XiHan:Persistence:SqlSugar:InitTables", "true")];
        for (var start = 0; start < 2; start++)
        {
            await using var host = await TestHost.StartAsync(
                builder => builder.UseFeature(ReplicatedSqlite.CreateFeature([(_site.ReplicaA.ConnectionString, 10)])),
                configuration,
                static services =>
                {
                    services.AddSingleton(TimeProvider.System);
                    services.AddRepository<ConformanceOrder, Guid>();
                });
        }

        using var replica = new SqlSugarClient(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = _site.ReplicaA.ConnectionString, IsAutoCloseConnection = true });
        Assert.False(replica.DbMaintenance.IsAnyTable(nameof(ConformanceOrder), isCache: false));
    }

    [Fact]
    public async Task WriteOutsideAnyUnitOfWork_GoesToThePrimary_AndAPlainReadToTheReplica()
    {
        await using var host = await StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        client.Ado.ExecuteCommand("insert into marker(name) values ('written')");

        Assert.Equal("replica-a", client.Ado.GetString(ReplicatedSqlite.ReadMarker));
        Assert.Equal(["primary", "written"], ReplicatedSqlite.Markers(_site.Primary));
    }

    [Fact]
    public async Task ExplicitTransaction_ReadsThePrimary()
    {
        await using var host = await StartAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();

        client.Ado.BeginTran();
        var seen = client.Ado.GetString(ReplicatedSqlite.ReadMarker);
        client.Ado.RollbackTran();

        Assert.Equal("primary", seen);
    }

    /// <summary>
    /// 启动配了一个从库的宿主
    /// </summary>
    /// <remarks>
    /// 建表只开在主库上
    /// </remarks>
    private Task<TestHost> StartAsync() => TestHost.StartAsync(
        builder => builder.UseFeature(ReplicatedSqlite.CreateFeature([(_site.ReplicaA.ConnectionString, 10)])),
        [.. _site.Configuration, new("XiHan:Persistence:SqlSugar:InitTables", "true")],
        static services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddRepository<ConformanceOrder, Guid>();
        });
}
