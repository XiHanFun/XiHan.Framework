// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;
using XiHan.Framework.Tasks.SqlSugar.Entities;

namespace XiHan.Framework.Tasks.SqlSugar.Tests;

/// <summary>
/// 定时任务存储的任务实例测试
/// </summary>
public class JobStoreInstanceTests
{
    /// <summary>
    /// 保存后按标识查回任务实例
    /// </summary>
    [Fact]
    public async Task 保存后按标识查回任务实例()
    {
        using var context = new TasksTestContext();
        var startedAt = DateTimeOffset.UtcNow.AddHours(-1).ToOffset(TimeSpan.FromHours(8));
        var instance = NewInstance("Report.Daily", JobStatus.Running, startedAt);
        instance.TenantId = 42;
        instance.Parameters = new Dictionary<string, object?> { ["orderId"] = 1 };

        await context.JobStore.SaveJobInstanceAsync(instance);
        var found = await context.JobStore.GetJobInstanceAsync(instance.InstanceId);

        Assert.NotNull(found);
        Assert.Equal(instance.InstanceId, found.InstanceId);
        Assert.Equal("Report.Daily", found.JobName);
        Assert.Equal(JobStatus.Running, found.Status);
        Assert.Equal(42L, found.TenantId);
        Assert.Equal(typeof(JobStoreInstanceTests), found.JobInfo.JobType);
        AssertClose(startedAt, found.ScheduledAt);
        Assert.True(found.StartedAt.HasValue);
        AssertClose(startedAt, found.StartedAt.GetValueOrDefault());

        Assert.NotNull(found.Parameters);
        var orderId = Assert.IsType<JsonElement>(found.Parameters["orderId"]);
        Assert.Equal(1, orderId.GetInt32());
    }

    /// <summary>
    /// 查找不存在的实例返回空
    /// </summary>
    [Fact]
    public async Task 查找不存在的实例返回空()
    {
        using var context = new TasksTestContext();

        Assert.Null(await context.JobStore.GetJobInstanceAsync("not-exists"));
    }

    /// <summary>
    /// 保存空实例抛出参数异常
    /// </summary>
    [Fact]
    public async Task 保存空实例抛出参数异常()
    {
        using var context = new TasksTestContext();

        await Assert.ThrowsAsync<ArgumentNullException>(() => context.JobStore.SaveJobInstanceAsync(null!));
    }

    /// <summary>
    /// 重复保存同一实例覆盖原记录
    /// </summary>
    [Fact]
    public async Task 重复保存同一实例覆盖原记录()
    {
        using var context = new TasksTestContext();
        var instance = NewInstance("Report.Daily", JobStatus.Running, DateTimeOffset.UtcNow);
        await context.JobStore.SaveJobInstanceAsync(instance);

        instance.Status = JobStatus.Failed;
        instance.ErrorMessage = "模拟失败。";
        await context.JobStore.SaveJobInstanceAsync(instance);

        Assert.Equal(1, await context.Client.Queryable<SysJobInstance>().CountAsync());

        var found = await context.JobStore.GetJobInstanceAsync(instance.InstanceId);
        Assert.NotNull(found);
        Assert.Equal(JobStatus.Failed, found.Status);
        Assert.Equal("模拟失败。", found.ErrorMessage);
    }

    /// <summary>
    /// 以终止状态保存时补齐完成时间
    /// </summary>
    [Fact]
    public async Task 以终止状态保存时补齐完成时间()
    {
        using var context = new TasksTestContext();
        var instance = NewInstance("Report.Daily", JobStatus.Succeeded, DateTimeOffset.UtcNow.AddMinutes(-1));
        instance.CompletedAt = null;

        await context.JobStore.SaveJobInstanceAsync(instance);

        Assert.True(instance.CompletedAt.HasValue);
        var found = await context.JobStore.GetJobInstanceAsync(instance.InstanceId);
        Assert.NotNull(found);
        Assert.True(found.CompletedAt.HasValue);
    }

