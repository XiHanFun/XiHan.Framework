// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs;
using XiHan.Framework.Tasks.BackgroundJobs.Abstractions;
using XiHan.Framework.Tasks.BackgroundJobs.Models;
using XiHan.Framework.Tasks.BackgroundJobs.Options;
using XiHan.Framework.Tasks.Tests.BackgroundJobs.Fakes;
using XiHan.Framework.Timing;

namespace XiHan.Framework.Tasks.Tests.BackgroundJobs;

/// <summary>
/// 后台作业轮询 Worker 测试
/// </summary>
/// <remarks>
/// Worker 的状态流转是整套后台作业机制的核心：
/// 成功即删除、业务失败按 <c>首等待 × 倍率^(尝试次数-1)</c> 退避、累计超过放弃阈值则标记放弃、
/// 致命错误（找不到配置 / 反序列化失败）不重试直接放弃、抢不到分布式锁整轮跳过。
/// <para>
/// 用例把首次等待设为 0、轮询周期设为 10 毫秒，配合可控时钟与手写存储，
/// 全部依靠"条件轮询"而不是固定睡眠来同步，因此不会有真实等待。
/// </para>
/// </remarks>
public class BackgroundJobWorkerTests
{
    /// <summary>
    /// 单个用例的兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    private static readonly DateTime Now = new(2026, 7, 8, 9, 10, 11, DateTimeKind.Utc);

    /// <summary>
    /// 关闭执行开关时 Worker 直接空转退出，既不抢锁也不碰存储
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task ExecuteAsync_WhenJobExecutionDisabled_ExitsWithoutTouchingLockOrStore()
    {
        var store = new RecordingBackgroundJobStore();
        var distributedLock = new FakeDistributedLock();
        var options = CreateWorkerOptions();
        options.IsJobExecutionEnabled = false;

        using var provider = BuildProvider(store, new RecordingBackgroundJobExecuter(), new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), new BackgroundJobOptions());
        using var worker = CreateWorker(provider, distributedLock, options);

        await worker.StartAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(worker.ExecuteTask);

