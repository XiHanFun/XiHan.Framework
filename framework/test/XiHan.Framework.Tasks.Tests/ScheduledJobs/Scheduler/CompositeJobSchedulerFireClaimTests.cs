// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Abstractions;
using XiHan.Framework.Tasks.ScheduledJobs.Models;
using XiHan.Framework.Tasks.ScheduledJobs.Scheduler;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Scheduler;

/// <summary>
/// CompositeJobScheduler 调度循环重复触发防护测试
/// </summary>
/// <remarks>
/// 调度定时器每秒一跳，而"记录触发 + 重排下次触发时间"发生在派发出去的执行体内部。执行体只要还没
/// 跑到重排（例如 AllowConcurrent=false 时要先 await 存储查运行中实例），下一跳就会读到没被推进的
/// NextFireTime 而重复触发同一次排期。
/// 这里用一个"卡住不返回"的假存储把那段窗口人为拉长到跨越多个定时器周期，再断言执行体只被进入一次；
/// 卡点选在存储查询而不是任务体，是因为重复触发的判定发生在任务体之前，卡任务体测不到这条路径。
/// </remarks>
public class CompositeJobSchedulerFireClaimTests
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
    /// 观察窗口：定时器周期为 1 秒，这段时间内至少还会跳两次
    /// </summary>
    private static readonly TimeSpan ObservationWindow = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 上一次触发还没走完时，调度循环不会把同一次排期重复触发
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StartAsync_WhenPreviousFireIsStillInFlight_DoesNotEnterExecutionAgain()
    {
        var executor = new CountingJobExecutor();
        var store = new BlockingJobStore();
        var scheduler = CreateScheduler(executor, store);
        scheduler.RegisterJob(CreateDueIntervalJob("slow-job", allowConcurrent: false));

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            // 等到调度循环第一次进入执行体：此刻它被卡在"查运行中实例"，还没来得及记录触发与重排
            await store.FirstQuery.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

            // 观察窗口内定时器还会跳好几次；没有触发权抢占时每一跳都会再次进入执行体
            await Task.Delay(ObservationWindow, TestContext.Current.CancellationToken);

            Assert.Equal(1, store.QueryCount);
            Assert.Equal(0, executor.InvocationCount);
        }
        finally
        {
            store.Release();
            await scheduler.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// 触发权在执行体结束后释放：任务后续仍能被正常触发，抢占不会把任务永久卡死
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StartAsync_AfterInFlightFireCompletes_FiresAgainOnNextTick()
    {
        var executor = new CountingJobExecutor(targetInvocations: 2);
        var scheduler = CreateScheduler(executor, new EmptyJobStore());
        scheduler.RegisterJob(CreateDueIntervalJob("repeating-job", allowConcurrent: true));

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await executor.ReachedTarget.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
        }
        finally
        {
            await scheduler.StopAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(executor.InvocationCount >= 2);
    }

    /// <summary>
    /// 手动触发不受调度循环的触发权影响：调度循环卡住时手动触发照样能跑
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task TriggerJobAsync_WhileSchedulerLoopIsBlocked_StillExecutes()
    {
        var executor = new CountingJobExecutor();
        var store = new BlockingJobStore();
        var scheduler = CreateScheduler(executor, store);
        var job = CreateDueIntervalJob("slow-job", allowConcurrent: false);
        scheduler.RegisterJob(job);

        await scheduler.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await store.FirstQuery.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);

            var manualTrigger = scheduler.TriggerJobAsync("slow-job");
            store.Release();

            var instanceId = await manualTrigger.WaitAsync(WaitBudget, TestContext.Current.CancellationToken);
            Assert.False(string.IsNullOrWhiteSpace(instanceId));
        }
        finally
        {
            store.Release();
            await scheduler.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// 组装一个带假执行器与指定存储的调度器
    /// </summary>
    private static CompositeJobScheduler CreateScheduler(IJobExecutor executor, IJobStore store)
    {
        return new CompositeJobScheduler(
            executor,
            NullLogger<CompositeJobScheduler>.Instance,
            store,
            new ServiceCollection().BuildServiceProvider());
    }

    /// <summary>
    /// 构造一个注册后立刻到期的间隔任务
    /// </summary>
    private static JobInfo CreateDueIntervalJob(string jobName, bool allowConcurrent)
    {
        return new JobInfo
        {
            JobName = jobName,
            JobType = typeof(CompositeJobSchedulerFireClaimTests),
            TriggerType = JobTriggerType.Interval,
            Interval = TimeSpan.FromMilliseconds(1),
            AllowConcurrent = allowConcurrent
        };
    }

    /// <summary>
    /// 计数用的假任务执行器
    /// </summary>
    private sealed class CountingJobExecutor : IJobExecutor
    {
        private readonly TaskCompletionSource _reachedTarget = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _targetInvocations;
        private int _invocationCount;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="targetInvocations">达到多少次调用时完成 <see cref="ReachedTarget"/></param>
        public CountingJobExecutor(int targetInvocations = 1)
        {
            _targetInvocations = targetInvocations;
        }

        /// <summary>
        /// 调用次数达到目标值时完成，用于事件驱动等待
        /// </summary>
        public Task ReachedTarget => _reachedTarget.Task;

        /// <summary>
        /// 被调用次数
        /// </summary>
        public int InvocationCount => Volatile.Read(ref _invocationCount);

        /// <summary>
        /// 执行任务
        /// </summary>
        public Task<JobResult> ExecuteAsync(JobInstance jobInstance, IDictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _invocationCount) >= _targetInvocations)
            {
                _reachedTarget.TrySetResult();
            }

            return Task.FromResult(JobResult.Success());
        }
    }

    /// <summary>
    /// 会把"查运行中实例"卡住的假任务存储，用来人为拉长执行体的前半段
    /// </summary>
    private sealed class BlockingJobStore : IJobStore
    {
        private readonly TaskCompletionSource _firstQuery = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _queryCount;

        /// <summary>
        /// 首次收到查询时完成，用于事件驱动等待
        /// </summary>
        public Task FirstQuery => _firstQuery.Task;

        /// <summary>
        /// 收到查询的次数，等价于执行体被进入的次数
        /// </summary>
        public int QueryCount => Volatile.Read(ref _queryCount);

        /// <summary>
        /// 放行所有被卡住的查询
        /// </summary>
        public void Release()
        {
            _gate.TrySetResult();
        }

        /// <summary>
        /// 获取运行中的任务实例（卡住直到被放行，且恒返回空表示允许执行）
        /// </summary>
        public async Task<IReadOnlyList<JobInstance>> GetRunningInstancesAsync(string jobName)
        {
            _ = Interlocked.Increment(ref _queryCount);
            _firstQuery.TrySetResult();

            await _gate.Task;

            return Array.Empty<JobInstance>();
        }

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
            return Task.FromResult<IReadOnlyList<JobHistory>>(Array.Empty<JobHistory>());
        }

        /// <summary>
        /// 清理过期历史
        /// </summary>
        public Task CleanupHistoryAsync(int retentionDays)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 恒返回空集合的假任务存储
    /// </summary>
    private sealed class EmptyJobStore : IJobStore
    {
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
            return Task.FromResult<IReadOnlyList<JobHistory>>(Array.Empty<JobHistory>());
        }

        /// <summary>
        /// 获取运行中的任务实例
        /// </summary>
        public Task<IReadOnlyList<JobInstance>> GetRunningInstancesAsync(string jobName)
        {
            return Task.FromResult<IReadOnlyList<JobInstance>>(Array.Empty<JobInstance>());
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