    /// <summary>
    /// 更新为终止状态时写入完成时间且不再算运行中
    /// </summary>
    [Fact]
    public async Task 更新为终止状态时写入完成时间且不再算运行中()
    {
        using var context = new TasksTestContext();
        var instance = NewInstance("Report.Daily", JobStatus.Running, DateTimeOffset.UtcNow);
        await context.JobStore.SaveJobInstanceAsync(instance);

        Assert.Single(await context.JobStore.GetRunningInstancesAsync("Report.Daily"));

        await context.JobStore.UpdateJobStatusAsync(instance.InstanceId, JobStatus.Succeeded);

        Assert.Empty(await context.JobStore.GetRunningInstancesAsync("Report.Daily"));
        var found = await context.JobStore.GetJobInstanceAsync(instance.InstanceId);
        Assert.NotNull(found);
        Assert.Equal(JobStatus.Succeeded, found.Status);
        Assert.True(found.CompletedAt.HasValue);
    }

    /// <summary>
    /// 更新为终止状态时不覆盖已有的完成时间
    /// </summary>
    [Fact]
    public async Task 更新为终止状态时不覆盖已有的完成时间()
    {
        using var context = new TasksTestContext();
        var completedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var instance = NewInstance("Report.Daily", JobStatus.Running, completedAt.AddMinutes(-5));
        instance.CompletedAt = completedAt;
        await context.JobStore.SaveJobInstanceAsync(instance);

        await context.JobStore.UpdateJobStatusAsync(instance.InstanceId, JobStatus.Failed);

        var found = await context.JobStore.GetJobInstanceAsync(instance.InstanceId);
        Assert.NotNull(found);
        Assert.Equal(JobStatus.Failed, found.Status);
        Assert.True(found.CompletedAt.HasValue);
        AssertClose(completedAt, found.CompletedAt.GetValueOrDefault());
    }

    /// <summary>
    /// 更新不存在的实例不抛异常且不插入
    /// </summary>
    [Fact]
    public async Task 更新不存在的实例不抛异常且不插入()
    {
        using var context = new TasksTestContext();

        await context.JobStore.UpdateJobStatusAsync("not-exists", JobStatus.Failed);

        Assert.Equal(0, await context.Client.Queryable<SysJobInstance>().CountAsync());
    }

    /// <summary>
    /// 只返回该任务运行中的实例
    /// </summary>
    [Fact]
    public async Task 只返回该任务运行中的实例()
    {
        using var context = new TasksTestContext();
        var now = DateTimeOffset.UtcNow;
        var running = NewInstance("Report.Daily", JobStatus.Running, now);
        var succeeded = NewInstance("Report.Daily", JobStatus.Succeeded, now);
        var otherJob = NewInstance("Report.Weekly", JobStatus.Running, now);

        foreach (var instance in new[] { running, succeeded, otherJob })
        {
            await context.JobStore.SaveJobInstanceAsync(instance);
        }

        var result = await context.JobStore.GetRunningInstancesAsync("Report.Daily");

        var single = Assert.Single(result);
        Assert.Equal(running.InstanceId, single.InstanceId);
    }

    /// <summary>
    /// 超过截止时刻的运行中实例不再算运行中
    /// </summary>
    [Fact]
    public async Task 超过截止时刻的运行中实例不再算运行中()
    {
        using var context = new TasksTestContext();
        var stale = NewInstance("Report.Daily", JobStatus.Running, DateTimeOffset.UtcNow.AddHours(-2), timeoutMilliseconds: 1000);
        var active = NewInstance("Report.Daily", JobStatus.Running, DateTimeOffset.UtcNow, timeoutMilliseconds: 300000);
        await context.JobStore.SaveJobInstanceAsync(stale);
        await context.JobStore.SaveJobInstanceAsync(active);

        var result = await context.JobStore.GetRunningInstancesAsync("Report.Daily");

        var single = Assert.Single(result);
        Assert.Equal(active.InstanceId, single.InstanceId);

        var staleFound = await context.JobStore.GetJobInstanceAsync(stale.InstanceId);
        Assert.NotNull(staleFound);
        Assert.Equal(JobStatus.Running, staleFound.Status);
    }

