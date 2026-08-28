// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Attributes;
using XiHan.Framework.Tasks.ScheduledJobs.Extensions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Extensions;

/// <summary>
/// JobSchedulerExtensions 注册扩展测试
/// </summary>
/// <remarks>
/// 程序集扫描的断言一律用"包含"语义而不是精确数量：测试程序集里还有别处定义的任务体假实现，
/// 数量断言会随其他用例的增减而失真。特性到 JobInfo 的映射则逐字段精确断言。
/// </remarks>
public class JobSchedulerExtensionsTests
{
    /// <summary>
    /// 调度器为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void RegisterJobsFromAssembly_WhenSchedulerIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => JobSchedulerExtensions.RegisterJobsFromAssembly(null!, typeof(JobSchedulerExtensionsTests).Assembly));
    }

    /// <summary>
    /// 程序集为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void RegisterJobsFromAssembly_WhenAssemblyIsNull_ThrowsArgumentNullException()
    {
        var scheduler = new RecordingJobScheduler();

        Assert.Throws<ArgumentNullException>(() => scheduler.RegisterJobsFromAssembly(null!));
    }

    /// <summary>
    /// 扫描时只收带任务名特性的任务体，没有特性的被跳过
    /// </summary>
    [Fact]
    public void RegisterJobsFromAssembly_OnlyPicksTypesWithJobNameAttribute()
    {
        var scheduler = new RecordingJobScheduler();

        scheduler.RegisterJobsFromAssembly(typeof(JobSchedulerExtensionsTests).Assembly);

        Assert.Contains(scheduler.Registered, job => job.JobName == "decorated-cron-job");
        Assert.DoesNotContain(scheduler.Registered, job => job.JobType == typeof(UndecoratedWorker));
    }

    /// <summary>
    /// 扫描时把各特性逐一映射到任务定义字段上
    /// </summary>
    [Fact]
    public void RegisterJobsFromAssembly_MapsEveryAttributeToJobInfo()
    {
        var scheduler = new RecordingJobScheduler();

        scheduler.RegisterJobsFromAssembly(typeof(JobSchedulerExtensionsTests).Assembly);

        var jobInfo = Assert.Single(scheduler.Registered, job => job.JobName == "decorated-cron-job");
        Assert.Equal(typeof(DecoratedCronWorker), jobInfo.JobType);
        Assert.Equal("被完整装饰的任务", jobInfo.Description);
        Assert.Equal(JobTriggerType.Cron, jobInfo.TriggerType);
        Assert.Equal("0 2 * * *", jobInfo.CronExpression);
        Assert.Equal(1234, jobInfo.TimeoutMilliseconds);
        Assert.False(jobInfo.AllowConcurrent);
        Assert.Equal(JobPriority.Critical, jobInfo.Priority);
        Assert.Equal(5, jobInfo.RetryPolicy.MaxRetryCount);
        Assert.Equal(250, jobInfo.RetryPolicy.RetryIntervalMilliseconds);
        Assert.False(jobInfo.RetryPolicy.UseExponentialBackoff);
    }

    /// <summary>
    /// 间隔与延时特性按秒换算为 TimeSpan
    /// </summary>
    [Fact]
    public void RegisterJobsFromAssembly_ConvertsIntervalAndDelaySecondsToTimeSpan()
    {
        var scheduler = new RecordingJobScheduler();

        scheduler.RegisterJobsFromAssembly(typeof(JobSchedulerExtensionsTests).Assembly);

        var jobInfo = Assert.Single(scheduler.Registered, job => job.JobName == "decorated-interval-job");
        Assert.Equal(JobTriggerType.Interval, jobInfo.TriggerType);
        Assert.Equal(TimeSpan.FromSeconds(45), jobInfo.Interval);
        Assert.Equal(TimeSpan.FromSeconds(10), jobInfo.Delay);
        Assert.Null(jobInfo.CronExpression);
    }

    /// <summary>
    /// 只带任务名特性时其余字段保持任务定义的默认值
    /// </summary>
    [Fact]
    public void RegisterJobsFromAssembly_WithNameOnly_KeepsJobInfoDefaults()
    {
        var scheduler = new RecordingJobScheduler();

        scheduler.RegisterJobsFromAssembly(typeof(JobSchedulerExtensionsTests).Assembly);

        var jobInfo = Assert.Single(scheduler.Registered, job => job.JobName == "name-only-job");
        Assert.Null(jobInfo.Description);
        Assert.Null(jobInfo.CronExpression);
        Assert.Null(jobInfo.Interval);
        Assert.True(jobInfo.AllowConcurrent);
        Assert.Equal(300000, jobInfo.TimeoutMilliseconds);
        Assert.Equal(JobPriority.Normal, jobInfo.Priority);
        Assert.Equal(3, jobInfo.RetryPolicy.MaxRetryCount);
    }

    /// <summary>
    /// 注册 Cron 任务时按入参组装任务定义
    /// </summary>
    [Fact]
    public void RegisterCronJob_BuildsCronJobInfoFromArguments()
    {
        var scheduler = new RecordingJobScheduler();

        scheduler.RegisterCronJob<UndecoratedWorker>("nightly", "0 2 * * *", "每晚跑批", JobPriority.High);

        var jobInfo = Assert.Single(scheduler.Registered);
        Assert.Equal("nightly", jobInfo.JobName);
        Assert.Equal("每晚跑批", jobInfo.Description);
        Assert.Equal(typeof(UndecoratedWorker), jobInfo.JobType);
        Assert.Equal(JobTriggerType.Cron, jobInfo.TriggerType);
        Assert.Equal("0 2 * * *", jobInfo.CronExpression);
        Assert.Equal(JobPriority.High, jobInfo.Priority);
    }

    /// <summary>
    /// 注册 Cron 任务时描述与优先级有默认值
    /// </summary>
    [Fact]
    public void RegisterCronJob_WithoutOptionalArguments_UsesDefaults()
    {
        var scheduler = new RecordingJobScheduler();

        scheduler.RegisterCronJob<UndecoratedWorker>("nightly", "0 2 * * *");

        var jobInfo = Assert.Single(scheduler.Registered);
        Assert.Null(jobInfo.Description);
        Assert.Equal(JobPriority.Normal, jobInfo.Priority);
    }

    /// <summary>
    /// 注册间隔任务时按入参组装任务定义
    /// </summary>
    [Fact]
    public void RegisterIntervalJob_BuildsIntervalJobInfoFromArguments()
    {
        var scheduler = new RecordingJobScheduler();

        scheduler.RegisterIntervalJob<UndecoratedWorker>("heartbeat", TimeSpan.FromMinutes(2), "心跳", JobPriority.Low);

        var jobInfo = Assert.Single(scheduler.Registered);
        Assert.Equal("heartbeat", jobInfo.JobName);
        Assert.Equal("心跳", jobInfo.Description);
        Assert.Equal(typeof(UndecoratedWorker), jobInfo.JobType);
        Assert.Equal(JobTriggerType.Interval, jobInfo.TriggerType);
        Assert.Equal(TimeSpan.FromMinutes(2), jobInfo.Interval);
        Assert.Equal(JobPriority.Low, jobInfo.Priority);
        Assert.Null(jobInfo.CronExpression);
    }

    /// <summary>
    /// 注册间隔任务时描述与优先级有默认值
    /// </summary>
    [Fact]
    public void RegisterIntervalJob_WithoutOptionalArguments_UsesDefaults()
    {
        var scheduler = new RecordingJobScheduler();

        scheduler.RegisterIntervalJob<UndecoratedWorker>("heartbeat", TimeSpan.FromMinutes(2));

        var jobInfo = Assert.Single(scheduler.Registered);
        Assert.Null(jobInfo.Description);
        Assert.Equal(JobPriority.Normal, jobInfo.Priority);
    }

    /// <summary>
    /// 带完整特性装饰的 Cron 任务体
    /// </summary>
    [JobName("decorated-cron-job")]
    [JobDescription("被完整装饰的任务")]
    [JobSchedule("0 2 * * *")]
    [JobRetry(MaxRetryCount = 5, RetryIntervalMilliseconds = 250, UseExponentialBackoff = false)]
    [JobConcurrent(false)]
    [JobTimeout(1234)]
    [JobPriorityAttribute(JobPriority.Critical)]
    public sealed class DecoratedCronWorker : IJobWorker
    {
        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(JobResult.Success());
        }
    }

    /// <summary>
    /// 带间隔与延时配置的任务体
    /// </summary>
    [JobName("decorated-interval-job")]
    [JobSchedule(45, DelaySeconds = 10)]
    public sealed class DecoratedIntervalWorker : IJobWorker
    {
        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(JobResult.Success());
        }
    }

    /// <summary>
    /// 只带任务名特性的任务体
    /// </summary>
    [JobName("name-only-job")]
    public sealed class NameOnlyWorker : IJobWorker
    {
        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(JobResult.Success());
        }
    }

    /// <summary>
    /// 完全没有特性的任务体，扫描时应被跳过
    /// </summary>
    public sealed class UndecoratedWorker : IJobWorker
    {
        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(JobResult.Success());
        }
    }

    /// <summary>
    /// 只记录注册结果的假调度器
    /// </summary>
    private sealed class RecordingJobScheduler : IJobScheduler
    {
        /// <summary>
        /// 已注册的任务定义
        /// </summary>
        public List<JobInfo> Registered { get; } = [];

        /// <summary>
        /// 注册任务
        /// </summary>
        public void RegisterJob(JobInfo jobInfo)
        {
            Registered.Add(jobInfo);
        }

        /// <summary>
        /// 取消注册任务
        /// </summary>
        public void UnregisterJob(string jobName)
        {
            Registered.RemoveAll(job => job.JobName == jobName);
        }

        /// <summary>
        /// 暂停任务
        /// </summary>
        public void PauseJob(string jobName)
        {
        }

        /// <summary>
        /// 恢复任务
        /// </summary>
        public void ResumeJob(string jobName)
        {
        }

        /// <summary>
        /// 手动触发任务
        /// </summary>
        public Task<string> TriggerJobAsync(string jobName, IDictionary<string, object?>? parameters = null)
        {
            return Task.FromResult(string.Empty);
        }

        /// <summary>
        /// 获取下次执行时间
        /// </summary>
        public DateTimeOffset? GetNextFireTime(string jobName)
        {
            return null;
        }

        /// <summary>
        /// 获取所有已注册的任务信息
        /// </summary>
        public IReadOnlyList<JobInfo> GetAllJobs()
        {
            return Registered;
        }

        /// <summary>
        /// 启动调度器
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 停止调度器
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
