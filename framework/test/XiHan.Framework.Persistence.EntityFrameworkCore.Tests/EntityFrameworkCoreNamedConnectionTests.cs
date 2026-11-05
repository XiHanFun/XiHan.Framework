// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Abstractions.UnitOfWork;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// EF Core 的命名连接（RFC-0005「命名连接」，ADR-0087）
/// </summary>
/// <remarks>
/// 两个 SQLite 文件：订单落默认库，条目登记到命名连接 <see cref="Leases"/>。绑定命名连接的工作单元所在作用域的 DbContext 按那个连接建出，配置回调收到它的绑定；业务事务占着默认库写锁时，绑定连接的工作单元照样提交；落点不一致、连接没声明、DbContext 已建出后再绑定都明确拒绝
/// </remarks>
public sealed class EntityFrameworkCoreNamedConnectionTests : IDisposable
{
    /// <summary>
    /// 命名连接的名字
    /// </summary>
    private const string Leases = "leases";

    /// <summary>
    /// 应用默认库
    /// </summary>
    private readonly SqliteDatabase _default = new();

    /// <summary>
    /// 命名连接 <see cref="Leases"/> 的库
    /// </summary>
    private readonly SqliteDatabase _named = new();

    /// <summary>
    /// 清空宿主所用连接串的连接池，再删除两个临时数据库文件
    /// </summary>
    public void Dispose()
    {
        // 宿主的连接串带忙等时限，是另外的连接池分组
        SqliteDatabase.ClearPool(Busy(_default));
        SqliteDatabase.ClearPool(Busy(_named));
        _default.Dispose();
        _named.Dispose();
    }

