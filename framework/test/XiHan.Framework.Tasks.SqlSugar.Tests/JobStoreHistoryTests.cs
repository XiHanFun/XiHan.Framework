// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 定时任务存储的执行历史测试
/// </summary>
public class JobStoreHistoryTests
{
    /// <summary>
    /// 按开始时间倒序分页返回执行历史
    /// </summary>
    [Fact]
    public async Task 按开始时间倒序分页返回执行历史()
    {
        using var context = new TasksTestContext();
        var baseTime = DateTimeOffset.UtcNow.AddDays(-1);
        var histories = Enumerable.Range(0, 5)
            .Select(index => NewHistory("Report.Daily", baseTime.AddHours(-index)))
            .ToList();

        for (var index = histories.Count - 1; index >= 0; index--)
        {
            await context.JobStore.SaveJobHistoryAsync(histories[index]);
        }

        await context.JobStore.SaveJobHistoryAsync(NewHistory("Report.Weekly", baseTime));

        var page1 = await context.JobStore.GetJobHistoryAsync("Report.Daily", 1, 2);
        var page2 = await context.JobStore.GetJobHistoryAsync("Report.Daily", 2, 2);
        var page3 = await context.JobStore.GetJobHistoryAsync("Report.Daily", 3, 2);

        string[] expected1 = [histories[0].HistoryId, histories[1].HistoryId];
        string[] expected2 = [histories[2].HistoryId, histories[3].HistoryId];
        Assert.Equal(expected1, page1.Select(history => history.HistoryId));
        Assert.Equal(expected2, page2.Select(history => history.HistoryId));
        var last = Assert.Single(page3);
        Assert.Equal(histories[4].HistoryId, last.HistoryId);
    }

    /// <summary>
    /// 历史标识为空时自动生成
    /// </summary>
    [Fact]
    public async Task 历史标识为空时自动生成()
    {
        using var context = new TasksTestContext();
        var history = NewHistory("Report.Daily", DateTimeOffset.UtcNow.AddHours(-1));
        history.HistoryId = string.Empty;
        history.IsSuccess = true;
        history.Remarks = "手动触发";

        await context.JobStore.SaveJobHistoryAsync(history);

        Assert.False(string.IsNullOrWhiteSpace(history.HistoryId));
        var single = Assert.Single(await context.JobStore.GetJobHistoryAsync("Report.Daily"));
        Assert.Equal(history.HistoryId, single.HistoryId);
        Assert.True(single.IsSuccess);
        Assert.Equal("手动触发", single.Remarks);
    }

    /// <summary>
    /// 保存空历史抛出参数异常
    /// </summary>
    [Fact]
    public async Task 保存空历史抛出参数异常()
    {
        using var context = new TasksTestContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() => context.JobStore.SaveJobHistoryAsync(null!));
    }

    /// <summary>
    /// 查询参数非法时抛出参数异常
    /// </summary>
    [Fact]
    public async Task 查询参数非法时抛出参数异常()
    {
        using var context = new TasksTestContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.JobStore.GetJobHistoryAsync("Report.Daily", 0, 20));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.JobStore.GetJobHistoryAsync("Report.Daily", 1, 0));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => context.JobStore.GetJobHistoryAsync(" "));
    }

    /// <summary>
    /// 清理早于保留期的历史、已结束实例与截止时刻早于保留期的遗留运行实例
    /// </summary>
    [Fact]
    public async Task 清理早于保留期的历史与已结束实例及遗留运行实例()
    {
        using var context = new TasksTestContext();
        var now = DateTimeOffset.UtcNow;

        var oldHistory = NewHistory("Report.Daily", now.AddDays(-40));
        var recentHistory = NewHistory("Report.Daily", now.AddDays(-1));
        await context.JobStore.SaveJobHistoryAsync(oldHistory);
        await context.JobStore.SaveJobHistoryAsync(recentHistory);

        var oldSucceeded = NewInstance("Report.Daily", JobStatus.Succeeded, now.AddDays(-40));
        oldSucceeded.CompletedAt = now.AddDays(-40);
        var oldRunning = NewInstance("Report.Daily", JobStatus.Running, now.AddDays(-40));
        var recentSucceeded = NewInstance("Report.Daily", JobStatus.Succeeded, now.AddDays(-1));
        recentSucceeded.CompletedAt = now.AddDays(-1);
        var recentRunning = NewInstance("Report.Daily", JobStatus.Running, now);

        foreach (var instance in new[] { oldSucceeded, oldRunning, recentSucceeded, recentRunning })
        {
            await context.JobStore.SaveJobInstanceAsync(instance);
        }

        await context.JobStore.CleanupHistoryAsync(30);

        var remaining = Assert.Single(await context.JobStore.GetJobHistoryAsync("Report.Daily"));
        Assert.Equal(recentHistory.HistoryId, remaining.HistoryId);
        Assert.Null(await context.JobStore.GetJobInstanceAsync(oldSucceeded.InstanceId));
        Assert.Null(await context.JobStore.GetJobInstanceAsync(oldRunning.InstanceId));
        Assert.NotNull(await context.JobStore.GetJobInstanceAsync(recentSucceeded.InstanceId));
        Assert.NotNull(await context.JobStore.GetJobInstanceAsync(recentRunning.InstanceId));
    }

    /// <summary>
    /// 保留天数为负时抛出参数异常
    /// </summary>
    [Fact]
    public async Task 保留天数为负时抛出参数异常()
    {
        using var context = new TasksTestContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.JobStore.CleanupHistoryAsync(-1));
    }

    private static JobHistory NewHistory(string jobName, DateTimeOffset startedAt)
    {
        return new JobHistory
        {
            InstanceId = Guid.NewGuid().ToString("N"),
            JobName = jobName,
            Status = JobStatus.Succeeded,
            StartedAt = startedAt,
            CompletedAt = startedAt.AddSeconds(1),
            TriggerType = JobTriggerType.Cron
        };
    }

    private static JobInstance NewInstance(string jobName, JobStatus status, DateTimeOffset startedAt)
    {
        return new JobInstance
        {
            JobName = jobName,
            JobInfo = new JobInfo { JobName = jobName },
            Status = status,
            ScheduledAt = startedAt,
            StartedAt = startedAt,
            TriggerType = JobTriggerType.Cron
        };
    }
}
