// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 定时任务存储的分批清理测试
/// </summary>
public class JobStoreBatchCleanupTests
{
    private const string JobName = "Report.Daily";

    private static readonly DateTimeOffset Cutoff = new(TasksTestContext.BaseTime);

    /// <summary>
    /// 早于截止时间的历史与已结束实例被删除，恰等于截止时间的保留
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 早于截止时间的记录被删除且恰等于截止时间的保留()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        var oldHistory = NewHistory(Cutoff.AddSeconds(-1));
        var boundaryHistory = NewHistory(Cutoff);
        await context.JobStore.SaveJobHistoryAsync(oldHistory);
        await context.JobStore.SaveJobHistoryAsync(boundaryHistory);

        var oldInstance = NewTerminalInstance(Cutoff.AddSeconds(-1));
        var boundaryInstance = NewTerminalInstance(Cutoff);
        await context.JobStore.SaveJobInstanceAsync(oldInstance);
        await context.JobStore.SaveJobInstanceAsync(boundaryInstance);

        var deleted = await context.JobStore.CleanupHistoryAsync(Cutoff, 100, cancellationToken);

        Assert.Equal(2, deleted);
        var remaining = Assert.Single(await context.JobStore.GetJobHistoryAsync(JobName, 1, 100));
        Assert.Equal(boundaryHistory.HistoryId, remaining.HistoryId);
        Assert.Null(await context.JobStore.GetJobInstanceAsync(oldInstance.InstanceId));
        Assert.NotNull(await context.JobStore.GetJobInstanceAsync(boundaryInstance.InstanceId));
    }

    /// <summary>
    /// 每类记录单批最多删除批量上限条
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 每类记录单批最多删除批量上限条()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var index = 1; index <= 5; index++)
        {
            await context.JobStore.SaveJobHistoryAsync(NewHistory(Cutoff.AddMinutes(-index)));
            await context.JobStore.SaveJobInstanceAsync(NewTerminalInstance(Cutoff.AddMinutes(-index)));
        }

        var first = await context.JobStore.CleanupHistoryAsync(Cutoff, 2, cancellationToken);

        Assert.Equal(4, first);
        Assert.Equal(3, (await context.JobStore.GetJobHistoryAsync(JobName, 1, 100)).Count);
        Assert.Equal(3, await CountInstancesAsync(context));

        var second = await context.JobStore.CleanupHistoryAsync(Cutoff, 2, cancellationToken);
        var third = await context.JobStore.CleanupHistoryAsync(Cutoff, 2, cancellationToken);
        var fourth = await context.JobStore.CleanupHistoryAsync(Cutoff, 2, cancellationToken);

        Assert.Equal(4, second);
        Assert.Equal(2, third);
        Assert.Equal(0, fourth);
        Assert.Empty(await context.JobStore.GetJobHistoryAsync(JobName, 1, 100));
        Assert.Equal(0, await CountInstancesAsync(context));
    }

    /// <summary>
    /// 等待中、暂停与运行截止时刻未早于截止时间的运行中实例不删除
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 等待中暂停与未过截止时刻的运行中实例不删除()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        var pending = NewInstance(JobStatus.Pending, Cutoff.AddDays(-10));
        var paused = NewInstance(JobStatus.Paused, Cutoff.AddDays(-10));
        await context.JobStore.SaveJobInstanceAsync(pending);
        await context.JobStore.SaveJobInstanceAsync(paused);
        var running = await InsertRunningInstanceAsync(context, Cutoff.AddMinutes(1));
        var boundaryRunning = await InsertRunningInstanceAsync(context, Cutoff);

        var deleted = await context.JobStore.CleanupHistoryAsync(Cutoff, 100, cancellationToken);

        Assert.Equal(0, deleted);
        Assert.NotNull(await context.JobStore.GetJobInstanceAsync(pending.InstanceId));
        Assert.NotNull(await context.JobStore.GetJobInstanceAsync(paused.InstanceId));
        Assert.NotNull(await context.JobStore.GetJobInstanceAsync(running));
        Assert.NotNull(await context.JobStore.GetJobInstanceAsync(boundaryRunning));
    }

    /// <summary>
    /// 运行截止时刻早于截止时间的遗留运行中实例被删除
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 运行截止时刻早于截止时间的遗留运行中实例被删除()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        var legacy = NewInstance(JobStatus.Running, Cutoff.AddDays(-1));
        await context.JobStore.SaveJobInstanceAsync(legacy);

        var deleted = await context.JobStore.CleanupHistoryAsync(Cutoff, 100, cancellationToken);

        Assert.Equal(1, deleted);
        Assert.Null(await context.JobStore.GetJobInstanceAsync(legacy.InstanceId));
    }

    /// <summary>
    /// 取消令牌已取消时抛出且不删除任何记录
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 取消令牌已取消时抛出且不删除记录()
    {
        using var context = new TasksTestContext();
        var history = NewHistory(Cutoff.AddDays(-1));
        await context.JobStore.SaveJobHistoryAsync(history);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.JobStore.CleanupHistoryAsync(Cutoff, 100, cancellation.Token));

        Assert.Single(await context.JobStore.GetJobHistoryAsync(JobName, 1, 100));
    }

    /// <summary>
    /// 批量上限不大于 0 时抛出参数异常
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task 批量上限不大于零时抛出参数异常()
    {
        using var context = new TasksTestContext();
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => context.JobStore.CleanupHistoryAsync(Cutoff, 0, cancellationToken));
    }

    private static async Task<string> InsertRunningInstanceAsync(TasksTestContext context, DateTimeOffset runningDeadline)
    {
        var instanceId = Guid.NewGuid().ToString("N");
        var startedAt = Cutoff.AddDays(-1).UtcDateTime;
        await context.Client.Insertable(new Entities.SysJobInstance(instanceId)
        {
            JobName = JobName,
            Status = (int)JobStatus.Running,
            TriggerType = (int)JobTriggerType.Cron,
            ScheduledAt = startedAt,
            StartedAt = startedAt,
            RunningDeadline = runningDeadline.UtcDateTime
        }).ExecuteCommandAsync();

        return instanceId;
    }

    private static Task<int> CountInstancesAsync(TasksTestContext context)
    {
        return context.Client.Queryable<Entities.SysJobInstance>().CountAsync();
    }

    private static JobHistory NewHistory(DateTimeOffset startedAt)
    {
        return new JobHistory
        {
            InstanceId = Guid.NewGuid().ToString("N"),
            JobName = JobName,
            Status = JobStatus.Succeeded,
            StartedAt = startedAt,
            CompletedAt = startedAt,
            TriggerType = JobTriggerType.Cron
        };
    }

    private static JobInstance NewTerminalInstance(DateTimeOffset completedAt)
    {
        var instance = NewInstance(JobStatus.Succeeded, completedAt.AddSeconds(-1));
        instance.CompletedAt = completedAt;
        return instance;
    }

    private static JobInstance NewInstance(JobStatus status, DateTimeOffset startedAt)
    {
        return new JobInstance
        {
            JobName = JobName,
            JobInfo = new JobInfo { JobName = JobName },
            Status = status,
            ScheduledAt = startedAt,
            StartedAt = startedAt,
            TriggerType = JobTriggerType.Cron
        };
    }
}