    [Fact]
    public async Task BoundUnitOfWork_BuildsItsDbContextOnTheNamedConnection_AndTheCallbackSeesItsBinding()
    {
        var seen = new ConcurrentQueue<(string? Value, string? Provider)>();
        await using var host = await StartHostAsync(seen);
        var orderId = Guid.CreateVersion7();

        (await host.InvokeAsync(async ctx => (await ctx.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>().AddAsync(new ConformanceOrder { Id = orderId, Name = "order" }, ctx.CancellationToken)).ThrowIfFailure())).ThrowIfFailure();
        await InLeasesAsync(host, static async (items, ct) => (await items.AddAsync(new ConformanceItem { Id = 7, Label = "lease" }, ct)).ThrowIfFailure());

        // 回调按作用域建 DbContext：默认作用域收到 db 绑定，绑定命名连接的作用域收到主值换成它的连接串、属性沿用 db 绑定的绑定
        Assert.Contains((Busy(_default), "sqlite"), seen);
        Assert.Contains((Busy(_named), "sqlite"), seen);
        // 行落在各自的库里；EnsureCreated 在命名库上按整个模型建表，两边都有两张表但各自只有自己的行
        Assert.Equal((1L, 0L), (Count(_default, "ConformanceOrder"), Count(_default, "ConformanceItem")));
        Assert.Equal((0L, 1L), (Count(_named, "ConformanceOrder"), Count(_named, "ConformanceItem")));
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

            // 新作用域里绑定命名连接的工作单元：DbContext 建在命名库上、事务只开在那里，不去等默认库的写锁（忙等 2 秒，等了就会失败）
            await InLeasesAsync(host, static async (items, ct) => (await items.AddAsync(new ConformanceItem { Id = 1, Label = "lease" }, ct)).ThrowIfFailure());
        })).ThrowIfFailure();

        Assert.Equal(1L, Count(_named, "ConformanceItem"));
        Assert.Equal(1L, Count(_default, "ConformanceOrder"));
    }

    [Fact]
    public async Task Placement_MustMatchTheUnitOfWorksConnection()
    {
        await using var host = await StartHostAsync();

        // 绑定命名连接的工作单元只认这个连接：落在默认连接的实体按落点错误拒绝，不去读不在这个工作单元里的库
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(new UnitOfWorkOptions(IsReadOnly: true, Connection: Leases), CancellationToken.None);
            Assert.False((await scope.ServiceProvider.GetRequiredService<IRepository<ConformanceItem, int>>().FindAsync(1, CancellationToken.None)).HasValue);
            var placed = await Assert.ThrowsAsync<FrameworkException>(async () => await scope.ServiceProvider.GetRequiredService<IRepository<ConformanceOrder, Guid>>().FindAsync(Guid.CreateVersion7(), CancellationToken.None));
            Assert.Equal(PersistenceErrors.UnknownConnection, placed.Code);
            await unitOfWork.RollbackAsync(CancellationToken.None);
        }

        // 普通工作单元的 DbContext 建在默认连接上：登记到命名连接的实体在这里读写不到它的库，不静默落到默认库
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginAsync(UnitOfWorkOptions.Default, CancellationToken.None);
            var unsupported = await Assert.ThrowsAsync<FrameworkException>(async () => await scope.ServiceProvider.GetRequiredService<IRepository<ConformanceItem, int>>().AddAsync(new ConformanceItem { Id = 2, Label = "x" }, CancellationToken.None));
            Assert.Equal(PersistenceErrors.NotSupported, unsupported.Code);
            await unitOfWork.RollbackAsync(CancellationToken.None);
        }

        Assert.Equal(0L, Count(_default, "ConformanceItem"));
    }

    [Fact]
    public async Task Binding_IsRejected_WhenTheConnectionIsNotDeclared_OrTheDbContextAlreadyExists()
    {
        await using var host = await StartHostAsync();

        // 绑定的连接没有声明：开始时就是装配错误，工作单元保持未开始
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var missing = await Assert.ThrowsAsync<FrameworkException>(() => unitOfWork.BeginAsync(new UnitOfWorkOptions(Connection: "archive"), CancellationToken.None));
            Assert.Equal(PersistenceErrors.UnknownConnection, missing.Code);
            Assert.Equal(UnitOfWorkState.NotStarted, unitOfWork.State);
        }

        // 作用域里已经按默认连接建出了 DbContext：一个 DbContext 一个连接，换不了，要在新作用域里绑定
        await using (var scope = host.Services.CreateAsyncScope())
        {
            _ = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var built = await Assert.ThrowsAsync<FrameworkException>(() => unitOfWork.BeginAsync(new UnitOfWorkOptions(Connection: Leases), CancellationToken.None));
            Assert.Equal(PersistenceErrors.InvalidUnitOfWorkState, built.Code);
            Assert.Equal(UnitOfWorkState.NotStarted, unitOfWork.State);
        }
    }

    [Fact]
    public async Task Startup_RejectsAnEntityRegisteredOnAnUndeclaredConnection()
    {
        // 没有声明 leases：登记到它的实体在装配时就失败，不静默落到默认库
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => TestHost.StartAsync(
            static builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
            _default.Configuration,
            static services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddRepository<ConformanceOrder, Guid>();
                services.AddRepository<ConformanceItem, int>(Leases);
            }));

        var framework = Assert.Single(Chain(exception).OfType<FrameworkException>());
        Assert.Equal(PersistenceErrors.UnknownConnection, framework.Code);
        Assert.Contains(Leases, framework.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_ReadDeclaredConnections()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new($"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:Connections:{Leases}", "Data Source=x.db")]).Build();

        var options = EntityFrameworkCorePersistenceOptions.FromConfiguration(configuration);

        Assert.Equal("Data Source=x.db", Assert.Single(options.Connections, static pair => pair.Key == Leases).Value);
        Assert.Empty(new EntityFrameworkCorePersistenceOptions().Connections);
    }

    [Theory]
    [InlineData("Leases", "Data Source=x.db")]
    [InlineData("default", "Data Source=x.db")]
    [InlineData("shard", "Data Source=x.db")]
    [InlineData("-x", "Data Source=x.db")]
    [InlineData("leases", " ")]
    public void Options_RejectInvalidConnectionNamesAndBlankValues(string name, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new($"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:Connections:{name}", value)]).Build();

        var error = Assert.Throws<ArgumentException>(() => EntityFrameworkCorePersistenceOptions.FromConfiguration(configuration));

        Assert.Contains($"Connections:{name}", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("x.db", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_RejectASubsectionInsteadOfAConnectionString()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new($"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:Connections:{Leases}:value", "Data Source=x.db")]).Build();

        var error = Assert.Throws<ArgumentException>(() => EntityFrameworkCorePersistenceOptions.FromConfiguration(configuration));

        Assert.Contains($"Connections:{Leases}", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 启动带命名连接 <see cref="Leases"/> 的宿主
    /// </summary>
    /// <remarks>
    /// 订单落默认连接，条目登记到命名连接，开启 <c>EnsureCreated</c>。两个库的连接串都把忙等上限设为 2 秒：误把两个写者放进同一个库时尽快失败。<paramref name="seen"/> 不为空时记下回调看到的绑定主值与 <c>provider</c> 属性
    /// </remarks>
    private Task<TestHost> StartHostAsync(ConcurrentQueue<(string? Value, string? Provider)>? seen = null) => TestHost.StartAsync(
        builder => builder.UseFeature(new EntityFrameworkCorePersistenceFeature<ConformanceDbContext>((binding, options) =>
        {
            var value = binding.ValueOrThrow();
            seen?.Enqueue((value.Value, value.Properties.GetValueOrDefault("provider")));
            options.UseSqlite(value.Value);
        })),
        [
            new("XiHan:Bindings:db:value", Busy(_default)),
            new("XiHan:Bindings:db:provider", "sqlite"),
            new($"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:Connections:{Leases}", Busy(_named)),
            new($"{EntityFrameworkCorePersistenceOptions.ConfigurationSection}:EnsureCreated", "true"),
        ],
        static services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddRepository<ConformanceNote, Guid>();
            services.AddRepository<ConformanceItem, int>(Leases);
        });

    /// <summary>
    /// 在新作用域里开绑定 <see cref="Leases"/> 的可写工作单元，对条目仓储执行 <paramref name="action"/> 后提交
    /// </summary>
    private static async Task InLeasesAsync(TestHost host, Func<IRepository<ConformanceItem, int>, CancellationToken, Task> action)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(new UnitOfWorkOptions(Connection: Leases), CancellationToken.None);
        await action(scope.ServiceProvider.GetRequiredService<IRepository<ConformanceItem, int>>(), CancellationToken.None);
        (await unitOfWork.CommitAsync(CancellationToken.None)).ThrowIfFailure();
    }

    /// <summary>
    /// 带 2 秒忙等上限的连接串
    /// </summary>
    private static string Busy(SqliteDatabase database) => database.ConnectionString + ";Default Timeout=2";

    /// <summary>
    /// 直接连库数一张表的行数
    /// </summary>
    private static long Count(SqliteDatabase database, string table)
    {
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>
    /// 异常与它的内层异常
    /// </summary>
    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            yield return exception;
        }
    }
}
