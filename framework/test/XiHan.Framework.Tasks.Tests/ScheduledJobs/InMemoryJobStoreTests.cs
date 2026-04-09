#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:InMemoryJobStoreTests
// Guid:11f2fd20-5855-4122-8e5f-929f7c8c1240
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/10 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Store;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs;

/// <summary>
/// InMemoryJobStore 行为测试
/// </summary>
public class InMemoryJobStoreTests
{
    [Fact]
    public async Task CleanupHistory_ShouldRemoveExpiredRecords()
    {
        var store = new InMemoryJobStore();
        var now = DateTimeOffset.UtcNow;

        await store.SaveJobHistoryAsync(CreateHistory("job-a", now.AddDays(-30)));
        await store.SaveJobHistoryAsync(CreateHistory("job-a", now.AddDays(-2)));

        await store.CleanupHistoryAsync(7);

        var histories = await store.GetJobHistoryAsync("job-a", 1, 10);

        Assert.Single(histories);
        Assert.True(histories[0].StartedAt >= now.AddDays(-7));
    }

    [Fact]
    public async Task GetJobHistory_ShouldReturnPagedAndSortedByStartedAtDescending()
    {
        var store = new InMemoryJobStore();
        var now = DateTimeOffset.UtcNow;

        await store.SaveJobHistoryAsync(CreateHistory("job-b", now.AddMinutes(-30)));
        await store.SaveJobHistoryAsync(CreateHistory("job-b", now.AddMinutes(-10)));
        await store.SaveJobHistoryAsync(CreateHistory("job-b", now.AddMinutes(-20)));

        var firstPage = await store.GetJobHistoryAsync("job-b", 1, 2);

        Assert.Equal(2, firstPage.Count);
        Assert.True(firstPage[0].StartedAt >= firstPage[1].StartedAt);
        Assert.Equal(now.AddMinutes(-10).ToUnixTimeSeconds(), firstPage[0].StartedAt.ToUnixTimeSeconds());
        Assert.Equal(now.AddMinutes(-20).ToUnixTimeSeconds(), firstPage[1].StartedAt.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task GetRunningInstances_ShouldOnlyReturnRunningStatus()
    {
        var store = new InMemoryJobStore();

        await store.SaveJobInstanceAsync(CreateInstance("job-c", JobStatus.Running));
        await store.SaveJobInstanceAsync(CreateInstance("job-c", JobStatus.Succeeded));
        await store.SaveJobInstanceAsync(CreateInstance("job-other", JobStatus.Running));

        var running = await store.GetRunningInstancesAsync("job-c");

        Assert.Single(running);
        Assert.Equal(JobStatus.Running, running[0].Status);
        Assert.Equal("job-c", running[0].JobName);
    }

    [Fact]
    public async Task CleanupHistory_ShouldThrowWhenRetentionDaysIsNegative()
    {
        var store = new InMemoryJobStore();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.CleanupHistoryAsync(-1));
    }

    private static JobHistory CreateHistory(string jobName, DateTimeOffset startedAt)
    {
        return new JobHistory
        {
            JobName = jobName,
            StartedAt = startedAt,
            Status = JobStatus.Succeeded,
            IsSuccess = true,
            TriggerType = JobTriggerType.Manual,
            InstanceId = Guid.NewGuid().ToString("N")
        };
    }

    private static JobInstance CreateInstance(string jobName, JobStatus status)
    {
        return new JobInstance
        {
            JobName = jobName,
            Status = status,
            JobInfo = new JobInfo
            {
                JobName = jobName,
                JobType = typeof(object),
                TriggerType = JobTriggerType.Manual
            },
            TriggerType = JobTriggerType.Manual
        };
    }
}
