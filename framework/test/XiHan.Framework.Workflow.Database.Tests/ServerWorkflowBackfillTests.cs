// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 查询列的回填在真实的数据库服务器上（ADR-0304）
/// </summary>
/// <remarks>
/// PostgreSQL、SQL Server、MySQL、Oracle 各派生一个类，给出按环境变量门控的夹具（<see cref="IGatedDatabase"/>），未设置即跳过，由 CI 的 database 与 oracle 分片运行；本机跑的同样用例在 <see cref="DatabaseWorkflowBackfillTests"/>。游标的时间比较、折叠后的 <c>LIKE</c> 与空串、<c>NULL</c> 的等同都在这里落到各库真实的 SQL 上
/// </remarks>
public abstract class ServerWorkflowBackfillTests
{
    [Fact]
    public async Task Backfill_RefoldsInBatches_AndASecondRunRewritesNothing()
    {
        await using var database = CreateDatabase();
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
        await using var database = CreateDatabase();
        var host = await database.StartHostAsync();
        var platform = await BackfillScenario.SeedAsync(host, null);
        var tenant = await BackfillScenario.SeedAsync(host, "t1");

        var results = await host.Services.GetRequiredService<DatabaseWorkflowBackfill>().RunAsync([null, "t1", "t1"], 3, CancellationToken.None);

        Assert.Equal([platform, tenant, 0], results.Select(static result => result.ValueOrThrow().Rewritten));
        Assert.Equal(BackfillScenario.Expected, await BackfillScenario.CountMatchesAsync(host, "t1"));
    }

    /// <summary>
    /// 建一个临时的库（未设置环境变量时跳过当前用例）
    /// </summary>
    private protected abstract IGatedDatabase CreateDatabase();
}
