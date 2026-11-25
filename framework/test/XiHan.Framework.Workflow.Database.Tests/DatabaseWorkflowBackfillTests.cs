// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Workflow.Database.Locking;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 查询列的回填在本机的库上（RFC-0021「默认实现与 Provider」，ADR-0304）
/// </summary>
/// <remarks>
/// SQLite（SqlSugar 与 EF Core 两种持久化），不靠外部的数据库服务：分批、可以重复跑（已是新值的行不再改写）、锁被占用时跳过、可取消、批大小有上限、按分区的逐项结果。真实数据库上同样的用例在 <see cref="ServerWorkflowBackfillTests"/> 的四个派生类里（PostgreSQL、SQL Server、MySQL、Oracle）
/// </remarks>
public sealed class DatabaseWorkflowBackfillTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(DatabaseWorkflowBackfill.MaxBatchSize)]
    public async Task Backfill_RefoldsInBatches_AndASecondRunRewritesNothing(int batchSize)
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var stale = await BackfillScenario.SeedAsync(host, null);

        // 升级前的折叠留下了 İ 与平面以外的大写字母：关键字一个也查不到
        Assert.Equal((0, 0, 0, 0), await BackfillScenario.CountMatchesAsync(host, null));

        var first = (await BackfillScenario.RunAsync(host, null, batchSize)).ValueOrThrow();
        Assert.Equal(new DatabaseWorkflowBackfillReport { Scanned = BackfillScenario.Scanned, Rewritten = stale, Busy = 0, Batches = BackfillScenario.BatchesOf(batchSize) }, first);
        Assert.Equal(BackfillScenario.Expected, await BackfillScenario.CountMatchesAsync(host, null));

        // 再跑一次：每一行都已是新值，一行也不改写
        var second = (await BackfillScenario.RunAsync(host, null, batchSize)).ValueOrThrow();
        Assert.Equal(first with { Rewritten = 0 }, second);
    }

    [Fact]
    public async Task Backfill_OnEntityFrameworkCore_RefoldsTheSameRows()
    {
        // EF Core 在 SQLite 上把时间存成刻度整数：游标的 >= 与同刻的偏移照样不重不漏
        await using var database = new EntityFrameworkCoreSqliteDatabase();
        var host = await database.StartHostAsync();
        var stale = await BackfillScenario.SeedAsync(host, null);

        var report = (await BackfillScenario.RunAsync(host, null, 2)).ValueOrThrow();

        Assert.Equal((BackfillScenario.Scanned, stale, BackfillScenario.BatchesOf(2)), (report.Scanned, report.Rewritten, report.Batches));
        Assert.Equal(BackfillScenario.Expected, await BackfillScenario.CountMatchesAsync(host, null));
        Assert.Equal(0, (await BackfillScenario.RunAsync(host, null, 2)).ValueOrThrow().Rewritten);
    }

    [Fact]
    public async Task Backfill_SkipsAnInstanceWhoseLockIsHeld_UntilARerun()
    {
        // 锁的等待上限是 0.2 秒（SqliteDatabase 的配置）：拿不到锁的实例这次跳过，它的行原样留着
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var stale = await BackfillScenario.SeedAsync(host, null);
        var locks = host.Services.GetRequiredService<DatabaseWorkflowInstanceLock>();

        var held = (await locks.AcquireAsync("bf-platform-0", CancellationToken.None)).ValueOrThrow();
        DatabaseWorkflowBackfillReport busy;
        await using (held)
        {
            busy = (await BackfillScenario.RunAsync(host, null, 2)).ValueOrThrow();
        }

        // 跳过的是实例 0 与它的任务书签两行；关键字查到的实例与待办各少一个
        Assert.Equal((1, stale - 2), (busy.Busy, busy.Rewritten));
        var (definitions, instances, tasks, _) = await BackfillScenario.CountMatchesAsync(host, null);
        Assert.Equal((BackfillScenario.Expected.Definitions, BackfillScenario.Expected.Instances - 1, BackfillScenario.Expected.Tasks - 1), (definitions, instances, tasks));

        // 锁释放之后再跑一次补上
        var rerun = (await BackfillScenario.RunAsync(host, null, 2)).ValueOrThrow();
        Assert.Equal((0, 2), (rerun.Busy, rerun.Rewritten));
        Assert.Equal(BackfillScenario.Expected, await BackfillScenario.CountMatchesAsync(host, null));
    }

    [Fact]
    public async Task Backfill_ReturnsOneResultPerPartition_InOrder()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var platform = await BackfillScenario.SeedAsync(host, null);
        var tenant = await BackfillScenario.SeedAsync(host, "t1");
        var backfill = host.Services.GetRequiredService<DatabaseWorkflowBackfill>();

        // 逐项结果与入参等长同序；同一分区第二次出现时看见第一次的结果
        var results = await backfill.RunAsync([null, "t1", null, "t2"], 3, CancellationToken.None);

        Assert.Equal([platform, tenant, 0, 0], results.Select(static result => result.ValueOrThrow().Rewritten));
        Assert.Equal((BackfillScenario.Scanned, BackfillScenario.Scanned, BackfillScenario.Scanned, 0L), (results[0].ValueOrThrow().Scanned, results[1].ValueOrThrow().Scanned, results[2].ValueOrThrow().Scanned, results[3].ValueOrThrow().Scanned));
        Assert.Equal(BackfillScenario.Expected, await BackfillScenario.CountMatchesAsync(host, "t1"));

        // 空集合不触存储
        Assert.Empty(await backfill.RunAsync([], 3, CancellationToken.None));
    }

    [Fact]
    public async Task Backfill_RejectsBadArguments_BeforeTouchingTheStore()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        await BackfillScenario.SeedAsync(host, null);
        var backfill = host.Services.GetRequiredService<DatabaseWorkflowBackfill>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await backfill.RunAsync([null], 0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await backfill.RunAsync([null], DatabaseWorkflowBackfill.MaxBatchSize + 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () => await backfill.RunAsync([null, " "], 10, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await backfill.RunAsync(null!, 10, CancellationToken.None));

        // 空白的租户排在后面也先核对：前面的平台分区一行也没有改写
        Assert.Equal((0, 0, 0, 0), await BackfillScenario.CountMatchesAsync(host, null));
    }

    [Fact]
    public async Task Backfill_Canceled_ThrowsAndKeepsTheRowsAsTheyWere()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var stale = await BackfillScenario.SeedAsync(host, null);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await host.Services.GetRequiredService<DatabaseWorkflowBackfill>().RunAsync([null], 2, canceled.Token));

        // 取消在进入存储之前：行原样留着，之后照常回填
        Assert.Equal((0, 0, 0, 0), await BackfillScenario.CountMatchesAsync(host, null));
        Assert.Equal(stale, (await BackfillScenario.RunAsync(host, null, 2)).ValueOrThrow().Rewritten);
    }
}
