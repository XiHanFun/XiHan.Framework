// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
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
/// 命名连接（RFC-0005「命名连接」，ADR-0084）。
/// </summary>
/// <remarks>
/// 方式一在 <c>XiHan:Persistence:SqlSugar:Connections</c> 声明的连接进每个作用域的客户端、回调按 <c>ConfigId</c> 区分；登记到命名连接的实体在那个库建表与读写；绑定连接的工作单元只在它上面开事务——SQLite 上业务事务占着默认库的写锁时照样能提交；落点错误与配置错误在启动或开始时报出。
/// </remarks>
public sealed class SqlSugarNamedConnectionTests : IDisposable
{
    /// <summary>
    /// 命名连接的名字。
    /// </summary>
    private const string Leases = "leases";

    /// <summary>
    /// 应用默认库。
    /// </summary>
    private readonly SqliteDatabase _default = new();

    /// <summary>
    /// 命名连接 <see cref="Leases"/> 的库。
    /// </summary>
    private readonly SqliteDatabase _named = new();

    /// <summary>
    /// 删除两个临时数据库文件。
    /// </summary>
    public void Dispose()
    {
        _default.Dispose();
        _named.Dispose();
    }

    [Fact]
    public async Task ConfiguredConnection_HoldsTheRegisteredEntity_AndTheCallbackSeesItsConfigId()
    {
        var seen = new List<(string ConfigId, string? Value, string? Provider)>();
        await using var host = await StartHostAsync(seen);
        var orderId = Guid.CreateVersion7();

        (await host.InvokeAsync(async ctx =>
        {
            (await ctx.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>().AddAsync(new ConformanceOrder { Id = orderId, Name = "order" }, ctx.CancellationToken)).ThrowIfFailure();
            (await ctx.Services.GetRequiredService<IRepository<ConformanceItem, int>>().AddAsync(new ConformanceItem { Id = 7, Label = "lease" }, ctx.CancellationToken)).ThrowIfFailure();
        })).ThrowIfFailure();

        // 回调对默认连接与命名连接各调用一次：命名连接的绑定主值是它自己的连接串，属性沿用 db 绑定
        Assert.Equal([(SqlSugarConnections.Default, Busy(_default), "sqlite"), (Leases, Busy(_named), "sqlite")], seen);
        Assert.Equal(Leases, host.Services.GetRequiredService<RepositoryRegistrations>().ConnectionOf(typeof(ConformanceItem)));
        using var defaultClient = CreateClient(_default.ConnectionString);
        using var namedClient = CreateClient(_named.ConnectionString);
        // 建表按落点分组：登记到命名连接的实体只在那个库里有表
        Assert.Equal([orderId], defaultClient.Queryable<ConformanceOrder>().ToList().Select(static o => o.Id));
        Assert.False(defaultClient.DbMaintenance.IsAnyTable(defaultClient.EntityMaintenance.GetTableName<ConformanceItem>(), false));
        Assert.Equal([7], namedClient.Queryable<ConformanceItem>().ToList().Select(static i => i.Id));
        Assert.False(namedClient.DbMaintenance.IsAnyTable(namedClient.EntityMaintenance.GetTableName<ConformanceOrder>(), false));
    }

    [Fact]
    public async Task BoundUnitOfWork_Commits_WhileABusinessTransactionHoldsTheDefaultDatabase()
    {
        await using var host = await StartHostAsync();
        var orderId = Guid.CreateVersion7();

        (await host.InvokeAsync(async ctx =>
        {
            // 业务事务写过默认库：SQLite 的库级写锁在它手里，直到调用结束
            (await ctx.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>().AddAsync(new ConformanceOrder { Id = orderId, Name = "order" }, ctx.CancellationToken)).ThrowIfFailure();

            // 新作用域里绑定命名连接的工作单元：事务只开在命名库上，不去等默认库的写锁（连接串忙等 2 秒，等了就会失败）
            await using var scope = host.Services.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions(Connection: Leases), ctx.CancellationToken);
            var items = scope.ServiceProvider.GetRequiredService<IRepository<ConformanceItem, int>>();
            (await items.AddAsync(new ConformanceItem { Id = 1, Label = "lease" }, ctx.CancellationToken)).ThrowIfFailure();
            (await unitOfWork.CommitAsync(ctx.CancellationToken)).ThrowIfFailure();
        })).ThrowIfFailure();

