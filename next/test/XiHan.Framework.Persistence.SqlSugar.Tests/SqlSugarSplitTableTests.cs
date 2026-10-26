// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Persistence.Abstractions;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 分表：带 <c>[SplitTable]</c> 的实体经仓储写入按分表字段落到各月的表，查找跨表、列表与计数按 <c>Within</c> 的时间范围只扫范围内的表，更新 / 删除与批量方法在分表上同样成立。
/// </summary>
public sealed class SqlSugarSplitTableTests : IDisposable
{
    /// <summary>
    /// 2026 年 1 月 10 日（UTC）。
    /// </summary>
    private static readonly DateTime _january = new(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 2026 年 2 月 10 日（UTC）。
    /// </summary>
    private static readonly DateTime _february = new(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 本测试实例独占的临时 SQLite 数据库。
    /// </summary>
    private readonly SqliteDatabase _database = new();

    /// <summary>
    /// 测试结束后删除临时数据库文件。
    /// </summary>
    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Repository_WritesToMonthlyTables_AndReadsAcrossThem()
    {
        await using var host = await StartHostAsync();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var third = Guid.CreateVersion7();

        // 两条一月、一条二月：单条新增与批量新增各走一次分表插入
        (await RunAsync(host, ctx => Repository(ctx).AddAsync(new SplitTableRecord { Id = first, RecordedAt = _january, Text = "jan-1" }, ctx.CancellationToken))).ThrowIfFailure();
        (await RunAsync(host, async ctx =>
        {
            var results = await Repository(ctx).AddRangeAsync(
            [
                new SplitTableRecord { Id = second, RecordedAt = _january.AddDays(5), Text = "jan-2" },
                new SplitTableRecord { Id = third, RecordedAt = _february, Text = "feb-1" },
            ], ctx.CancellationToken);
            Assert.All(results, static r => Assert.True(r.IsSuccess, r.ToString()));
            return Result.Success();
        })).ThrowIfFailure();

        // 物理上是两张按月命名的表
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var tables = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().DbMaintenance.GetTableInfoList(false).Select(static t => t.Name).ToList();
            Assert.Contains(tables, static t => t.StartsWith("split_records_202601", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(tables, static t => t.StartsWith("split_records_202602", StringComparison.OrdinalIgnoreCase));
        }

        // 查找跨全部分表；范围提示只扫一月的表；没有提示扫全部；计数同样
        Maybe<SplitTableRecord> found = default;
        IReadOnlyList<SplitTableRecord> january = [];
        IReadOnlyList<SplitTableRecord> all = [];
        IReadOnlyList<SplitTableRecord> many = [];
        long januaryCount = 0;
        (await RunAsync(host, async ctx =>
        {
            var repository = Repository(ctx);
            found = await repository.FindAsync(third, ctx.CancellationToken);
            var inJanuary = EntityQuery.All<SplitTableRecord>().Within(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero)).OrderBy(static r => r.Text);
            january = await repository.ListAsync(inJanuary, ctx.CancellationToken);
            januaryCount = await repository.CountAsync(inJanuary, ctx.CancellationToken);
            all = await repository.ListAsync(EntityQuery.All<SplitTableRecord>().OrderBy(static r => r.Text), ctx.CancellationToken);
            many = await repository.FindManyAsync([third, first], ctx.CancellationToken);
            return Result.Success();
        })).ThrowIfFailure();
        Assert.Equal("feb-1", found.ValueOrThrow().Text);
        Assert.Equal(["jan-1", "jan-2"], january.Select(static r => r.Text));
        Assert.Equal(2, januaryCount);
        Assert.Equal(["feb-1", "jan-1", "jan-2"], all.Select(static r => r.Text));
        Assert.Equal([third, first], many.Select(static r => r.Id));
    }

    [Fact]
    public async Task Repository_UpdatesAndRemoves_OnSplitTables_WithStampCheck()
    {
        await using var host = await StartHostAsync();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        (await RunAsync(host, async ctx => Fold(await Repository(ctx).AddRangeAsync(
        [
            new SplitTableRecord { Id = first, RecordedAt = _january, Text = "jan" },
            new SplitTableRecord { Id = second, RecordedAt = _february, Text = "feb" },
        ], ctx.CancellationToken)))).ThrowIfFailure();

        // 更新二月那条：并发戳刷新；拿旧戳的游离实例再更新冲突
        string? stampBefore = null;
        string? stampAfter = null;
        (await RunAsync(host, async ctx =>
        {
            var loaded = (await Repository(ctx).FindAsync(second, ctx.CancellationToken)).ValueOrThrow();
            stampBefore = loaded.ConcurrencyStamp;
            loaded.Text = "feb-updated";
            var updated = await Repository(ctx).UpdateAsync(loaded, ctx.CancellationToken);
            stampAfter = loaded.ConcurrencyStamp;
            return updated;
        })).ThrowIfFailure();
        Assert.NotEqual(stampBefore, stampAfter);
        var stale = new SplitTableRecord { Id = second, RecordedAt = _february, Text = "stale", ConcurrencyStamp = stampBefore! };
        Assert.Equal(PersistenceErrors.Concurrency, (await RunAsync(host, ctx => Repository(ctx).UpdateAsync(stale, ctx.CancellationToken))).Error?.Code);

        // 批量更新一月那条、物理删除二月那条；之后只剩一月且内容已更新
        (await RunAsync(host, async ctx =>
        {
            var repository = Repository(ctx);
            var jan = (await repository.FindAsync(first, ctx.CancellationToken)).ValueOrThrow();
            jan.Text = "jan-updated";
            Fold(await repository.UpdateRangeAsync([jan], ctx.CancellationToken)).ThrowIfFailure();
            var feb = (await repository.FindAsync(second, ctx.CancellationToken)).ValueOrThrow();
            return await repository.RemoveAsync(feb, ctx.CancellationToken);
        })).ThrowIfFailure();
        IReadOnlyList<SplitTableRecord> remaining = [];
        (await RunAsync(host, async ctx =>
        {
            remaining = await Repository(ctx).ListAsync(EntityQuery.All<SplitTableRecord>(), ctx.CancellationToken);
            return Result.Success();
        })).ThrowIfFailure();
        Assert.Equal(["jan-updated"], remaining.Select(static r => r.Text));

        // 批量删除：存在的删掉、不存在的逐项 NotFound
        IReadOnlyList<Result> removed = [];
        (await RunAsync(host, async ctx =>
        {
            removed = await Repository(ctx).RemoveRangeAsync([new SplitTableRecord { Id = first, RecordedAt = _january }, new SplitTableRecord { Id = second, RecordedAt = _february }], ctx.CancellationToken);
            return Result.Success();
        })).ThrowIfFailure();
        Assert.True(removed[0].IsSuccess);
        Assert.Equal(PersistenceErrors.NotFound, removed[1].Error?.Code);
        (await RunAsync(host, async ctx =>
        {
            Assert.Equal(0, await Repository(ctx).CountAsync(EntityQuery.All<SplitTableRecord>(), ctx.CancellationToken));
            return Result.Success();
        })).ThrowIfFailure();
    }

    /// <summary>
    /// 启动带本数据库、分表实体与 <c>InitTables</c> 的宿主。
    /// </summary>
    private Task<TestHost> StartHostAsync() => TestHost.StartAsync(
        static builder => builder.UseFeature(SqliteDatabase.CreateFeature()),
        _database.Configuration,
        static services =>
        {
            services.AddSingleton(TimeProvider.System);
            services.AddRepository<SplitTableRecord, Guid>();
        });

    /// <summary>
    /// 取调用作用域内的分表实体仓储。
    /// </summary>
    private static IRepository<SplitTableRecord, Guid> Repository(InvocationContext context)
        => context.Services.GetRequiredService<IRepository<SplitTableRecord, Guid>>();

    /// <summary>
    /// 逐项结果折叠成一个：第一个失败即整体失败。
    /// </summary>
    private static Result Fold(IReadOnlyList<Result> results) => results.FirstOrDefault(static r => r.IsFailure, Result.Success());

    /// <summary>
    /// 在一次完整管道调用内执行 <paramref name="action"/>，其返回值作为调用结果。
    /// </summary>
    private static async Task<Result> RunAsync(TestHost host, Func<InvocationContext, Task<Result>> action)
        => await host.InvokeAsync(async context => context.SetOutcome(await action(context)));
}
