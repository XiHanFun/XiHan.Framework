// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 多库：一次调用里平台实体落默认库、租户实体落分片库，失败时两边一起回滚；<c>[Tenant]</c> 显式落点优先；应用自注册的多连接客户端同样成立；指向不存在连接的 <c>[Tenant]</c> 是配置错误。
/// </summary>
public sealed class SqlSugarMultiConnectionTests : IDisposable
{
    /// <summary>
    /// 应用默认库。
    /// </summary>
    private readonly SqliteDatabase _default = new();

    /// <summary>
    /// 第二个库：作为租户 <c>acme</c> 的分片库，或方式二里的 <c>audit</c> 连接。
    /// </summary>
    private readonly SqliteDatabase _second = new();

    /// <summary>
    /// 删除两个临时数据库文件。
    /// </summary>
    public void Dispose()
    {
        _default.Dispose();
        _second.Dispose();
    }

    [Fact]
    public async Task Call_WritesPlatformRowsToDefault_AndTenantRowsToShard_AndRollsBackBothOnFailure()
    {
        using (var client = CreateClient(_second.ConnectionString))
        {
            client.CodeFirst.InitTables<ConformanceNote>();
        }

        await using var host = await StartShardedHostAsync();
        var orderId = Guid.CreateVersion7();
        var noteId = Guid.CreateVersion7();

        // 先写默认库再写分片库：分片连接在事务开启之后才首次被触及，必须也被纳入同一事务；调用以失败结束，两边都不能留下行
        var failed = await InvokeAsync(host, "acme", async ctx =>
        {
            (await Orders(ctx).AddAsync(new ConformanceOrder { Id = orderId, Name = "platform" }, ctx.CancellationToken)).ThrowIfFailure();
            (await Notes(ctx).AddAsync(new ConformanceNote { Id = noteId, TenantId = "acme", Text = "tenant" }, ctx.CancellationToken)).ThrowIfFailure();
            return Error.Validation(PersistenceErrors.TenantRequired, PersistenceMessages.TenantRequired, MessageArguments.Of(("entity", "test")));
        });
        Assert.Equal(PersistenceErrors.TenantRequired, failed.Error?.Code);
        using (var defaultClient = CreateClient(_default.ConnectionString))
        using (var shardClient = CreateClient(_second.ConnectionString))
        {
            Assert.Empty(defaultClient.Queryable<ConformanceOrder>().ToList());
            Assert.Empty(defaultClient.Queryable<ConformanceNote>().ToList());
            Assert.Empty(shardClient.Queryable<ConformanceNote>().ToList());
        }

        // 成功的调用：平台行在默认库，租户行在分片库，默认库里没有租户行
        (await InvokeAsync(host, "acme", async ctx =>
        {
            (await Orders(ctx).AddAsync(new ConformanceOrder { Id = orderId, Name = "platform" }, ctx.CancellationToken)).ThrowIfFailure();
            return await Notes(ctx).AddAsync(new ConformanceNote { Id = noteId, TenantId = "acme", Text = "tenant" }, ctx.CancellationToken);
        })).ThrowIfFailure();
        using (var defaultClient = CreateClient(_default.ConnectionString))
        using (var shardClient = CreateClient(_second.ConnectionString))
        {
            Assert.Equal([orderId], defaultClient.Queryable<ConformanceOrder>().ToList().Select(static o => o.Id));
            Assert.Empty(defaultClient.Queryable<ConformanceNote>().ToList());
            Assert.Equal([noteId], shardClient.Queryable<ConformanceNote>().ToList().Select(static n => n.Id));
        }

        // 同一调用里两边都能读回自己写的行
        var seen = (false, false);
        (await InvokeAsync(host, "acme", async ctx =>
        {
            seen = ((await Orders(ctx).FindAsync(orderId, ctx.CancellationToken)).HasValue, (await Notes(ctx).FindAsync(noteId, ctx.CancellationToken)).HasValue);
            return Result.Success();
        })).ThrowIfFailure();
        Assert.Equal((true, true), seen);
    }

