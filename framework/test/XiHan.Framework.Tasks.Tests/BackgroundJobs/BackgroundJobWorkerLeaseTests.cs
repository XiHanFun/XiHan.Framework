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
/// 后台作业 Worker 逐作业续租测试
/// </summary>
/// <remarks>
/// 存储时钟用 <see cref="FakeClock"/>，续租等待用 <see cref="TimerTrackingTimeProvider"/>，两者由用例同步推进；
/// 推进前先等续租计时器已创建，推进后以条件轮询等待异步结果。
/// </remarks>
public class BackgroundJobWorkerLeaseTests
{
    /// <summary>
    /// 单个用例的兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 存储的作业租约时长（秒）
    /// </summary>
    private const int LeaseSeconds = 60;

    private static readonly DateTime Start = new(2026, 7, 8, 9, 10, 11, DateTimeKind.Utc);

    /// <summary>
    /// 长任务执行期间按间隔续租，其它领取者拿不到该作业；完成后按令牌删除，且不调用旧的更新与删除
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenRunningLongerThanLease_RenewsLeaseAndCompletesByToken()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var harness = new LeaseHarness(_ => release.Task);
        var jobId = await harness.AddJobAsync();

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1 && harness.TimeProvider.TimerCount == 1, "作业应开始执行且续租计时器已创建");

        for (var i = 1; i <= 12; i++)
        {
            harness.Advance(TimeSpan.FromSeconds(15));
            var expectedTimers = i + 1;
            await WaitUntilAsync(() => harness.TimeProvider.TimerCount == expectedTimers, $"第 {i} 次续租后应创建下一个续租计时器");
            Assert.Empty(await harness.Store.Inner.GetWaitingJobsAsync(null, 10));
        }

        release.SetResult();
        await WaitUntilAsync(async () => await harness.Store.Inner.FindAsync(jobId) is null, "作业完成后应被删除");
        await harness.StopAsync();

        Assert.Equal(13, harness.Store.RenewCallCount);
        Assert.Equal(1, harness.Store.CompleteCallCount);
        Assert.Equal(0, harness.Store.TryUpdateCallCount);
        Assert.Equal(0, harness.Store.UpdateCallCount);
        Assert.Equal(0, harness.Store.DeleteCallCount);
    }

    /// <summary>
    /// 失去租约时取消执行令牌，协作的处理器停止后不回写，作业归新领取者
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenLeaseLostAndHandlerCooperates_CancelsExecutionAndDoesNotWriteBack()
    {
        await using var harness = new LeaseHarness(context => Task.Delay(Timeout.Infinite, context.CancellationToken));
        var jobId = await harness.AddJobAsync();

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1 && harness.TimeProvider.TimerCount == 1, "作业应开始执行且续租计时器已创建");

        harness.Clock.Now += TimeSpan.FromSeconds(LeaseSeconds + 1);
        var claimed = Assert.Single(await harness.Store.Inner.GetWaitingJobsAsync(null, 10));
        Assert.Equal(jobId, claimed.Id);

        harness.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await WaitUntilAsync(() => harness.Executer.FinishedCount == 1, "处理器应因取消而结束");
        await WaitUntilAsync(() => harness.Store.WaitingCallCount >= 2, "Worker 应已结束本轮");
        await harness.StopAsync();

        Assert.True(harness.Executer.Started[0].CancellationToken.IsCancellationRequested);
        var stored = await harness.Store.Inner.FindAsync(jobId);
        Assert.NotNull(stored);
        Assert.Equal(claimed.ClaimToken, stored.ClaimToken);
        Assert.Equal((short)0, stored.TryCount);
        Assert.Null(stored.LastTryTime);
        AssertNoWriteBack(harness.Store);
    }

    /// <summary>
    /// 失去租约后处理器仍成功返回时同样不回写
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenLeaseLostAndHandlerIgnoresCancellation_DoesNotWriteBack()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var harness = new LeaseHarness(_ => release.Task);
        var jobId = await harness.AddJobAsync();

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1 && harness.TimeProvider.TimerCount == 1, "作业应开始执行且续租计时器已创建");

        harness.Clock.Now += TimeSpan.FromSeconds(LeaseSeconds + 1);
        var claimed = Assert.Single(await harness.Store.Inner.GetWaitingJobsAsync(null, 10));

        harness.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await WaitUntilAsync(() => harness.Executer.Started[0].CancellationToken.IsCancellationRequested, "续租失败应取消执行令牌");

        release.SetResult();
        await WaitUntilAsync(() => harness.Executer.FinishedCount == 1, "处理器应已返回");
        await WaitUntilAsync(() => harness.Store.WaitingCallCount >= 2, "Worker 应已结束本轮");
        await harness.StopAsync();

        var stored = await harness.Store.Inner.FindAsync(jobId);
        Assert.NotNull(stored);
        Assert.Equal(claimed.ClaimToken, stored.ClaimToken);
        Assert.Equal((short)0, stored.TryCount);
        AssertNoWriteBack(harness.Store);
    }

    /// <summary>
    /// 管理端请求取消后，续租带回取消标记，处理器收到取消，作业按放弃回写
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenCancellationRequested_CancelsExecutionAndAbandonsJob()
    {
        await using var harness = new LeaseHarness(context => Task.Delay(Timeout.Infinite, context.CancellationToken));
        var jobId = await harness.AddJobAsync();

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1 && harness.TimeProvider.TimerCount == 1, "作业应开始执行且续租计时器已创建");

        Assert.Equal(BackgroundJobManagementStatus.CancellationRequested, await harness.Store.Inner.RequestCancellationAsync(jobId));

        harness.TimeProvider.Advance(TimeSpan.FromSeconds(15));
        await WaitUntilAsync(async () => await harness.Store.Inner.FindAsync(jobId) is null, "已取消的作业应按放弃处理并移除");
        await harness.StopAsync();

        Assert.True(harness.Executer.Started[0].CancellationToken.IsCancellationRequested);
        Assert.Equal(1, harness.Store.TryUpdateCallCount);
        Assert.Equal(0, harness.Store.CompleteCallCount);
        Assert.Equal(0, harness.Store.UpdateCallCount);
        Assert.Equal(0, harness.Store.DeleteCallCount);
    }

    /// <summary>
    /// 同批作业在前一个执行期间被他人领走时，执行前确认失败，不执行也不回写
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenLeaseLostBeforeExecution_SkipsJob()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await using var harness = new LeaseHarness(_ => Interlocked.Increment(ref calls) == 1 ? release.Task : Task.CompletedTask);
        var firstId = await harness.AddJobAsync(BackgroundJobPriority.High);
        var secondId = await harness.AddJobAsync(BackgroundJobPriority.Normal);

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1 && harness.TimeProvider.TimerCount == 1, "第一个作业应开始执行且续租计时器已创建");

        harness.Advance(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(15));
        await WaitUntilAsync(() => harness.TimeProvider.TimerCount == 2, "第一个作业应已续租");

        harness.Clock.Now += TimeSpan.FromSeconds(31);
        var claimed = Assert.Single(await harness.Store.Inner.GetWaitingJobsAsync(null, 10));
        Assert.Equal(secondId, claimed.Id);

        release.SetResult();
        await WaitUntilAsync(async () => await harness.Store.Inner.FindAsync(firstId) is null, "第一个作业应完成并删除");
        await WaitUntilAsync(() => harness.Store.WaitingCallCount >= 2, "Worker 应已结束本轮");
        await harness.StopAsync();

        Assert.Single(harness.Executer.Started);
        Assert.Equal(3, harness.Store.RenewCallCount);
        Assert.Equal(1, harness.Store.CompleteCallCount);
        var second = await harness.Store.Inner.FindAsync(secondId);
        Assert.NotNull(second);
        Assert.Equal(claimed.ClaimToken, second.ClaimToken);
        Assert.Equal((short)0, second.TryCount);
        Assert.Equal(0, harness.Store.TryUpdateCallCount);
        Assert.Equal(0, harness.Store.UpdateCallCount);
        Assert.Equal(0, harness.Store.DeleteCallCount);
    }

    /// <summary>
    /// 宿主停止时，被中断的作业与批内未执行的作业都释放租约，可立即被再次领取，且不累计失败
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJobs_WhenHostStops_ReleasesLeasesOfInterruptedAndPendingJobs()
    {
        await using var harness = new LeaseHarness(context => Task.Delay(Timeout.Infinite, context.CancellationToken));
        await harness.AddJobAsync(BackgroundJobPriority.High);
        await harness.AddJobAsync(BackgroundJobPriority.Normal);

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1, "第一个作业应开始执行");
        await harness.StopAsync();

        Assert.Single(harness.Executer.Started);
        Assert.Equal(2, harness.Store.ReleaseCallCount);
        var reclaimed = await harness.Store.Inner.GetWaitingJobsAsync(null, 10);
        Assert.Equal(2, reclaimed.Count);
        Assert.All(reclaimed, job => Assert.Equal((short)0, job.TryCount));
        Assert.Equal(0, harness.Store.TryUpdateCallCount);
        Assert.Equal(0, harness.Store.CompleteCallCount);
        Assert.Equal(0, harness.Store.UpdateCallCount);
        Assert.Equal(0, harness.Store.DeleteCallCount);
    }

    /// <summary>
    /// 业务失败按令牌回写退避结果并结束租约，不调用旧的更新
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenHandlerFails_WritesBackBackoffByToken()
    {
        await using var harness = new LeaseHarness(_ => Task.FromException(new BackgroundJobExecutionException("模拟业务失败")));
        var jobId = await harness.AddJobAsync();

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Store.TryUpdateCallCount == 1, "失败的作业应按令牌回写");
        await harness.StopAsync();

        var stored = await harness.Store.Inner.FindAsync(jobId);
        Assert.NotNull(stored);
        Assert.Equal((short)1, stored.TryCount);
        Assert.Equal(Start, stored.LastTryTime);
        Assert.Equal(Start.AddSeconds(60), stored.NextTryTime);
        Assert.False(stored.IsAbandoned);
        Assert.Null(stored.ClaimToken);
        Assert.Null(stored.LeaseExpiresAt);
        Assert.Equal(0, harness.Store.UpdateCallCount);
        Assert.Equal(0, harness.Store.DeleteCallCount);
    }

    /// <summary>
    /// 续租间隔：配置值小于租约时长三分之一时采用配置值，否则取四分之一
    /// </summary>
    /// <param name="configuredSeconds">配置的续租间隔（秒）</param>
    /// <param name="expectedSeconds">期望的续租间隔（秒）</param>
    /// <returns>任务</returns>
    [Theory(Timeout = TimeoutMilliseconds)]
    [InlineData(0, 15)]
    [InlineData(30, 15)]
    [InlineData(20, 15)]
    [InlineData(10, 10)]
    public async Task LeasedJob_RenewalInterval_FollowsConfigurationWithinOneThirdOfLease(int configuredSeconds, int expectedSeconds)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var harness = new LeaseHarness(_ => release.Task, configuredSeconds);
        var jobId = await harness.AddJobAsync();

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.TimeProvider.TimerCount == 1, "续租计时器应已创建");

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), harness.TimeProvider.DueTimes[0]);

        release.SetResult();
        await WaitUntilAsync(async () => await harness.Store.Inner.FindAsync(jobId) is null, "作业完成后应被删除");
        await harness.StopAsync();
    }

    /// <summary>
    /// 续租出错但租约尚未到期时继续执行，下个间隔续租成功后作业按令牌完成
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenRenewalThrowsBeforeExpiry_RetriesAndCompletesByToken()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var harness = new LeaseHarness(_ => release.Task);
        var jobId = await harness.AddJobAsync();

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1 && harness.TimeProvider.TimerCount == 1, "作业应开始执行且续租计时器已创建");

        harness.Store.RenewFailuresToInject = 1;
        harness.Advance(TimeSpan.FromSeconds(15));
        await WaitUntilAsync(() => harness.TimeProvider.TimerCount == 2, "续租出错后应等待下个间隔");
        Assert.False(harness.Executer.Started[0].CancellationToken.IsCancellationRequested);

        harness.Advance(TimeSpan.FromSeconds(15));
        await WaitUntilAsync(() => harness.TimeProvider.TimerCount == 3, "第二次续租应成功");

        release.SetResult();
        await WaitUntilAsync(async () => await harness.Store.Inner.FindAsync(jobId) is null, "作业完成后应被删除");
        await harness.StopAsync();

        Assert.False(harness.Executer.Started[0].CancellationToken.IsCancellationRequested);
        Assert.Equal(3, harness.Store.RenewCallCount);
        Assert.Equal(1, harness.Store.CompleteCallCount);
        Assert.Equal(0, harness.Store.TryUpdateCallCount);
        Assert.Equal(0, harness.Store.UpdateCallCount);
        Assert.Equal(0, harness.Store.DeleteCallCount);
    }

    /// <summary>
    /// 租约已到期后续租出错视为失租：取消执行令牌且不回写
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenRenewalThrowsAfterExpiry_CancelsExecutionAndDoesNotWriteBack()
    {
        await using var harness = new LeaseHarness(context => Task.Delay(Timeout.Infinite, context.CancellationToken));
        await harness.AddJobAsync();

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1 && harness.TimeProvider.TimerCount == 1, "作业应开始执行且续租计时器已创建");

        harness.Store.RenewFailuresToInject = int.MaxValue;
        harness.Advance(TimeSpan.FromSeconds(LeaseSeconds + 1), TimeSpan.FromSeconds(15));
        await WaitUntilAsync(() => harness.Executer.FinishedCount == 1, "处理器应因取消而结束");
        await WaitUntilAsync(() => harness.Store.WaitingCallCount >= 2, "Worker 应已结束本轮");
        await harness.StopAsync();

        Assert.True(harness.Executer.Started[0].CancellationToken.IsCancellationRequested);
        Assert.Single(harness.Executer.Started);
        AssertNoWriteBack(harness.Store);
    }

    /// <summary>
    /// 执行前确认时已被请求取消的作业不执行，直接按放弃回写
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenCancellationRequestedBeforeExecution_AbandonsWithoutExecuting()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await using var harness = new LeaseHarness(_ => Interlocked.Increment(ref calls) == 1 ? release.Task : Task.CompletedTask);
        var firstId = await harness.AddJobAsync(BackgroundJobPriority.High);
        var secondId = await harness.AddJobAsync(BackgroundJobPriority.Normal);

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1, "第一个作业应开始执行");

        Assert.Equal(BackgroundJobManagementStatus.CancellationRequested, await harness.Store.Inner.RequestCancellationAsync(secondId));

        release.SetResult();
        await WaitUntilAsync(async () => await harness.Store.Inner.FindAsync(secondId) is null, "已请求取消的作业应按放弃处理并移除");
        await harness.StopAsync();

        Assert.Single(harness.Executer.Started);
        Assert.Null(await harness.Store.Inner.FindAsync(firstId));
        Assert.Equal(1, harness.Store.CompleteCallCount);
        Assert.Equal(1, harness.Store.TryUpdateCallCount);
        Assert.Equal(0, harness.Store.UpdateCallCount);
        Assert.Equal(0, harness.Store.DeleteCallCount);
    }

    /// <summary>
    /// 租约路径找不到作业配置时不执行，按令牌回写放弃
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenJobConfigurationMissing_AbandonsByToken()
    {
        await using var harness = new LeaseHarness(_ => Task.CompletedTask);
        var jobId = await harness.AddJobAsync(jobName: "xihan-tests-unregistered-job");

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Store.TryUpdateCallCount == 1, "找不到配置的作业应按令牌回写");
        await harness.StopAsync();

        Assert.Empty(harness.Executer.Started);
        Assert.Null(await harness.Store.Inner.FindAsync(jobId));
        Assert.Equal(0, harness.Store.CompleteCallCount);
        Assert.Equal(0, harness.Store.UpdateCallCount);
        Assert.Equal(0, harness.Store.DeleteCallCount);
    }

    /// <summary>
    /// 不支持租约的存储即使作业带有令牌也走原路径：成功调用删除、失败调用更新
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StoreWithoutLeaseSupport_UsesDeleteAndUpdateAsBefore()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();
        var jobName = jobOptions.GetJobs()[0].JobName;

        var succeeded = CreateJob(jobName, BackgroundJobPriority.High);
        var failed = CreateJob(jobName, BackgroundJobPriority.Normal);
        foreach (var job in new[] { succeeded, failed })
        {
            job.ClaimToken = "foreign-token";
            job.LeaseExpiresAt = Start.AddMinutes(5);
        }

        var store = new RecordingBackgroundJobStore();
        store.EnqueueWaitingBatch(succeeded, failed);

        var calls = 0;
        var executer = new GatedBackgroundJobExecuter(_ => Interlocked.Increment(ref calls) == 1
            ? Task.CompletedTask
            : Task.FromException(new BackgroundJobExecutionException("模拟业务失败")));
        var timeProvider = new TimerTrackingTimeProvider(new DateTimeOffset(Start));

        using var provider = BuildProvider(store, executer, new FakeClock(Start), jobOptions);
        using var worker = CreateWorker(provider, CreateWorkerOptions(0), timeProvider);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Deleted.Count == 1 && store.Updated.Count == 1, "应按原路径删除成功作业并更新失败作业");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(((IBackgroundJobStore)store).SupportsJobLease);
        Assert.Equal(succeeded.Id, store.Deleted[0]);
        var updated = Assert.Single(store.Updated);
        Assert.Equal(failed.Id, updated.Id);
        Assert.Equal(Start.AddSeconds(60), updated.NextTryTime);
        Assert.Equal(0, timeProvider.TimerCount);
    }

    /// <summary>
    /// 不支持租约的存储：宿主停止中断处理器时不累计失败、不回写
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StoreWithoutLeaseSupport_WhenHostStopsDuringExecution_DoesNotWriteBack()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();
        var job = CreateJob(jobOptions.GetJobs()[0].JobName, BackgroundJobPriority.Normal);

        var store = new RecordingBackgroundJobStore();
        store.EnqueueWaitingBatch(job);

        var executer = new GatedBackgroundJobExecuter(context => Task.Delay(Timeout.Infinite, context.CancellationToken));

        using var provider = BuildProvider(store, executer, new FakeClock(Start), jobOptions);
        using var worker = CreateWorker(provider, CreateWorkerOptions(0), new TimerTrackingTimeProvider(new DateTimeOffset(Start)));

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => executer.Started.Count == 1, "作业应开始执行");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, executer.FinishedCount);
        Assert.Empty(store.Updated);
        Assert.Empty(store.Deleted);
        Assert.False(job.IsAbandoned);
    }

    /// <summary>
    /// 不支持租约的存储：宿主停止期间处理器抛出非取消的业务异常时照常退避回写
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task StoreWithoutLeaseSupport_WhenHandlerFailsWhileHostStops_WritesBackBackoff()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();
        var job = CreateJob(jobOptions.GetJobs()[0].JobName, BackgroundJobPriority.Normal);

        var store = new RecordingBackgroundJobStore();
        store.EnqueueWaitingBatch(job);

        var executer = new GatedBackgroundJobExecuter(FailAfterCancellationAsync);

        using var provider = BuildProvider(store, executer, new FakeClock(Start), jobOptions);
        using var worker = CreateWorker(provider, CreateWorkerOptions(0), new TimerTrackingTimeProvider(new DateTimeOffset(Start)));

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => executer.Started.Count == 1, "作业应开始执行");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        var updated = Assert.Single(store.Updated);
        Assert.Equal(job.Id, updated.Id);
        Assert.Equal((short)1, updated.TryCount);
        Assert.Equal(Start.AddSeconds(60), updated.NextTryTime);
        Assert.False(updated.IsAbandoned);
        Assert.Empty(store.Deleted);
    }

    /// <summary>
    /// 租约路径：宿主停止期间处理器抛出非取消的业务异常时照常按令牌退避回写，不只释放租约
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task LeasedJob_WhenHandlerFailsWhileHostStops_WritesBackBackoffByToken()
    {
        await using var harness = new LeaseHarness(FailAfterCancellationAsync);
        var jobId = await harness.AddJobAsync();

        await harness.StartAsync();
        await WaitUntilAsync(() => harness.Executer.Started.Count == 1, "作业应开始执行");
        await harness.StopAsync();

        var stored = await harness.Store.Inner.FindAsync(jobId);
        Assert.NotNull(stored);
        Assert.Equal((short)1, stored.TryCount);
        Assert.Equal(Start.AddSeconds(60), stored.NextTryTime);
        Assert.False(stored.IsAbandoned);
        Assert.Null(stored.ClaimToken);
        Assert.Equal(1, harness.Store.TryUpdateCallCount);
        Assert.Equal(0, harness.Store.ReleaseCallCount);
        Assert.Equal(0, harness.Store.UpdateCallCount);
    }

    /// <summary>
    /// 等到执行令牌取消后抛出非取消的业务异常
    /// </summary>
    /// <param name="context">执行上下文</param>
    /// <returns>任务</returns>
    private static async Task FailAfterCancellationAsync(BackgroundJobExecutionContext context)
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (context.CancellationToken.Register(() => cancelled.TrySetResult()))
        {
            await cancelled.Task;
        }

        throw new BackgroundJobExecutionException("模拟停止期间的业务失败");
    }

    /// <summary>
    /// 断言租约路径没有任何回写
    /// </summary>
    /// <param name="store">存储替身</param>
    private static void AssertNoWriteBack(LeaseAwareBackgroundJobStore store)
    {
        Assert.Equal(0, store.CompleteCallCount);
        Assert.Equal(0, store.TryUpdateCallCount);
        Assert.Equal(0, store.ReleaseCallCount);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Equal(0, store.DeleteCallCount);
    }

    /// <summary>
    /// 轮询等待条件成立
    /// </summary>
    /// <param name="condition">条件</param>
    /// <param name="description">条件描述</param>
    /// <returns>任务</returns>
    private static Task WaitUntilAsync(Func<bool> condition, string description)
    {
        return WaitUntilAsync(() => Task.FromResult(condition()), description);
    }

    /// <summary>
    /// 轮询等待异步条件成立
    /// </summary>
    /// <param name="condition">条件</param>
    /// <param name="description">条件描述</param>
    /// <returns>任务</returns>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string description)
    {
        var deadline = Environment.TickCount64 + 10_000;
        while (Environment.TickCount64 <= deadline)
        {
            if (await condition())
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
    /// <param name="priority">优先级</param>
    /// <returns>作业记录</returns>
    private static BackgroundJobInfo CreateJob(string jobName, BackgroundJobPriority priority)
    {
        return new BackgroundJobInfo
        {
            Id = Guid.NewGuid(),
            JobName = jobName,
            JobArgs = "{}",
            CreationTime = Start,
            NextTryTime = Start,
            Priority = priority
        };
    }

    /// <summary>
    /// 构造轮询选项：首次不等待、10 毫秒一轮
    /// </summary>
    /// <param name="renewalIntervalSeconds">续租间隔配置（秒）</param>
    /// <returns>Worker 选项</returns>
    private static BackgroundJobWorkerOptions CreateWorkerOptions(int renewalIntervalSeconds)
    {
        return new BackgroundJobWorkerOptions
        {
            FirstWaitDurationMilliseconds = 0,
            JobPollPeriodMilliseconds = 10,
            MaxJobFetchCount = 5,
            DistributedLockName = "test-lock",
            DistributedLockExpirySeconds = 30,
            JobLeaseRenewalIntervalSeconds = renewalIntervalSeconds
        };
    }

    /// <summary>
    /// 构建 Worker 依赖的服务提供器
    /// </summary>
    /// <param name="store">存储</param>
    /// <param name="executer">执行器</param>
    /// <param name="clock">时钟</param>
    /// <param name="jobOptions">作业注册表</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(IBackgroundJobStore store, IBackgroundJobExecuter executer, IClock clock, BackgroundJobOptions jobOptions)
    {
        var services = new ServiceCollection();
        services.AddSingleton(store);
        services.AddSingleton(executer);
        services.AddSingleton<IBackgroundJobSerializer>(new ScriptedBackgroundJobSerializer());
        services.AddSingleton<ICurrentTenant>(new FakeCurrentTenant());
        services.AddSingleton(clock);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jobOptions));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 构建 Worker
    /// </summary>
    /// <param name="provider">服务提供器</param>
    /// <param name="options">Worker 选项</param>
    /// <param name="timeProvider">续租等待使用的时间提供器</param>
    /// <returns>Worker</returns>
    private static BackgroundJobWorker CreateWorker(ServiceProvider provider, BackgroundJobWorkerOptions options, TimeProvider timeProvider)
    {
        return new BackgroundJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeDistributedLock(),
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<BackgroundJobWorker>.Instance,
            timeProvider);
    }

    /// <summary>
    /// 租约用例的装配：进程内存储、可控时钟与时间提供器、按用例处理逻辑执行的执行器
    /// </summary>
    private sealed class LeaseHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly BackgroundJobWorker _worker;
        private readonly string _jobName;
        private bool _stopped;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="handler">处理逻辑</param>
        /// <param name="renewalIntervalSeconds">续租间隔配置（秒）</param>
        public LeaseHarness(Func<BackgroundJobExecutionContext, Task> handler, int renewalIntervalSeconds = 0)
        {
            Clock = new FakeClock(Start);
            TimeProvider = new TimerTrackingTimeProvider(new DateTimeOffset(Start));
            var storeOptions = new BackgroundJobWorkerOptions { JobLeaseDurationSeconds = LeaseSeconds };
            Store = new LeaseAwareBackgroundJobStore(new DefaultBackgroundJobStore(Clock, Microsoft.Extensions.Options.Options.Create(storeOptions)));
            Executer = new GatedBackgroundJobExecuter(handler);

            var jobOptions = new BackgroundJobOptions();
            jobOptions.AddJob<UnnamedArgsJob>();
            _jobName = jobOptions.GetJobs()[0].JobName;

            _provider = BuildProvider(Store, Executer, Clock, jobOptions);
            _worker = CreateWorker(_provider, CreateWorkerOptions(renewalIntervalSeconds), TimeProvider);
        }

        /// <summary>
        /// 存储时钟
        /// </summary>
        public FakeClock Clock { get; }

        /// <summary>
        /// 续租等待使用的时间提供器
        /// </summary>
        public TimerTrackingTimeProvider TimeProvider { get; }

        /// <summary>
        /// 存储替身
        /// </summary>
        public LeaseAwareBackgroundJobStore Store { get; }

        /// <summary>
        /// 执行器替身
        /// </summary>
        public GatedBackgroundJobExecuter Executer { get; }

        /// <summary>
        /// 向存储加入一条立即可执行的作业
        /// </summary>
        /// <param name="priority">优先级</param>
        /// <returns>作业标识</returns>
        /// <param name="jobName">作业名，为空时取已注册的作业</param>
        public async Task<Guid> AddJobAsync(BackgroundJobPriority priority = BackgroundJobPriority.Normal, string? jobName = null)
        {
            var job = CreateJob(jobName ?? _jobName, priority);
            await Store.Inner.InsertAsync(job);
            return job.Id;
        }

        /// <summary>
        /// 同步推进存储时钟与时间提供器
        /// </summary>
        /// <param name="duration">推进量</param>
        public void Advance(TimeSpan duration)
        {
            Advance(duration, duration);
        }

        /// <summary>
        /// 分别推进存储时钟与时间提供器
        /// </summary>
        /// <param name="clockDuration">存储时钟推进量</param>
        /// <param name="timerDuration">时间提供器推进量</param>
        public void Advance(TimeSpan clockDuration, TimeSpan timerDuration)
        {
            Clock.Now += clockDuration;
            TimeProvider.Advance(timerDuration);
        }

        /// <summary>
        /// 启动 Worker
        /// </summary>
        /// <returns>任务</returns>
        public Task StartAsync()
        {
            return _worker.StartAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// 停止 Worker
        /// </summary>
        /// <returns>任务</returns>
        public async Task StopAsync()
        {
            _stopped = true;
            await _worker.StopAsync(TestContext.Current.CancellationToken);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <returns>任务</returns>
        public async ValueTask DisposeAsync()
        {
            if (!_stopped)
            {
                await _worker.StopAsync(CancellationToken.None);
            }

            _worker.Dispose();
            await _provider.DisposeAsync();
        }
    }
}
