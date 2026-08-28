// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Scheduler;

/// <summary>
/// CompositeJobScheduler 复合调度器测试
/// </summary>
/// <remarks>
/// 调度器的执行是"记录触发 → 重排下次 → 后台执行"三步，其中后台执行是 fire-and-forget，
/// 所以这里用带 TaskCompletionSource 的假执行器做事件驱动等待，绝不用固定时长的 Sleep 去碰运气。
/// 并发控制（AllowConcurrent=false 时的重入保护）通过假存储直接给出"已有运行中实例"来构造，
/// 不需要真的并发跑两遍任务。
/// </remarks>
public class CompositeJobSchedulerTests
{
    /// <summary>
    /// 兜底超时：涉及定时器与后台执行的用例上限
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 事件驱动等待的最长时间
    /// </summary>
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 构造函数对每个依赖都做非空校验
    /// </summary>
    [Fact]
    public void Constructor_WhenAnyDependencyIsNull_ThrowsArgumentNullException()
    {
        var executor = new RecordingJobExecutor();
        var logger = NullLogger<CompositeJobScheduler>.Instance;
        var store = new StubJobStore();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() => new CompositeJobScheduler(null!, logger, store, serviceProvider));
        Assert.Throws<ArgumentNullException>(() => new CompositeJobScheduler(executor, null!, store, serviceProvider));
        Assert.Throws<ArgumentNullException>(() => new CompositeJobScheduler(executor, logger, null!, serviceProvider));
        Assert.Throws<ArgumentNullException>(() => new CompositeJobScheduler(executor, logger, store, null!));
    }

    /// <summary>
    /// 注册 null 任务定义时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void RegisterJob_WhenJobInfoIsNull_ThrowsArgumentNullException()
    {
        var scheduler = CreateScheduler(out _, out _);

        Assert.Throws<ArgumentNullException>(() => scheduler.RegisterJob(null!));
    }

    /// <summary>
    /// 注册无名任务时抛出 ArgumentException
    /// </summary>
    [Fact]
    public void RegisterJob_WhenJobNameIsBlank_ThrowsArgumentException()
    {
        var scheduler = CreateScheduler(out _, out _);

        Assert.Throws<ArgumentException>(() => scheduler.RegisterJob(CreateJob(string.Empty)));
    }

    /// <summary>
    /// 注册后可在任务列表中查到，同名重复注册只保留一条
    /// </summary>
    [Fact]
    public void RegisterJob_WithSameName_ReplacesInsteadOfDuplicating()
    {
        var scheduler = CreateScheduler(out _, out _);

        scheduler.RegisterJob(CreateJob("job-a"));
        scheduler.RegisterJob(CreateJob("job-a"));
        scheduler.RegisterJob(CreateJob("job-b"));

        var jobs = scheduler.GetAllJobs();
        Assert.Equal(2, jobs.Count);
        Assert.Contains(jobs, job => job.JobName == "job-a");
        Assert.Contains(jobs, job => job.JobName == "job-b");
    }

    /// <summary>
    /// 间隔任务注册后立即排出"当前时刻 + 间隔"的下次触发时间
    /// </summary>
    [Fact]
    public void RegisterJob_WithIntervalTrigger_SchedulesNowPlusInterval()
    {
        var scheduler = CreateScheduler(out _, out _);
        var job = CreateJob("interval-job");
        job.TriggerType = JobTriggerType.Interval;
        job.Interval = TimeSpan.FromHours(1);

        var before = DateTimeOffset.UtcNow;
        scheduler.RegisterJob(job);
        var after = DateTimeOffset.UtcNow;

        var next = scheduler.GetNextFireTime("interval-job");
        Assert.NotNull(next);
        Assert.InRange(next!.Value, before.AddHours(1), after.AddHours(1));
    }

    /// <summary>
    /// 延时任务注册后排出"当前时刻 + 延时"的一次性触发时间
    /// </summary>
    [Fact]
    public void RegisterJob_WithDelayTrigger_SchedulesNowPlusDelay()
    {
        var scheduler = CreateScheduler(out _, out _);
        var job = CreateJob("delay-job");
        job.TriggerType = JobTriggerType.Delay;
        job.Delay = TimeSpan.FromMinutes(30);

        var before = DateTimeOffset.UtcNow;
        scheduler.RegisterJob(job);
        var after = DateTimeOffset.UtcNow;

        var next = scheduler.GetNextFireTime("delay-job");
        Assert.NotNull(next);
        Assert.InRange(next!.Value, before.AddMinutes(30), after.AddMinutes(30));
    }

    /// <summary>
    /// Cron 任务注册后排出未来的触发时间
    /// </summary>
    [Fact]
    public void RegisterJob_WithCronTrigger_SchedulesFutureMoment()
    {
        var scheduler = CreateScheduler(out _, out _);
        var job = CreateJob("cron-job");
        job.TriggerType = JobTriggerType.Cron;
        job.CronExpression = "0 3 * * *";

        var before = DateTimeOffset.UtcNow;
        scheduler.RegisterJob(job);

        var next = scheduler.GetNextFireTime("cron-job");
        Assert.NotNull(next);
        Assert.True(next!.Value > before);
    }

    /// <summary>
    /// Cron 表达式非法时排不出触发时间，任务不会被自动调度
    /// </summary>
    [Fact]
    public void RegisterJob_WithInvalidCronExpression_LeavesNextFireTimeNull()
    {
        var scheduler = CreateScheduler(out _, out _);
        var job = CreateJob("broken-cron-job");
        job.TriggerType = JobTriggerType.Cron;
        job.CronExpression = "definitely not a cron";

        scheduler.RegisterJob(job);

        Assert.Null(scheduler.GetNextFireTime("broken-cron-job"));
    }

    /// <summary>
    /// 手动触发类型的任务不参与自动排期
    /// </summary>
    [Fact]
    public void RegisterJob_WithManualTrigger_LeavesNextFireTimeNull()
    {
        var scheduler = CreateScheduler(out _, out _);

        scheduler.RegisterJob(CreateJob("manual-job"));

        Assert.Null(scheduler.GetNextFireTime("manual-job"));
    }

    /// <summary>
    /// 截止时间早于下次触发时间时排期被截断
    /// </summary>
    [Fact]
    public void RegisterJob_WhenEndTimeAlreadyPassed_TruncatesSchedule()
    {
        var scheduler = CreateScheduler(out _, out _);
        var job = CreateJob("expired-job");
        job.TriggerType = JobTriggerType.Interval;
        job.Interval = TimeSpan.FromHours(1);
        job.EndTime = DateTimeOffset.UtcNow.AddDays(-1);

        scheduler.RegisterJob(job);

        Assert.Null(scheduler.GetNextFireTime("expired-job"));
    }

    /// <summary>
    /// 未注册的任务查不到下次触发时间
    /// </summary>
    [Fact]
    public void GetNextFireTime_WhenJobUnknown_ReturnsNull()
    {
        var scheduler = CreateScheduler(out _, out _);

        Assert.Null(scheduler.GetNextFireTime("never-registered"));
    }

    /// <summary>
    /// 取消注册会同时清掉任务定义与触发状态
    /// </summary>
    [Fact]
    public void UnregisterJob_RemovesJobAndItsTriggerState()
    {
        var scheduler = CreateScheduler(out _, out _);
        var job = CreateJob("interval-job");
        job.TriggerType = JobTriggerType.Interval;
        job.Interval = TimeSpan.FromHours(1);
        scheduler.RegisterJob(job);

        scheduler.UnregisterJob("interval-job");

        Assert.Empty(scheduler.GetAllJobs());
        Assert.Null(scheduler.GetNextFireTime("interval-job"));
    }

    /// <summary>
    /// 取消注册不存在的任务是空操作
    /// </summary>
    [Fact]
    public void UnregisterJob_WhenJobUnknown_DoesNotThrow()
    {
        var scheduler = CreateScheduler(out _, out _);

        scheduler.UnregisterJob("never-registered");

        Assert.Empty(scheduler.GetAllJobs());
    }

    /// <summary>
    /// 暂停与恢复不存在的任务是空操作
    /// </summary>
    [Fact]
    public void PauseAndResumeJob_WhenJobUnknown_DoNotThrow()
    {
        var scheduler = CreateScheduler(out _, out _);

        scheduler.PauseJob("never-registered");
        scheduler.ResumeJob("never-registered");

        Assert.Null(scheduler.GetNextFireTime("never-registered"));
    }

    /// <summary>
    /// 暂停与恢复不影响已排好的下次触发时间
    /// </summary>
    [Fact]
    public void PauseAndResumeJob_KeepScheduledNextFireTime()
    {
        var scheduler = CreateScheduler(out _, out _);
        var job = CreateJob("interval-job");
        job.TriggerType = JobTriggerType.Interval;
        job.Interval = TimeSpan.FromHours(1);
        scheduler.RegisterJob(job);
        var scheduled = scheduler.GetNextFireTime("interval-job");

        scheduler.PauseJob("interval-job");
        Assert.Equal(scheduled, scheduler.GetNextFireTime("interval-job"));

        scheduler.ResumeJob("interval-job");
        Assert.Equal(scheduled, scheduler.GetNextFireTime("interval-job"));
    }

    /// <summary>
    /// 手动触发不存在的任务时抛出 InvalidOperationException 并带出任务名
    /// </summary>
    [Fact]
    public async Task TriggerJobAsync_WhenJobUnknown_ThrowsInvalidOperationException()
    {
        var scheduler = CreateScheduler(out _, out _);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scheduler.TriggerJobAsync("never-registered"));

        Assert.Contains("never-registered", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 手动触发返回实例唯一标识，并把带完整上下文的实例交给执行器
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WhenJobRegistered_HandsFullyPopulatedInstanceToExecutor()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        var job = CreateJob("manual-job");
        scheduler.RegisterJob(job);

        var instanceId = await scheduler.TriggerJobAsync("manual-job");

        Assert.False(string.IsNullOrWhiteSpace(instanceId));
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        var instance = executor.LastInstance;
        Assert.NotNull(instance);
        Assert.Equal(instanceId, instance!.InstanceId);
        Assert.Equal("manual-job", instance.JobName);
        Assert.Same(job, instance.JobInfo);
        Assert.Equal(JobTriggerType.Manual, instance.TriggerType);
        Assert.Equal(Environment.MachineName, instance.ExecutionNode);
        Assert.False(string.IsNullOrWhiteSpace(instance.TraceId));
        Assert.True(instance.ScheduledAt > DateTimeOffset.MinValue);
    }

    /// <summary>
    /// 手动触发时把参数原样透传给执行器
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WithParameters_PassesThemToExecutor()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        scheduler.RegisterJob(CreateJob("manual-job"));
        var parameters = new Dictionary<string, object?> { ["batchSize"] = 100 };

        await scheduler.TriggerJobAsync("manual-job", parameters);
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        Assert.Same(parameters, executor.LastParameters);
        Assert.Same(parameters, executor.LastInstance!.Parameters);
    }

    /// <summary>
    /// 不允许并发的任务在已有运行中实例时被跳过：返回空标识且完全不进入执行器
    /// </summary>
    [Fact]
    public async Task TriggerJobAsync_WhenConcurrencyDisallowedAndInstanceRunning_SkipsExecution()
    {
        var scheduler = CreateScheduler(out var executor, out var store);
        var job = CreateJob("exclusive-job");
        job.AllowConcurrent = false;
        scheduler.RegisterJob(job);
        store.RunningInstances.Add(new JobInstance
        {
            JobName = "exclusive-job",
            JobInfo = job,
            Status = JobStatus.Running,
            TriggerType = JobTriggerType.Manual
        });

        var instanceId = await scheduler.TriggerJobAsync("exclusive-job");

        Assert.Equal(string.Empty, instanceId);
        Assert.Equal(0, executor.InvocationCount);
    }

    /// <summary>
    /// 不允许并发但没有运行中实例时正常执行
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WhenConcurrencyDisallowedAndNothingRunning_Executes()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        var job = CreateJob("exclusive-job");
        job.AllowConcurrent = false;
        scheduler.RegisterJob(job);

        var instanceId = await scheduler.TriggerJobAsync("exclusive-job");

        Assert.NotEqual(string.Empty, instanceId);
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        Assert.Equal(1, executor.InvocationCount);
    }

    /// <summary>
    /// 允许并发的任务即使已有运行中实例也照常触发
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WhenConcurrencyAllowed_IgnoresRunningInstances()
    {
        var scheduler = CreateScheduler(out var executor, out var store);
        var job = CreateJob("parallel-job");
        job.AllowConcurrent = true;
        scheduler.RegisterJob(job);
        store.RunningInstances.Add(new JobInstance
        {
            JobName = "parallel-job",
            JobInfo = job,
            Status = JobStatus.Running,
            TriggerType = JobTriggerType.Manual
        });

        var instanceId = await scheduler.TriggerJobAsync("parallel-job");

        Assert.NotEqual(string.Empty, instanceId);
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 运行中实例属于别的任务时不构成阻塞
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WhenRunningInstanceBelongsToAnotherJob_StillExecutes()
    {
        var scheduler = CreateScheduler(out var executor, out var store);
        var job = CreateJob("exclusive-job");
        job.AllowConcurrent = false;
        scheduler.RegisterJob(job);
        store.RunningInstances.Add(new JobInstance
        {
            JobName = "some-other-job",
            JobInfo = job,
            Status = JobStatus.Running,
            TriggerType = JobTriggerType.Manual
        });

        var instanceId = await scheduler.TriggerJobAsync("exclusive-job");

        Assert.NotEqual(string.Empty, instanceId);
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 参数里的 tenantId 决定实例归属租户
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WithTenantIdParameter_ResolvesTenantFromParameters()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        scheduler.RegisterJob(CreateJob("tenant-job"));

        await scheduler.TriggerJobAsync("tenant-job", new Dictionary<string, object?> { ["tenantId"] = 42L });
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        Assert.Equal(42L, executor.LastInstance!.TenantId);
    }

    /// <summary>
    /// 没有参数时回落到任务定义上的租户
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WithoutParameters_FallsBackToJobTenant()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        var job = CreateJob("tenant-job");
        job.TenantId = 7L;
        scheduler.RegisterJob(job);

        await scheduler.TriggerJobAsync("tenant-job");
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        Assert.Equal(7L, executor.LastInstance!.TenantId);
    }

    /// <summary>
    /// 参数里的 tenantId 优先级高于任务定义上的租户
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WhenBothTenantSourcesPresent_ParameterWins()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        var job = CreateJob("tenant-job");
        job.TenantId = 7L;
        scheduler.RegisterJob(job);

        await scheduler.TriggerJobAsync("tenant-job", new Dictionary<string, object?> { ["tenantId"] = "42" });
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        Assert.Equal(42L, executor.LastInstance!.TenantId);
    }

    /// <summary>
    /// 参数里的 tenantId 不是合法数值时回落到任务定义
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WhenTenantParameterIsNotNumeric_FallsBackToJobTenant()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        var job = CreateJob("tenant-job");
        job.TenantId = 7L;
        scheduler.RegisterJob(job);

        await scheduler.TriggerJobAsync("tenant-job", new Dictionary<string, object?> { ["tenantId"] = "abc" });
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        Assert.Equal(7L, executor.LastInstance!.TenantId);
    }

    /// <summary>
    /// 既无参数也无任务级租户时实例归属 Host（租户为空）
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WithoutAnyTenantSource_LeavesTenantNull()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        scheduler.RegisterJob(CreateJob("host-job"));

        await scheduler.TriggerJobAsync("host-job");
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        Assert.Null(executor.LastInstance!.TenantId);
    }

    /// <summary>
    /// 延时任务是一次性的：触发过一次之后不再续排，避免退化成周期任务
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WithDelayTrigger_DoesNotRescheduleAfterFirstFire()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        var job = CreateJob("delay-job");
        job.TriggerType = JobTriggerType.Delay;
        job.Delay = TimeSpan.FromMinutes(30);
        scheduler.RegisterJob(job);
        Assert.NotNull(scheduler.GetNextFireTime("delay-job"));

        await scheduler.TriggerJobAsync("delay-job");
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        Assert.Null(scheduler.GetNextFireTime("delay-job"));
    }

    /// <summary>
    /// 间隔任务每次触发后都会续排下一次
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WithIntervalTrigger_ReschedulesAfterFire()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        var job = CreateJob("interval-job");
        job.TriggerType = JobTriggerType.Interval;
        job.Interval = TimeSpan.FromHours(1);
        scheduler.RegisterJob(job);
        var firstSchedule = scheduler.GetNextFireTime("interval-job");

        await scheduler.TriggerJobAsync("interval-job");
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

        var secondSchedule = scheduler.GetNextFireTime("interval-job");
        Assert.NotNull(secondSchedule);
        Assert.True(secondSchedule!.Value >= firstSchedule!.Value);
    }

    /// <summary>
    /// 执行器抛出异常时不会冒泡到调用方（后台执行独立于触发调用）
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WhenExecutorThrows_DoesNotPropagateToCaller()
    {
        var executor = new RecordingJobExecutor { ThrowOnExecute = true };
        var scheduler = new CompositeJobScheduler(
            executor,
            NullLogger<CompositeJobScheduler>.Instance,
            new StubJobStore(),
            new ServiceCollection().BuildServiceProvider());
        scheduler.RegisterJob(CreateJob("faulty-job"));

        var instanceId = await scheduler.TriggerJobAsync("faulty-job");

        Assert.NotEqual(string.Empty, instanceId);
        await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 启动与停止都幂等，重复调用不抛异常
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StartAsync_And_StopAsync_AreIdempotent()
    {
        var scheduler = CreateScheduler(out _, out _);

        await scheduler.StopAsync(TestContext.Current.CancellationToken);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await scheduler.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 调度器启动后，到期的间隔任务会被调度循环自动触发
    /// </summary>
    /// <remarks>
    /// 用 TaskCompletionSource 等待"被触发"这一事件，而不是睡固定时长再断言。
    /// </remarks>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StartAsync_WhenIntervalJobIsDue_FiresItAutomatically()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        var job = CreateJob("due-job");
        job.TriggerType = JobTriggerType.Interval;
        job.Interval = TimeSpan.FromMilliseconds(1);
        scheduler.RegisterJob(job);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        }
        finally
        {
            await scheduler.StopAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(executor.InvocationCount >= 1);
        Assert.Equal(JobTriggerType.Interval, executor.LastInstance!.TriggerType);
    }

    /// <summary>
    /// 停止后的调度器不再持有定时器，重新启动仍可正常工作
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StopAsync_ThenStartAsync_RestartsSchedulerLoop()
    {
        var scheduler = CreateScheduler(out var executor, out _);
        var job = CreateJob("due-job");
        job.TriggerType = JobTriggerType.Interval;
        job.Interval = TimeSpan.FromMilliseconds(1);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        await scheduler.StopAsync(TestContext.Current.CancellationToken);

        scheduler.RegisterJob(job);
        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await executor.Executed.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        }
        finally
        {
            await scheduler.StopAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(executor.InvocationCount >= 1);
    }

    /// <summary>
    /// 组装一个带假执行器与假存储的调度器
    /// </summary>
    private static CompositeJobScheduler CreateScheduler(out RecordingJobExecutor executor, out StubJobStore store)
    {
        executor = new RecordingJobExecutor();
        store = new StubJobStore();
        return new CompositeJobScheduler(
            executor,
            NullLogger<CompositeJobScheduler>.Instance,
            store,
            new ServiceCollection().BuildServiceProvider());
    }

    /// <summary>
    /// 构造一个最小可用的手动触发任务定义
    /// </summary>
    private static JobInfo CreateJob(string jobName)
    {
        return new JobInfo
        {
            JobName = jobName,
            JobType = typeof(CompositeJobSchedulerTests),
            TriggerType = JobTriggerType.Manual
        };
    }

    /// <summary>
    /// 记录调用的假任务执行器
    /// </summary>
    private sealed class RecordingJobExecutor : IJobExecutor
    {
        private readonly TaskCompletionSource _executed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _invocationCount;

        /// <summary>
        /// 执行器是否以抛异常收场
        /// </summary>
        public bool ThrowOnExecute { get; init; }

        /// <summary>
        /// 首次被调用时完成，用于事件驱动等待
        /// </summary>
        public Task Executed => _executed.Task;

        /// <summary>
        /// 最近一次收到的任务实例
        /// </summary>
        public JobInstance? LastInstance { get; private set; }

        /// <summary>
        /// 最近一次收到的参数
        /// </summary>
        public IDictionary<string, object?>? LastParameters { get; private set; }

        /// <summary>
        /// 被调用次数
        /// </summary>
        public int InvocationCount => Volatile.Read(ref _invocationCount);

        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(JobInstance jobInstance, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            LastInstance = jobInstance;
            LastParameters = parameters;
            Interlocked.Increment(ref _invocationCount);
            _executed.TrySetResult();

            return ThrowOnExecute
                ? throw new InvalidOperationException("模拟执行器异常")
                : Task.FromResult(JobResult.Success());
        }
    }

    /// <summary>
    /// 可预置"运行中实例"的假任务存储
    /// </summary>
    private sealed class StubJobStore : IJobStore
    {
        /// <summary>
        /// 预置的运行中实例
        /// </summary>
        public List<JobInstance> RunningInstances { get; } = [];

        /// <summary>
        /// 保存任务实例
        /// </summary>
        public Task SaveJobInstanceAsync(JobInstance jobInstance)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 更新任务实例状态
        /// </summary>
        public Task UpdateJobStatusAsync(string instanceId, JobStatus status)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 保存执行历史
        /// </summary>
        public Task SaveJobHistoryAsync(JobHistory history)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取任务实例
        /// </summary>
        public Task<JobInstance?> GetJobInstanceAsync(string instanceId)
        {
            return Task.FromResult<JobInstance?>(null);
        }

        /// <summary>
        /// 获取执行历史
        /// </summary>
        public Task<IReadOnlyList<JobHistory>> GetJobHistoryAsync(string jobName, int pageIndex = 1, int pageSize = 20)
        {
            return Task.FromResult<IReadOnlyList<JobHistory>>(new List<JobHistory>());
        }

        /// <summary>
        /// 获取运行中的任务实例
        /// </summary>
        public Task<IReadOnlyList<JobInstance>> GetRunningInstancesAsync(string jobName)
        {
            IReadOnlyList<JobInstance> matched = RunningInstances.Where(instance => instance.JobName == jobName).ToList();
            return Task.FromResult(matched);
        }

        /// <summary>
        /// 清理过期历史
        /// </summary>
        public Task CleanupHistoryAsync(int retentionDays)
        {
            return Task.CompletedTask;
        }
    }
}
