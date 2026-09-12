// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Store;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs;

/// <summary>
/// DefaultJobStore 行为测试
/// </summary>
public class DefaultJobStoreTests
{
    [Fact]
    public async Task CleanupHistory_ShouldRemoveExpiredRecords()
    {
        var store = new DefaultJobStore();
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
        var store = new DefaultJobStore();
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
        var store = new DefaultJobStore();

        await store.SaveJobInstanceAsync(CreateInstance("job-c", JobStatus.Running));
        await store.SaveJobInstanceAsync(CreateInstance("job-c", JobStatus.Succeeded));
        await store.SaveJobInstanceAsync(CreateInstance("job-other", JobStatus.Running));

        var running = await store.GetRunningInstancesAsync("job-c");

        Assert.Single(running);
        Assert.Equal(JobStatus.Running, running[0].Status);
        Assert.Equal("job-c", running[0].JobName);
    }

    [Theory]
    [InlineData(JobStatus.Succeeded)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.Canceled)]
    public async Task UpdateJobStatus_WithTerminalStatus_RetainsRecentRuntimeInstance(JobStatus status)
    {
        var store = new DefaultJobStore();
        var instance = CreateInstance("job-terminal", JobStatus.Running);
        await store.SaveJobInstanceAsync(instance);

        await store.UpdateJobStatusAsync(instance.InstanceId, status);

        Assert.Equal(status, instance.Status);
        Assert.NotNull(instance.CompletedAt);
        Assert.Same(instance, await store.GetJobInstanceAsync(instance.InstanceId));
    }

    [Fact]
    public async Task UpdateJobStatus_WhenCompletedInstanceLimitExceeded_EvictsOldestInstance()
    {
        const int maxCompletedInstanceCount = 10000;
        var store = new DefaultJobStore();
        JobInstance? oldest = null;
        JobInstance? newest = null;

        for (var index = 0; index <= maxCompletedInstanceCount; index++)
        {
            var instance = CreateInstance("job-capped", JobStatus.Running);
            oldest ??= instance;
            newest = instance;
            await store.SaveJobInstanceAsync(instance);
            await store.UpdateJobStatusAsync(instance.InstanceId, JobStatus.Succeeded);
        }

        Assert.Null(await store.GetJobInstanceAsync(oldest!.InstanceId));
        Assert.Same(newest, await store.GetJobInstanceAsync(newest!.InstanceId));
    }

    [Fact]
    public async Task CleanupHistory_ShouldThrowWhenRetentionDaysIsNegative()
    {
        var store = new DefaultJobStore();

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