    /// <summary>
    /// 超时关闭的非并发任务在宽限期后仍算运行中且调度器不再触发第二份
    /// </summary>
    [Fact]
    public async Task 超时关闭的非并发任务在宽限期后仍算运行中且调度器不再触发第二份()
    {
        using var context = new TasksTestContext();
        var jobInfo = new JobInfo
        {
            JobName = "Report.NoTimeout",
            JobType = typeof(JobStoreInstanceTests),
            TriggerType = JobTriggerType.Manual,
            AllowConcurrent = false,
            TimeoutMilliseconds = 0
        };
        var startedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var running = new JobInstance
        {
            JobName = jobInfo.JobName,
            JobInfo = jobInfo,
            Status = JobStatus.Running,
            ScheduledAt = startedAt,
            StartedAt = startedAt,
            TriggerType = JobTriggerType.Manual
        };
        await context.JobStore.SaveJobInstanceAsync(running);

        var stillRunning = Assert.Single(await context.JobStore.GetRunningInstancesAsync(jobInfo.JobName));
        Assert.Equal(running.InstanceId, stillRunning.InstanceId);

        var executor = new RecordingJobExecutor();
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var scheduler = new CompositeJobScheduler(
            executor,
            NullLogger<CompositeJobScheduler>.Instance,
            context.JobStore,
            serviceProvider);
        scheduler.RegisterJob(jobInfo);

        var triggeredInstanceId = await scheduler.TriggerJobAsync(jobInfo.JobName);

        Assert.Equal(string.Empty, triggeredInstanceId);
        Assert.Equal(0, executor.CallCount);
    }

    /// <summary>
    /// 任务名为空白时抛出参数异常
    /// </summary>
    [Fact]
    public async Task 任务名为空白时抛出参数异常()
    {
        using var context = new TasksTestContext();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => context.JobStore.GetRunningInstancesAsync(" "));
    }

    /// <summary>
    /// 在租户上下文中保存时以宿主上下文写库
    /// </summary>
    [Fact]
    public async Task 在租户上下文中保存时以宿主上下文写库()
    {
        using var context = new TasksTestContext();
        var instance = NewInstance("Report.Daily", JobStatus.Running, DateTimeOffset.UtcNow);
        instance.TenantId = 42;

        using (context.Tenant.Change(42))
        {
            await context.JobStore.SaveJobInstanceAsync(instance);
        }

        Assert.NotEmpty(context.ExecutingTenantIds);
        Assert.All(context.ExecutingTenantIds, tenantId => Assert.Null(tenantId));

        var stored = await context.Client.Queryable<SysJobInstance>().FirstAsync();
        Assert.Equal(42L, stored.TenantId);
    }

    private static JobInstance NewInstance(
        string jobName,
        JobStatus status,
        DateTimeOffset startedAt,
        int timeoutMilliseconds = 300000)
    {
        return new JobInstance
        {
            JobName = jobName,
            JobInfo = new JobInfo
            {
                JobName = jobName,
                JobType = typeof(JobStoreInstanceTests),
                TimeoutMilliseconds = timeoutMilliseconds
            },
            Status = status,
            ScheduledAt = startedAt,
            StartedAt = startedAt,
            TriggerType = JobTriggerType.Cron
        };
    }

    private static void AssertClose(DateTimeOffset expected, DateTimeOffset actual)
    {
        Assert.True(
            Math.Abs((expected - actual).TotalSeconds) < 1,
            $"期望 {expected:O}，实际 {actual:O}");
    }

    /// <summary>
    /// 记录调用次数的任务执行器
    /// </summary>
    private sealed class RecordingJobExecutor : IJobExecutor
    {
        private int _callCount;

        /// <summary>
        /// 被调用的次数
        /// </summary>
        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>
        /// 记录一次调用并返回成功
        /// </summary>
        public Task<JobResult> ExecuteAsync(
            JobInstance jobInstance,
            IDictionary<string, object?>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(JobResult.Success());
        }
    }
}