        using var namedClient = CreateClient(_named.ConnectionString);
        using var defaultClient = CreateClient(_default.ConnectionString);
        Assert.Equal([1], namedClient.Queryable<ConformanceItem>().ToList().Select(static i => i.Id));
        Assert.Equal([orderId], defaultClient.Queryable<ConformanceOrder>().ToList().Select(static o => o.Id));
    }

    [Fact]
    public async Task BoundUnitOfWork_RejectsAnEntityPlacedElsewhere_AndAMissingConnection()
    {
        await using var host = await StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<SqlSugarUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true, Connection: Leases), CancellationToken.None);

        // 只认绑定的连接：落在默认连接的实体按落点错误拒绝，不去读不在这个工作单元里的库
        Assert.Equal(Leases, unitOfWork.ConnectionFor(typeof(ConformanceItem)).CurrentConnectionConfig.ConfigId);
        var placed = Assert.Throws<FrameworkException>(() => unitOfWork.ConnectionFor(typeof(ConformanceOrder)));
        Assert.Equal(PersistenceErrors.UnknownConnection, placed.Code);
        Assert.False((await scope.ServiceProvider.GetRequiredService<IRepository<ConformanceItem, int>>().FindAsync(1, CancellationToken.None)).HasValue);
        await unitOfWork.RollbackAsync(CancellationToken.None);

        // 绑定的连接不在客户端里：开始时就是装配错误
        await using var other = host.Services.CreateAsyncScope();
        var missing = await Assert.ThrowsAsync<FrameworkException>(() => other.ServiceProvider.GetRequiredService<IUnitOfWork>().BeginAsync(new UnitOfWorkOptions(Connection: "archive"), CancellationToken.None));
        Assert.Equal(PersistenceErrors.UnknownConnection, missing.Code);
        Assert.Throws<ArgumentException>(static () => new UnitOfWorkOptions(Connection: " "));
    }

    [Fact]
    public async Task Startup_RejectsARegisteredConnectionThatDoesNotExist_OrDisagreesWithTenantAttribute()
    {
        // 客户端里只有 default：登记到 leases 的实体在启动时就报装配错误
        var missing = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(
            static builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
            [new("XiHan:Bindings:db", _default.ConnectionString)],
            static services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddRepository<ConformanceItem, int>(Leases);
            }));
        Assert.Contains(PersistenceErrors.UnknownConnection, Codes(missing));
        Assert.Contains(Leases, missing.ToString(), StringComparison.Ordinal);

        // [Tenant("audit")] 与登记的 leases 各说各的：不静默取其一
        var conflict = await Assert.ThrowsAsync<FrameworkException>(() => TestHost.StartAsync(
            static builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
            [new("XiHan:Bindings:db", _default.ConnectionString), new($"{SqlSugarPersistenceOptions.ConfigurationSection}:Connections:{Leases}", _named.ConnectionString)],
            static services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddRepository<PinnedAuditRecord, Guid>(Leases);
            }));
        Assert.Contains(PersistenceErrors.UnknownConnection, Codes(conflict));
        Assert.Contains("[Tenant]", conflict.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Leases", "Data Source=x.db")]
    [InlineData("default", "Data Source=x.db")]
    [InlineData("shard", "Data Source=x.db")]
    [InlineData("-x", "Data Source=x.db")]
    [InlineData("leases", " ")]
    public void Options_RejectInvalidConnectionNamesAndBlankValues(string name, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new($"{SqlSugarPersistenceOptions.ConfigurationSection}:Connections:{name}", value)]).Build();

        var error = Assert.Throws<ArgumentException>(() => SqlSugarPersistenceOptions.FromConfiguration(configuration));

        Assert.Contains($"Connections:{name}", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("x.db", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Startup_RejectsConfiguredConnections_WhenTheApplicationRegistersItsOwnClient()
    {
        // 方式二由应用自己组装连接：这一节对它没有作用，不静默忽略
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => TestHost.StartAsync(
            static builder => builder.UseFeature(new SqlSugarPersistenceFeature()),
            [new($"{SqlSugarPersistenceOptions.ConfigurationSection}:Connections:{Leases}", _named.ConnectionString)],
            services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddScoped<ISqlSugarClient>(_ => CreateClient(_default.ConnectionString));
            }));

        Assert.Contains(Chain(exception), static e => e is ArgumentException && e.Message.Contains("Connections", StringComparison.Ordinal));
    }

    /// <summary>
    /// 启动方式一的宿主，带命名连接 <see cref="Leases"/>。
    /// </summary>
    /// <remarks>
    /// 订单落默认连接，条目登记到命名连接，开启建表。
    /// 两个库的连接串都把忙等上限设为 2 秒：误把两个写者放进同一个库时尽快失败。<paramref name="seen"/> 不为空时记下回调看到的 <c>ConfigId</c>、绑定主值与 <c>provider</c> 属性。
    /// </remarks>
    private Task<TestHost> StartHostAsync(List<(string ConfigId, string? Value, string? Provider)>? seen = null) => TestHost.StartAsync(
        builder => builder.UseFeature(new SqlSugarPersistenceFeature((binding, config) =>
        {
            var value = binding.ValueOrThrow();
            seen?.Add((config.ConfigId.ToString()!, value.Value, value.Properties.GetValueOrDefault("provider")));
            config.DbType = DbType.Sqlite;
            config.ConnectionString = value.Value;
        })),
        [
            new("XiHan:Bindings:db:value", Busy(_default)),
            new("XiHan:Bindings:db:provider", "sqlite"),
            new($"{SqlSugarPersistenceOptions.ConfigurationSection}:Connections:{Leases}", Busy(_named)),
            new("XiHan:Persistence:SqlSugar:InitTables", "true"),
        ],
        static services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddRepository<ConformanceItem, int>(Leases);
        });

    /// <summary>
    /// 带 2 秒忙等上限的连接串。
    /// </summary>
    private static string Busy(SqliteDatabase database) => database.ConnectionString + ";Default Timeout=2";

    /// <summary>
    /// 创建指向给定连接串、已施加约定的独立客户端。
    /// </summary>
    private static SqlSugarClient CreateClient(string connectionString)
        => new(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = connectionString, IsAutoCloseConnection = true }));

    /// <summary>
    /// 异常链上的全部框架错误码。
    /// </summary>
    private static List<string> Codes(Exception exception)
        => [.. Chain(exception).OfType<FrameworkException>().Select(static framework => framework.Code)];

    /// <summary>
    /// 异常与它的内层异常。
    /// </summary>
    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception;
        }
    }
}
