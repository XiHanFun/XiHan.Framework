// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Store;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Store;

/// <summary>
/// DefaultJobStore 按截止时间分批清理测试
/// </summary>
public class DefaultJobStoreCleanupTests
{
    private static readonly DateTimeOffset Cutoff = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 早于截止时间的历史被删除，恰等于截止时间与更晚的保留
    /// </summary>
    [Fact]
    public async Task CleanupHistoryAsync_DeletesHistoryStrictlyBeforeCutoff()
    {
        var store = new DefaultJobStore();
        await store.SaveJobHistoryAsync(CreateHistory("before", Cutoff.AddTicks(-1)));
        await store.SaveJobHistoryAsync(CreateHistory("equal", Cutoff));
        await store.SaveJobHistoryAsync(CreateHistory("after", Cutoff.AddMinutes(1)));

        var deleted = await store.CleanupHistoryAsync(Cutoff, 100, TestContext.Current.CancellationToken);

        Assert.Equal(1, deleted);
        var remaining = await store.GetJobHistoryAsync("job");
        Assert.Equal(["after", "equal"], remaining.Select(h => h.HistoryId).ToArray());
    }

    /// <summary>
    /// 已终结且完成时间早于截止时间的实例被删除，恰等于截止时间的保留
    /// </summary>
    [Theory]
    [InlineData(JobStatus.Succeeded)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Canceled)]
    public async Task CleanupHistoryAsync_DeletesTerminalInstancesStrictlyBeforeCutoff(JobStatus status)
    {
        var store = new DefaultJobStore();
        await store.SaveJobInstanceAsync(CreateInstance("before", status, Cutoff.AddTicks(-1)));
        await store.SaveJobInstanceAsync(CreateInstance("equal", status, Cutoff));

        var deleted = await store.CleanupHistoryAsync(Cutoff, 100, TestContext.Current.CancellationToken);

        Assert.Equal(1, deleted);
        Assert.Null(await store.GetJobInstanceAsync("before"));
        Assert.NotNull(await store.GetJobInstanceAsync("equal"));
    }

    /// <summary>
    /// 等待中与运行中的实例不删除
    /// </summary>
    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Running)]
    public async Task CleanupHistoryAsync_KeepsPendingAndRunningInstances(JobStatus status)
    {
        var store = new DefaultJobStore();
        var instance = CreateInstance("active", status, null);
        instance.StartedAt = Cutoff.AddDays(-10);
        instance.ScheduledAt = Cutoff.AddDays(-10);
        await store.SaveJobInstanceAsync(instance);

        var deleted = await store.CleanupHistoryAsync(Cutoff, 100, TestContext.Current.CancellationToken);

        Assert.Equal(0, deleted);
        Assert.NotNull(await store.GetJobInstanceAsync("active"));
    }

    /// <summary>
    /// 每类最多删除批量上限条，返回两类合计
    /// </summary>
    [Fact]
    public async Task CleanupHistoryAsync_DeletesAtMostBatchSizePerCategory()
    {
        var store = new DefaultJobStore();
        for (var i = 0; i < 5; i++)
        {
            await store.SaveJobHistoryAsync(CreateHistory($"h{i}", Cutoff.AddDays(-1 - i)));
            await store.SaveJobInstanceAsync(CreateInstance($"i{i}", JobStatus.Succeeded, Cutoff.AddDays(-1 - i)));
        }

        var first = await store.CleanupHistoryAsync(Cutoff, 2, TestContext.Current.CancellationToken);

        Assert.Equal(4, first);
        Assert.Equal(3, (await store.GetJobHistoryAsync("job", 1, 100)).Count);

        var second = await store.CleanupHistoryAsync(Cutoff, 2, TestContext.Current.CancellationToken);
        var third = await store.CleanupHistoryAsync(Cutoff, 2, TestContext.Current.CancellationToken);
        var fourth = await store.CleanupHistoryAsync(Cutoff, 2, TestContext.Current.CancellationToken);

        Assert.Equal(4, second);
        Assert.Equal(2, third);
        Assert.Equal(0, fourth);
        Assert.Empty(await store.GetJobHistoryAsync("job", 1, 100));
        for (var i = 0; i < 5; i++)
        {
            Assert.Null(await store.GetJobInstanceAsync($"i{i}"));
        }
    }

    /// <summary>
    /// 分批时优先删除最早的历史
    /// </summary>
    [Fact]
    public async Task CleanupHistoryAsync_DeletesOldestHistoryFirst()
    {
        var store = new DefaultJobStore();
        await store.SaveJobHistoryAsync(CreateHistory("newer", Cutoff.AddDays(-1)));
        await store.SaveJobHistoryAsync(CreateHistory("oldest", Cutoff.AddDays(-3)));
        await store.SaveJobHistoryAsync(CreateHistory("older", Cutoff.AddDays(-2)));

        await store.CleanupHistoryAsync(Cutoff, 2, TestContext.Current.CancellationToken);

        var remaining = await store.GetJobHistoryAsync("job");
        Assert.Equal("newer", Assert.Single(remaining).HistoryId);
    }

    /// <summary>
    /// 已取消的令牌立即抛出且不删除任何记录
    /// </summary>
    [Fact]
    public async Task CleanupHistoryAsync_WhenCanceled_ThrowsWithoutDeleting()
    {
        var store = new DefaultJobStore();
        await store.SaveJobHistoryAsync(CreateHistory("h", Cutoff.AddDays(-1)));
        await store.SaveJobInstanceAsync(CreateInstance("i", JobStatus.Succeeded, Cutoff.AddDays(-1)));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.CleanupHistoryAsync(Cutoff, 100, cts.Token));

        Assert.Single(await store.GetJobHistoryAsync("job"));
        Assert.NotNull(await store.GetJobInstanceAsync("i"));
    }

    /// <summary>
    /// 批量上限必须大于 0
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CleanupHistoryAsync_WhenBatchSizeNotPositive_Throws(int batchSize)
    {
        var store = new DefaultJobStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => store.CleanupHistoryAsync(Cutoff, batchSize, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 被清理的实例重新保存为终结状态后仍能被完成追踪接管
    /// </summary>
    [Fact]
    public async Task CleanupHistoryAsync_RemovedInstanceCanBeSavedAgain()
    {
        var store = new DefaultJobStore();
        await store.SaveJobInstanceAsync(CreateInstance("i", JobStatus.Succeeded, Cutoff.AddDays(-1)));
        await store.CleanupHistoryAsync(Cutoff, 100, TestContext.Current.CancellationToken);

        await store.SaveJobInstanceAsync(CreateInstance("i", JobStatus.Succeeded, Cutoff.AddDays(-1)));

        Assert.NotNull(await store.GetJobInstanceAsync("i"));
        Assert.Equal(1, await store.CleanupHistoryAsync(Cutoff, 100, TestContext.Current.CancellationToken));
        Assert.Null(await store.GetJobInstanceAsync("i"));
    }

    private static JobHistory CreateHistory(string id, DateTimeOffset startedAt)
    {
        return new JobHistory
        {
            HistoryId = id,
            InstanceId = id,
            JobName = "job",
            Status = JobStatus.Succeeded,
            StartedAt = startedAt,
            CompletedAt = startedAt,
            IsSuccess = true
        };
    }

    private static JobInstance CreateInstance(string id, JobStatus status, DateTimeOffset? completedAt)
    {
        return new JobInstance
        {
            InstanceId = id,
            JobName = "job",
            JobInfo = new JobInfo { JobName = "job" },
            Status = status,
            ScheduledAt = completedAt ?? Cutoff,
            StartedAt = completedAt,
            CompletedAt = completedAt
        };
    }
}