        // 这里等 ExecuteTask 完成，而不是断言 StartAsync 返回时它「已经」完成：
        // 后者测的是宿主基类的实现细节而非本 Worker 的行为。.NET 8/9 的 BackgroundService.StartAsync
        // 在调用线程上同步跑完 ExecuteAsync，.NET 10 起不再保证（net10 的 IL 里 StartAsync 多出了
        // <StartAsync>b__5_0 这个闭包，net8/net9 中没有），于是 IsCompletedSuccessfully 会偶然为 false。
        // 关闭开关时 ExecuteAsync 立刻返回，等待随即结束；一旦 IsJobExecutionEnabled 不被尊重，
        // Worker 会持续轮询、这里等不到完成，用例照样红，覆盖面没有削弱。
        await worker.ExecuteTask!;
        Assert.Equal(0, distributedLock.AcquireCount);
        Assert.Equal(0, store.WaitingCallCount);

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 抢不到分布式锁时整轮跳过，不去领取作业
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenLockNotAcquired_SkipsRound()
    {
        var store = new RecordingBackgroundJobStore();
        store.EnqueueWaitingBatch(CreateJob("any-job"));
        var distributedLock = new FakeDistributedLock { CanAcquire = false };

        using var provider = BuildProvider(store, new RecordingBackgroundJobExecuter(), new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), new BackgroundJobOptions());
        using var worker = CreateWorker(provider, distributedLock, CreateWorkerOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => distributedLock.AcquireCount >= 2, "Worker 应持续尝试抢锁");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, store.WaitingCallCount);
        Assert.Equal(0, distributedLock.ReleasedCount);
    }

    /// <summary>
    /// 执行成功后删除作业，并按配置抢锁、按应用名与上限领取
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenJobSucceeds_DeletesJobAndUsesConfiguredLockAndFetchLimits()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();
        var jobName = jobOptions.GetJobs()[0].JobName;

        var store = new RecordingBackgroundJobStore();
        var job = CreateJob(jobName);
        store.EnqueueWaitingBatch(job);

        var currentTenant = new FakeCurrentTenant();
        var executer = new RecordingBackgroundJobExecuter(currentTenant);
        var distributedLock = new FakeDistributedLock();
        var options = CreateWorkerOptions();
        options.ApplicationName = "order-service";
        options.MaxJobFetchCount = 7;

        using var provider = BuildProvider(store, executer, new ScriptedBackgroundJobSerializer(), currentTenant, new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, distributedLock, options);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Deleted.Count == 1, "执行成功的作业应被删除");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(job.Id, store.Deleted[0]);
        Assert.Empty(store.Updated);
        Assert.Equal((short)1, job.TryCount);
        Assert.Equal(Now, job.LastTryTime);
        Assert.False(job.IsAbandoned);

        Assert.Equal("test-lock", distributedLock.LastResourceKey);
        Assert.Equal(TimeSpan.FromSeconds(30), distributedLock.LastExpiry);
        Assert.True(distributedLock.ReleasedCount >= 1);

        Assert.Equal("order-service", store.LastApplicationName);
        Assert.Equal(7, store.LastMaxResultCount);

        var context = Assert.Single(executer.Contexts);
        Assert.Equal(typeof(UnnamedArgsJob), context.JobType);
        Assert.IsType<UnnamedJobArgs>(context.JobArgs);
    }

    /// <summary>
    /// 执行期间切换到入队时的租户上下文，执行完自动还原
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenJobHasTenant_SwitchesTenantDuringExecutionAndRestoresAfter()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();

        var store = new RecordingBackgroundJobStore();
        var job = CreateJob(jobOptions.GetJobs()[0].JobName);
        job.TenantId = 2048;
        store.EnqueueWaitingBatch(job);

        var currentTenant = new FakeCurrentTenant();
        var executer = new RecordingBackgroundJobExecuter(currentTenant);

        using var provider = BuildProvider(store, executer, new ScriptedBackgroundJobSerializer(), currentTenant, new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, new FakeDistributedLock(), CreateWorkerOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Deleted.Count == 1, "执行成功的作业应被删除");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2048L, executer.ObservedTenantIds[0]!.Value);
        Assert.Contains(currentTenant.ChangedIds, x => x == 2048L);
        Assert.Null(currentTenant.Id);
    }

    /// <summary>
    /// 执行上下文携带的取消令牌来自 Worker 的停止令牌，服务停止后同步取消
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_PropagatesStoppingTokenIntoExecutionContext()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();

        var store = new RecordingBackgroundJobStore();
        store.EnqueueWaitingBatch(CreateJob(jobOptions.GetJobs()[0].JobName));

        var executer = new RecordingBackgroundJobExecuter();

        using var provider = BuildProvider(store, executer, new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, new FakeDistributedLock(), CreateWorkerOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => executer.Contexts.Count == 1, "作业应已被执行");

        var token = executer.Contexts[0].CancellationToken;
        Assert.True(token.CanBeCanceled);
        Assert.False(token.IsCancellationRequested);

        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(token.IsCancellationRequested);
    }

    /// <summary>
    /// 业务失败时按指数退避推迟下次执行时间，不删除也不放弃
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenJobFailsWithExecutionException_SchedulesExponentialBackoff()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();

        var store = new RecordingBackgroundJobStore();
        var job = CreateJob(jobOptions.GetJobs()[0].JobName);
        store.EnqueueWaitingBatch(job);

        var executer = new RecordingBackgroundJobExecuter
        {
            ExceptionToThrow = new BackgroundJobExecutionException("业务失败")
        };

        var options = CreateWorkerOptions();
        options.DefaultFirstWaitDurationSeconds = 60;
        options.DefaultWaitFactor = 2.0;

        using var provider = BuildProvider(store, executer, new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, new FakeDistributedLock(), options);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Updated.Count == 1, "业务失败的作业应被回写");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Empty(store.Deleted);
        Assert.False(job.IsAbandoned);
        Assert.Equal((short)1, job.TryCount);
        Assert.Equal(Now.AddSeconds(60), job.NextTryTime);
    }

    /// <summary>
    /// 已失败多次的作业退避间隔按倍率放大
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenJobAlreadyRetried_BackoffGrowsByWaitFactor()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();

        var store = new RecordingBackgroundJobStore();
        var job = CreateJob(jobOptions.GetJobs()[0].JobName);

        // 本轮开始前已尝试 2 次，进入 Worker 后自增到 3，退避为 60 × 2^(3-1) = 240 秒
        job.TryCount = 2;
        store.EnqueueWaitingBatch(job);

        var executer = new RecordingBackgroundJobExecuter
        {
            ExceptionToThrow = new BackgroundJobExecutionException("业务失败")
        };

        var options = CreateWorkerOptions();
        options.DefaultFirstWaitDurationSeconds = 60;
        options.DefaultWaitFactor = 2.0;

        using var provider = BuildProvider(store, executer, new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, new FakeDistributedLock(), options);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Updated.Count == 1, "业务失败的作业应被回写");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal((short)3, job.TryCount);
        Assert.Equal(Now.AddSeconds(240), job.NextTryTime);
        Assert.False(job.IsAbandoned);
    }

    /// <summary>
    /// 退避后的下次执行时间超过放弃阈值时直接放弃，不再安排重试
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenBackoffExceedsTimeout_MarksJobAbandoned()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();

        var store = new RecordingBackgroundJobStore();
        var job = CreateJob(jobOptions.GetJobs()[0].JobName);
        store.EnqueueWaitingBatch(job);

        var executer = new RecordingBackgroundJobExecuter
        {
            ExceptionToThrow = new BackgroundJobExecutionException("业务失败")
        };

        var options = CreateWorkerOptions();
        options.DefaultFirstWaitDurationSeconds = 60;
        options.DefaultTimeoutSeconds = 10;

        using var provider = BuildProvider(store, executer, new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, new FakeDistributedLock(), options);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Updated.Count == 1, "放弃的作业应被回写");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(job.IsAbandoned);
        Assert.Empty(store.Deleted);
    }

    /// <summary>
    /// 注册表里找不到作业配置时直接放弃，不做反序列化也不进入执行器
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenJobConfigurationMissing_AbandonsWithoutExecuting()
    {
        var store = new RecordingBackgroundJobStore();
        var job = CreateJob("job-name-not-registered");
        store.EnqueueWaitingBatch(job);

        var executer = new RecordingBackgroundJobExecuter();
        var serializer = new ScriptedBackgroundJobSerializer();

        using var provider = BuildProvider(store, executer, serializer, new FakeCurrentTenant(), new FakeClock(Now), new BackgroundJobOptions());
        using var worker = CreateWorker(provider, new FakeDistributedLock(), CreateWorkerOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Updated.Count == 1, "找不到配置的作业应被回写为放弃");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(job.IsAbandoned);
        Assert.Equal((short)1, job.TryCount);
        Assert.Empty(executer.Contexts);
        Assert.Equal(0, serializer.DeserializeCallCount);
        Assert.Empty(store.Deleted);
    }

    /// <summary>
    /// 反序列化失败属于致命错误，直接放弃而不是退避重试
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenDeserializationFails_AbandonsWithoutRetry()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();

        var store = new RecordingBackgroundJobStore();
        var job = CreateJob(jobOptions.GetJobs()[0].JobName);
        store.EnqueueWaitingBatch(job);

        var executer = new RecordingBackgroundJobExecuter();
        var serializer = new ScriptedBackgroundJobSerializer { ThrowOnDeserialize = true };

        using var provider = BuildProvider(store, executer, serializer, new FakeCurrentTenant(), new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, new FakeDistributedLock(), CreateWorkerOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Updated.Count == 1, "致命错误的作业应被回写为放弃");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(job.IsAbandoned);
        Assert.Empty(executer.Contexts);
        Assert.Equal(Now, job.NextTryTime);
    }

    /// <summary>
    /// 非执行异常（致命错误）不会被当成业务失败进入退避重试
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenExecuterThrowsUnexpectedException_AbandonsInsteadOfRetrying()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();

        var store = new RecordingBackgroundJobStore();
        var job = CreateJob(jobOptions.GetJobs()[0].JobName);
        store.EnqueueWaitingBatch(job);

        var executer = new RecordingBackgroundJobExecuter
        {
            ExceptionToThrow = new InvalidOperationException("非预期错误")
        };

        using var provider = BuildProvider(store, executer, new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, new FakeDistributedLock(), CreateWorkerOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Updated.Count == 1, "致命错误的作业应被回写为放弃");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(job.IsAbandoned);
        Assert.Equal(Now, job.NextTryTime);
    }

    /// <summary>
    /// 回写作业状态失败时只记日志，轮询主循环继续存活
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenStoreUpdateThrows_KeepsWorkerAlive()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();

        var store = new RecordingBackgroundJobStore { ThrowOnUpdate = true };
        store.EnqueueWaitingBatch(CreateJob(jobOptions.GetJobs()[0].JobName));

        var executer = new RecordingBackgroundJobExecuter
        {
            ExceptionToThrow = new BackgroundJobExecutionException("业务失败")
        };

        using var provider = BuildProvider(store, executer, new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, new FakeDistributedLock(), CreateWorkerOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Updated.Count >= 1 && store.WaitingCallCount >= 3, "写回失败后 Worker 仍应继续轮询");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(worker.ExecuteTask);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    /// <summary>
    /// 一轮内批量领取到的作业逐个执行
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenMultipleJobsFetched_ExecutesEachOfThem()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();
        var jobName = jobOptions.GetJobs()[0].JobName;

        var store = new RecordingBackgroundJobStore();
        var first = CreateJob(jobName);
        var second = CreateJob(jobName);
        var third = CreateJob(jobName);
        store.EnqueueWaitingBatch(first, second, third);

        var executer = new RecordingBackgroundJobExecuter();

        using var provider = BuildProvider(store, executer, new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), jobOptions);
        using var worker = CreateWorker(provider, new FakeDistributedLock(), CreateWorkerOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Deleted.Count == 3, "整批作业都应被执行并删除");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, executer.Contexts.Count);
        Assert.Contains(first.Id, store.Deleted);
        Assert.Contains(second.Id, store.Deleted);
        Assert.Contains(third.Id, store.Deleted);
    }

    /// <summary>
    /// 停止后 Worker 主循环正常结束，不留下未完成的任务
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StopAsync_CompletesWorkerLoopGracefully()
    {
        var store = new RecordingBackgroundJobStore();

        using var provider = BuildProvider(store, new RecordingBackgroundJobExecuter(), new ScriptedBackgroundJobSerializer(), new FakeCurrentTenant(), new FakeClock(Now), new BackgroundJobOptions());
        using var worker = CreateWorker(provider, new FakeDistributedLock(), CreateWorkerOptions());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.WaitingCallCount >= 2, "Worker 应已空转若干轮");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(worker.ExecuteTask);
        Assert.True(worker.ExecuteTask!.IsCompleted);
        Assert.False(worker.ExecuteTask!.IsFaulted);
    }

    /// <summary>
    /// 轮询等待条件成立
    /// </summary>
    /// <remarks>
    /// Worker 的状态变更发生在自己的轮询线程上，没有可精确同步的单点信号，
    /// 因此用"短周期轮询 + 硬上限"代替固定睡眠：正常几毫秒就返回，只有真出问题才会走满上限并判失败。
    /// </remarks>
    /// <param name="condition">条件</param>
    /// <param name="description">条件描述</param>
    /// <returns>任务</returns>
    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = Environment.TickCount64 + 10_000;
        while (Environment.TickCount64 <= deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"等待条件超时：{description}");
    }

    /// <summary>
    /// 构造一条立即可执行的作业记录
    /// </summary>
    /// <param name="jobName">作业名</param>
    /// <returns>作业记录</returns>
    private static BackgroundJobInfo CreateJob(string jobName)
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            JobName = jobName,
            JobArgs = "{}",
            CreationTime = Now,
            NextTryTime = Now,
            Priority = BackgroundJobPriority.Normal
        };
    }

    /// <summary>
    /// 构造轮询选项：首次不等待、10 毫秒一轮，保证用例快速收敛
    /// </summary>
    /// <returns>Worker 选项</returns>
    private static BackgroundJobWorkerOptions CreateWorkerOptions()
    {
        return new BackgroundJobWorkerOptions
        {
            FirstWaitDurationMilliseconds = 0,
            JobPollPeriodMilliseconds = 10,
            MaxJobFetchCount = 5,
            DistributedLockName = "test-lock",
            DistributedLockExpirySeconds = 30
        };
    }

    /// <summary>
    /// 构建 Worker 依赖的服务提供器
    /// </summary>
    /// <param name="store">存储</param>
    /// <param name="executer">执行器</param>
    /// <param name="serializer">序列化器</param>
    /// <param name="currentTenant">当前租户</param>
    /// <param name="clock">时钟</param>
    /// <param name="jobOptions">作业注册表</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(
        RecordingBackgroundJobStore store,
        RecordingBackgroundJobExecuter executer,
        ScriptedBackgroundJobSerializer serializer,
        FakeCurrentTenant currentTenant,
        FakeClock clock,
        BackgroundJobOptions jobOptions)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBackgroundJobStore>(store);
        services.AddSingleton<IBackgroundJobExecuter>(executer);
        services.AddSingleton<IBackgroundJobSerializer>(serializer);
        services.AddSingleton<ICurrentTenant>(currentTenant);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jobOptions));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 构建 Worker
    /// </summary>
    /// <param name="provider">服务提供器</param>
    /// <param name="distributedLock">分布式锁</param>
    /// <param name="options">Worker 选项</param>
    /// <returns>Worker</returns>
    private static BackgroundJobWorker CreateWorker(
        ServiceProvider provider,
        FakeDistributedLock distributedLock,
        BackgroundJobWorkerOptions options)
    {
        return new BackgroundJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            distributedLock,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<BackgroundJobWorker>.Instance);
    }
}
