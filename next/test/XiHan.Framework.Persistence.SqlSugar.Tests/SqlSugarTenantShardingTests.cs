// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 租户分库：带 <c>ConnectionString</c> 属性的租户在作用域内拿到指向分片库的客户端，其余租户与无租户调用用应用默认连接。
/// </summary>
public sealed class SqlSugarTenantShardingTests : IDisposable
{
    /// <summary>
    /// 应用默认库。
    /// </summary>
    private readonly SqliteDatabase _default = new();

    /// <summary>
    /// 租户 <c>acme</c> 的分片库。
    /// </summary>
    private readonly SqliteDatabase _shard = new();

    /// <summary>
    /// 删除两个临时数据库文件。
    /// </summary>
    public void Dispose()
    {
        _default.Dispose();
        _shard.Dispose();
    }

    [Fact]
    public async Task Repository_UsesTenantShard_WhenTenantHasConnectionString()
    {
        // 分片库的表结构由应用管理：启动只在默认库建表，这里用独立客户端给分片库建表
        using (var client = CreateClient(_shard.ConnectionString))
        {
            client.CodeFirst.InitTables<ConformanceNote>();
        }

        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(SqliteDatabase.CreateFeature()),
            [
                .. _default.Configuration,
                new("XiHan:MultiTenancy:Tenants:acme:Properties:" + TenantShardKeys.ConnectionString, _shard.ConnectionString),
                new("XiHan:MultiTenancy:Tenants:acme:Properties:" + TenantShardKeys.DatabaseProvider, "sqlite"),
                new("XiHan:MultiTenancy:Tenants:beta:DisplayName", "Beta"),
            ],
            static services => services.AddRepository<ConformanceNote, Guid>());

        var acmeId = Guid.CreateVersion7();
        var betaId = Guid.CreateVersion7();
        string? acmeConnection = null;
        string? betaConnection = null;
        (await InvokeAsync(host, "acme", ctx =>
        {
            acmeConnection = ctx.Services.GetRequiredService<ISqlSugarClient>().CurrentConnectionConfig.ConnectionString;
            return Repository(ctx).AddAsync(new ConformanceNote { Id = acmeId, TenantId = "acme", Text = "acme" }, ctx.CancellationToken);
        })).ThrowIfFailure();
        (await InvokeAsync(host, "beta", ctx =>
        {
            betaConnection = ctx.Services.GetRequiredService<ISqlSugarClient>().CurrentConnectionConfig.ConnectionString;
            return Repository(ctx).AddAsync(new ConformanceNote { Id = betaId, TenantId = "beta", Text = "beta" }, ctx.CancellationToken);
        })).ThrowIfFailure();

        Assert.Equal(_shard.ConnectionString, acmeConnection);
        Assert.Equal(_default.ConnectionString, betaConnection);

        // 各自通过仓储只看到自己的行
        var acmeFound = false;
        var betaFound = false;
        (await InvokeAsync(host, "acme", async ctx =>
        {
            acmeFound = (await Repository(ctx).FindAsync(acmeId, ctx.CancellationToken)).HasValue;
            return Result.Success();
        })).ThrowIfFailure();
        (await InvokeAsync(host, "beta", async ctx =>
        {
            betaFound = (await Repository(ctx).FindAsync(betaId, ctx.CancellationToken)).HasValue;
            return Result.Success();
        })).ThrowIfFailure();
        Assert.True(acmeFound);
        Assert.True(betaFound);

        // 行物理上落在不同的库文件里
        using var shardClient = CreateClient(_shard.ConnectionString);
        using var defaultClient = CreateClient(_default.ConnectionString);
        Assert.Equal([acmeId], shardClient.Queryable<ConformanceNote>().ToList().Select(static n => n.Id));
        Assert.Equal([betaId], defaultClient.Queryable<ConformanceNote>().ToList().Select(static n => n.Id));
    }

    /// <summary>
    /// 创建指向给定连接串、已施加约定的独立客户端。
    /// </summary>
    private static SqlSugarClient CreateClient(string connectionString)
        => new(SqlSugarConventions.Apply(new ConnectionConfig { DbType = DbType.Sqlite, ConnectionString = connectionString, IsAutoCloseConnection = true }));

    /// <summary>
    /// 当前调用作用域内的套件笔记仓储。
    /// </summary>
    private static IRepository<ConformanceNote, Guid> Repository(InvocationContext context)
        => context.Services.GetRequiredService<IRepository<ConformanceNote, Guid>>();

    /// <summary>
    /// 以给定租户（经 Baggage 自报）走完整管道执行 <paramref name="action"/>，其返回值作为调用结果。
    /// </summary>
    private static async Task<Result> InvokeAsync(TestHost host, string tenant, Func<InvocationContext, Task<Result>> action)
        => await host.InvokeAsync(async context => context.SetOutcome(await action(context)), baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = tenant });
}
