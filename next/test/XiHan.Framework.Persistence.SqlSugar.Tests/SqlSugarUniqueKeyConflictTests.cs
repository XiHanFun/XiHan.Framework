// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 撞上唯一索引时返回唯一键重复（ADR-0072）。
/// </summary>
/// <remarks>
/// 仓储返回 <c>PERSISTENCE_CONFLICT_003</c>，不让数据库异常逃出（RFC-0005「索引声明」）。
/// 新增（同一批两项同键、已提交的另一行同键）、更新把键改成已有的值、软删除撞上同一键已删除的行；之后工作单元只能回滚，调用以同一错误结束，库里不留这次的写入。PostgreSQL 上语句失败后事务已中止，核对走另开的连接，同样在真实库上验证（<c>XIHAN_TEST_POSTGRES</c>）。
/// </remarks>
public sealed class SqlSugarUniqueKeyConflictTests
{
    /// <summary>
    /// 固定的时刻。
    /// </summary>
    private static readonly DateTimeOffset _at = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddRange_WithTwoItemsSharingAKey_ReportsUniqueKeyConflict_OnSqlite()
    {
        using var database = new SqliteDatabase();
        await using var host = await StartAsync(SqliteDatabase.CreateFeature(), database.Configuration);

        var outcome = await host.InvokeAsync(async context =>
        {
            var results = await Records(context).AddRangeAsync([Record("acme", "A"), Record("acme", "A")], context.CancellationToken);
            Assert.All(results, static r => Assert.Equal(PersistenceErrors.UniqueKeyConflict, r.Error?.Code));
            Assert.Equal("UX_indexed_record_TeId_Co", results[0].Error!.Details!["index"]);
        });

        // 处理器没有返回失败，工作单元也已标为失败：提交回滚并以同一错误结束
        Assert.Equal(PersistenceErrors.UniqueKeyConflict, outcome.Error?.Code);
        Assert.Equal(0, await CountAsync(host));
    }

    [Fact]
    public async Task Add_WhenAnotherRowHoldsTheKey_ReportsUniqueKeyConflict_OnSqlite()
    {
        using var database = new SqliteDatabase();
        await using var host = await StartAsync(SqliteDatabase.CreateFeature(), database.Configuration);
        await AddsTheSameKeyTwiceAsync(host);
    }

    [Fact]
    public async Task Add_WhenAnotherRowHoldsTheKey_ReportsUniqueKeyConflict_OnPostgreSql()
    {
        using var database = PostgresTestServer.CreateDatabase();
        await using var host = await StartAsync(PostgresDatabase.CreateFeature(), database.Configuration);
        await AddsTheSameKeyTwiceAsync(host);
    }

    [Fact]
    public async Task Update_ToAKeyAnotherRowHolds_ReportsUniqueKeyConflict_OnSqlite()
    {
        using var database = new SqliteDatabase();
        await using var host = await StartAsync(SqliteDatabase.CreateFeature(), database.Configuration);
        var first = Record("acme", "A");
        var second = Record("acme", "B");
        (await host.InvokeAsync(async context => context.SetOutcome(Combine(await Records(context).AddRangeAsync([first, second], context.CancellationToken))))).ThrowIfFailure();

        var outcome = await host.InvokeAsync(async context =>
        {
            var found = (await Records(context).FindAsync(second.Id, context.CancellationToken)).ValueOrThrow();
            found.Code = "A";
            Assert.Equal(PersistenceErrors.UniqueKeyConflict, (await Records(context).UpdateAsync(found, context.CancellationToken)).Error?.Code);
        });

        Assert.Equal(PersistenceErrors.UniqueKeyConflict, outcome.Error?.Code);
        await using var scope = host.Services.CreateAsyncScope();
        Assert.Equal(["A", "B"], scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Queryable<IndexedRecord>().OrderBy(static r => r.Code).Select(static r => r.Code).ToList());
    }