    [Fact]
    public async Task ApplicationClient_WithSeveralConnections_PinsEntitiesByTenantAttribute()
    {
        // 方式二：应用自己注册带 default 与 audit 两个连接的客户端；启动建表按落点分组，[Tenant("audit")] 的实体在 audit 库建表
        await using var host = await TestHost.StartAsync(
            static builder => builder.UseFeature(new SqlSugarPersistenceFeature()),
            [new("XiHan:Persistence:SqlSugar:InitTables", "true")],
            services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddScoped<ISqlSugarClient>(_ => new SqlSugarClient(
                [
                    SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = SqlSugarConnections.Default, DbType = DbType.Sqlite, ConnectionString = _default.ConnectionString, IsAutoCloseConnection = true }),
                    SqlSugarConventions.Apply(new ConnectionConfig { ConfigId = "audit", DbType = DbType.Sqlite, ConnectionString = _second.ConnectionString, IsAutoCloseConnection = true }),
                ]));
                services.AddRepository<ConformanceOrder, Guid>();
                services.AddRepository<PinnedAuditRecord, Guid>();
            });
        var orderId = Guid.CreateVersion7();
        var auditId = Guid.CreateVersion7();

        (await host.InvokeAsync(async ctx =>
        {
            var unitOfWork = ctx.Services.GetRequiredService<SqlSugarUnitOfWork>();
            Assert.Equal(_default.ConnectionString, unitOfWork.ConnectionFor(typeof(ConformanceOrder)).CurrentConnectionConfig.ConnectionString);
            Assert.Equal(_second.ConnectionString, unitOfWork.ConnectionFor(typeof(PinnedAuditRecord)).CurrentConnectionConfig.ConnectionString);
            (await Orders(ctx).AddAsync(new ConformanceOrder { Id = orderId, Name = "order" }, ctx.CancellationToken)).ThrowIfFailure();
            (await ctx.Services.GetRequiredService<IRepository<PinnedAuditRecord, Guid>>().AddAsync(new PinnedAuditRecord { Id = auditId, Text = "audit" }, ctx.CancellationToken)).ThrowIfFailure();
        })).ThrowIfFailure();

        using var defaultClient = CreateClient(_default.ConnectionString);
        using var auditClient = CreateClient(_second.ConnectionString);
        Assert.Equal([orderId], defaultClient.Queryable<ConformanceOrder>().ToList().Select(static o => o.Id));
        Assert.False(defaultClient.DbMaintenance.IsAnyTable(defaultClient.EntityMaintenance.GetTableName<PinnedAuditRecord>(), false));
        Assert.Equal([auditId], auditClient.Queryable<PinnedAuditRecord>().ToList().Select(static r => r.Id));
    }

    [Fact]
    public void Selector_RejectsPinnedConnectionThatDoesNotExist()
    {
        using var client = CreateClient(_default.ConnectionString);
        var selector = new SqlSugarConnectionSelector();

        // 没有分片连接时租户实体与平台实体都落根连接；[Tenant] 指向的连接不存在是装配错误，不回退
        Assert.Same(client, selector.ConnectionFor(typeof(ConformanceOrder), client));
        Assert.Same(client, selector.ConnectionFor(typeof(ConformanceNote), client));
        var error = Assert.Throws<FrameworkException>(() => selector.ConnectionFor(typeof(PinnedAuditRecord), client));
        Assert.Equal(PersistenceErrors.StoreUnavailable, error.Code);
    }

    /// <summary>
    /// 启动带默认库、租户 <c>acme</c> 分片到第二个库的宿主；默认库建两种套件实体的表。
    /// </summary>
    private Task<TestHost> StartShardedHostAsync() => TestHost.StartAsync(
        static b => b.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(SqliteDatabase.CreateFeature()),
        [
            .. _default.Configuration,
            new("XiHan:MultiTenancy:Tenants:acme:Properties:" + TenantShardKeys.ConnectionString, _second.ConnectionString),
            new("XiHan:MultiTenancy:Tenants:acme:Properties:" + TenantShardKeys.DatabaseProvider, "sqlite"),
        ],
        static services =>
        {
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddRepository<ConformanceNote, Guid>();
        });

    /// <summary>
    /// 创建指向给定连接串、已施加约定的独立客户端。
    /// </summary>
    private static SqlSugarClient CreateClient(string connectionString)
        => new(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = connectionString, IsAutoCloseConnection = true }));

    /// <summary>
    /// 当前调用作用域内的订单（平台实体）仓储。
    /// </summary>
    private static IRepository<ConformanceOrder, Guid> Orders(InvocationContext context)
        => context.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>();

    /// <summary>
    /// 当前调用作用域内的笔记（租户实体）仓储。
    /// </summary>
    private static IRepository<ConformanceNote, Guid> Notes(InvocationContext context)
        => context.Services.GetRequiredService<IRepository<ConformanceNote, Guid>>();

    /// <summary>
    /// 以给定租户（经 Baggage 自报）走完整管道执行 <paramref name="action"/>，其返回值作为调用结果。
    /// </summary>
    private static async Task<Result> InvokeAsync(TestHost host, string tenant, Func<InvocationContext, Task<Result>> action)
        => await host.InvokeAsync(async context => context.SetOutcome(await action(context)), baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = tenant });
}
