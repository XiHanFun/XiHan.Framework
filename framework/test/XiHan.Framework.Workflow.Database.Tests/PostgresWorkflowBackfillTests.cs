// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 查询列的回填在真实的 PostgreSQL 上（ADR-0304）
/// </summary>
/// <remarks>
/// 环境变量 <c>XIHAN_TEST_POSTGRES</c>，未设置即跳过（<see cref="PostgresDatabase"/>），由 CI 的数据库分片运行；本机跑的同样用例在 <see cref="DatabaseWorkflowBackfillTests"/>。PostgreSQL 的 <c>LIKE</c> 区分大小写、时间到微秒，游标与折叠后的匹配都在这里落到真实的 SQL 上
/// </remarks>
public sealed class PostgresWorkflowBackfillTests
{
    [Fact]
    public async Task Backfill_RefoldsInBatches_AndASecondRunRewritesNothing()
    {
        await using var database = PostgresDatabase.Create();
        var host = await database.StartHostAsync();
        var stale = await BackfillScenario.SeedAsync(host, null);
        Assert.Equal((0, 0, 0, 0), await BackfillScenario.CountMatchesAsync(host, null));

        var first = (await BackfillScenario.RunAsync(host, null, 2)).ValueOrThrow();

        Assert.Equal(new DatabaseWorkflowBackfillReport { Scanned = BackfillScenario.Scanned, Rewritten = stale, Busy = 0, Batches = BackfillScenario.BatchesOf(2) }, first);
        Assert.Equal(BackfillScenario.Expected, await BackfillScenario.CountMatchesAsync(host, null));
        Assert.Equal(first with { Rewritten = 0 }, (await BackfillScenario.RunAsync(host, null, 2)).ValueOrThrow());
    }

    [Fact]
    public async Task Backfill_ReturnsOneResultPerPartition_InOrder()
    {
        await using var database = PostgresDatabase.Create();
        var host = await database.StartHostAsync();
        var platform = await BackfillScenario.SeedAsync(host, null);
        var tenant = await BackfillScenario.SeedAsync(host, "t1");

        var results = await host.Services.GetRequiredService<DatabaseWorkflowBackfill>().RunAsync([null, "t1", "t1"], 3, CancellationToken.None);

        Assert.Equal([platform, tenant, 0], results.Select(static result => result.ValueOrThrow().Rewritten));
        Assert.Equal(BackfillScenario.Expected, await BackfillScenario.CountMatchesAsync(host, "t1"));
    }
}