    [Fact]
    public async Task SoftDelete_OfASecondRowWithADeletedTwin_ReportsUniqueKeyConflict_OnSqlite()
    {
        using var database = new SqliteDatabase();
        await using var host = await StartAsync(SqliteDatabase.CreateFeature(), database.Configuration);
        var first = new SoftDeletedIndexedRecord { Id = Guid.CreateVersion7(), Code = "A" };
        var second = new SoftDeletedIndexedRecord { Id = Guid.CreateVersion7(), Code = "A" };
        // 删掉第一行之后同一编码可以再建一行
        (await host.InvokeAsync(async context => context.SetOutcome(await Soft(context).AddAsync(first, context.CancellationToken)))).ThrowIfFailure();
        (await host.InvokeAsync(async context => context.SetOutcome(await Soft(context).RemoveAsync(first, context.CancellationToken)))).ThrowIfFailure();
        (await host.InvokeAsync(async context => context.SetOutcome(await Soft(context).AddAsync(second, context.CancellationToken)))).ThrowIfFailure();

        // 再删第二行：唯一索引带删除标记，同一编码已有一行已删除
        var outcome = await host.InvokeAsync(async context => context.SetOutcome(await Soft(context).RemoveAsync(second, context.CancellationToken)));

        Assert.Equal(PersistenceErrors.UniqueKeyConflict, outcome.Error?.Code);
        Assert.Equal("UX_soft_deleted_indexed_record_Co", outcome.Error!.Details!["index"]);
    }

    /// <summary>
    /// 先提交一行，再在另一次调用里新增同一个键的另一行。
    /// </summary>
    /// <remarks>
    /// 新增前的标识核对查不到冲突（标识不同），写入时撞上唯一索引，按唯一键重复报告。
    /// </remarks>
    private static async Task AddsTheSameKeyTwiceAsync(TestHost host)
    {
        (await host.InvokeAsync(async context => context.SetOutcome(await Records(context).AddAsync(Record("acme", "A"), context.CancellationToken)))).ThrowIfFailure();

        var outcome = await host.InvokeAsync(async context =>
        {
            var result = await Records(context).AddAsync(Record("acme", "A"), context.CancellationToken);
            Assert.Equal(PersistenceErrors.UniqueKeyConflict, result.Error?.Code);
            context.SetOutcome(result);
        });

        Assert.Equal(PersistenceErrors.UniqueKeyConflict, outcome.Error?.Code);
        Assert.Equal(1, await CountAsync(host));
    }

    /// <summary>
    /// 启动只登记了两个索引实体的宿主。
    /// </summary>
    private static Task<TestHost> StartAsync(IFeature feature, IEnumerable<KeyValuePair<string, string?>> configuration) => TestHost.StartAsync(
        builder => builder.UseFeature(feature),
        configuration,
        static services =>
        {
            services.AddRepository<IndexedRecord, Guid>();
            services.AddRepository<SoftDeletedIndexedRecord, Guid>();
        });

    /// <summary>
    /// 表里的行数，绕过仓储直接查。
    /// </summary>
    private static async Task<int> CountAsync(TestHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().Queryable<IndexedRecord>().CountAsync();
    }

    /// <summary>
    /// 有唯一索引的实体的仓储。
    /// </summary>
    private static IRepository<IndexedRecord, Guid> Records(InvocationContext context) => context.Services.GetRequiredService<IRepository<IndexedRecord, Guid>>();

    /// <summary>
    /// 软删除实体的仓储。
    /// </summary>
    private static IRepository<SoftDeletedIndexedRecord, Guid> Soft(InvocationContext context) => context.Services.GetRequiredService<IRepository<SoftDeletedIndexedRecord, Guid>>();

    /// <summary>
    /// 逐项结果合成一个：有失败取第一个失败。
    /// </summary>
    private static Result Combine(IReadOnlyList<Result> results) => results.Any(static r => r.IsFailure) ? results.First(static r => r.IsFailure) : Result.Success();

    /// <summary>
    /// 一行记录。
    /// </summary>
    private static IndexedRecord Record(string tenantId, string code)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, Code = code, Category = "c", At = _at };
}
