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
/// 后台作业轮询 Worker 的分布式锁续期测试
/// </summary>
/// <remarks>
/// 这把锁存在的全部理由是"多实例单活"。可一轮要串行跑掉刚领到的整批作业（上限默认 1000 条），
/// 而锁的 TTL 是固定的：本轮耗时一旦超过 TTL，锁自动过期，另一实例抢到后会领到同一批还没删掉的作业，
/// 于是同一个作业被跑两遍——恰好是这把锁本该杜绝的事。
/// <para>
/// 用例用 <see cref="SteppingClock"/> 在毫秒内造出"跑了好几分钟"的长轮次，
/// 从而在不真等的前提下把两条契约钉死：长轮次要续期；续不上就必须收手，把剩下的作业留给下一轮。
/// </para>
/// </remarks>
public class BackgroundJobWorkerLockRenewalTests
{
    /// <summary>
    /// 单个用例的兜底超时
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 分布式锁 TTL（秒），续期周期为其一半
    /// </summary>
    private const int LockExpirySeconds = 30;

    private static readonly DateTime Start = new(2026, 7, 8, 9, 10, 11, DateTimeKind.Utc);

    /// <summary>
    /// 单轮耗时越过 TTL 一半时，Worker 在作业之间给锁续期
    /// </summary>
    /// <remarks>
    /// 修复前这里一次续期都不会发生：框架早就提供了 <c>ExtendAsync</c>，但抢到锁之后再没人调用它。
    /// </remarks>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenRoundOutlivesHalfOfLockTtl_ExtendsLockWhileStillWorking()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();
        var jobName = jobOptions.GetJobs()[0].JobName;

        var store = new RecordingBackgroundJobStore();
        store.EnqueueWaitingBatch(CreateJob(jobName), CreateJob(jobName), CreateJob(jobName));

        var executer = new RecordingBackgroundJobExecuter();
        var distributedLock = new RenewalTrackingDistributedLock();

        using var provider = BuildProvider(store, executer, jobOptions, new SteppingClock(Start, TimeSpan.FromSeconds(10)));
        using var worker = CreateWorker(provider, distributedLock);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Deleted.Count == 3, "整批作业都应被执行并删除");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(distributedLock.ExtendCallCount >= 1, "长轮次里应至少给锁续期一次");
        Assert.Equal(TimeSpan.FromSeconds(LockExpirySeconds), distributedLock.LastExtendExpiry);

        // 续期只是顺带做的事，不能打断本轮该干的活
        Assert.Equal(3, executer.Contexts.Count);
    }

    /// <summary>
    /// 续期失败（锁已不在自己手上）时本轮立刻收手，剩下的作业留到下一轮
    /// </summary>
    /// <remarks>
    /// 续不上意味着另一实例已经可以抢到这把锁。此时再往下跑，就是明知会重复执行还继续跑。
    /// 剩余作业没被删除，仍留在存储里，下一轮重新抢到锁后照样能处理，不会丢。
    /// </remarks>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenLockRenewalFails_StopsRoundInsteadOfRunningOnWithoutLock()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();
        var jobName = jobOptions.GetJobs()[0].JobName;

        var store = new RecordingBackgroundJobStore();
        var first = CreateJob(jobName);
        store.EnqueueWaitingBatch(first, CreateJob(jobName), CreateJob(jobName));

        var executer = new RecordingBackgroundJobExecuter();
        var distributedLock = new RenewalTrackingDistributedLock { CanExtend = false };

        using var provider = BuildProvider(store, executer, jobOptions, new SteppingClock(Start, TimeSpan.FromSeconds(10)));
        using var worker = CreateWorker(provider, distributedLock);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => distributedLock.ExtendCallCount >= 1, "第二个作业之前应先尝试续期");
        await WaitUntilAsync(() => store.WaitingCallCount >= 3, "续期失败后 Worker 应继续下一轮轮询而不是崩掉");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Single(executer.Contexts);
        Assert.Single(store.Deleted);
        Assert.Equal(first.Id, store.Deleted[0]);

        // 提前收手不等于把作业判死：剩下的既没被删也没被标记放弃
        Assert.Empty(store.Updated);
    }

    /// <summary>
    /// 反例：单轮很快跑完时不做任何续期，不给锁服务平添无谓请求
    /// </summary>
    /// <returns>任务</returns>
    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task PollOnce_WhenRoundIsShort_DoesNotExtendLock()
    {
        var jobOptions = new BackgroundJobOptions();
        jobOptions.AddJob<UnnamedArgsJob>();
        var jobName = jobOptions.GetJobs()[0].JobName;

        var store = new RecordingBackgroundJobStore();
        store.EnqueueWaitingBatch(CreateJob(jobName), CreateJob(jobName), CreateJob(jobName));

        var executer = new RecordingBackgroundJobExecuter();
        var distributedLock = new RenewalTrackingDistributedLock();

        // 固定时钟表示"整轮几乎不耗时"：时间从来没往前走，永远够不到续期阈值
        using var provider = BuildProvider(store, executer, jobOptions, new FakeClock(Start));
        using var worker = CreateWorker(provider, distributedLock);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => store.Deleted.Count == 3, "整批作业都应被执行并删除");
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, distributedLock.ExtendCallCount);
        Assert.Equal(3, executer.Contexts.Count);
    }

    /// <summary>
    /// 轮询等待条件成立
    /// </summary>
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
            CreationTime = Start,
            NextTryTime = Start,
            Priority = BackgroundJobPriority.Normal
        };
    }

    /// <summary>
    /// 构造轮询选项：首次不等待、10 毫秒一轮，锁 TTL 30 秒（续期周期 15 秒）
    /// </summary>
    /// <returns>Worker 选项</returns>
    private static BackgroundJobWorkerOptions CreateWorkerOptions()
    {
        return new BackgroundJobWorkerOptions
        {
            FirstWaitDurationMilliseconds = 0,
            JobPollPeriodMilliseconds = 10,
            MaxJobFetchCount = 5,
            DistributedLockName = "renewal-lock",
            DistributedLockExpirySeconds = LockExpirySeconds
        };
    }

    /// <summary>
    /// 构建 Worker 依赖的服务提供器
    /// </summary>
    /// <param name="store">存储</param>
    /// <param name="executer">执行器</param>
    /// <param name="jobOptions">作业注册表</param>
    /// <param name="clock">时钟</param>
    /// <returns>服务提供器</returns>
    private static ServiceProvider BuildProvider(
        RecordingBackgroundJobStore store,
        RecordingBackgroundJobExecuter executer,
        BackgroundJobOptions jobOptions,
        IClock clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBackgroundJobStore>(store);
        services.AddSingleton<IBackgroundJobExecuter>(executer);
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
    /// <param name="distributedLock">分布式锁</param>
    /// <returns>Worker</returns>
    private static BackgroundJobWorker CreateWorker(ServiceProvider provider, RenewalTrackingDistributedLock distributedLock)
    {
        return new BackgroundJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            distributedLock,
            Microsoft.Extensions.Options.Options.Create(CreateWorkerOptions()),
            NullLogger<BackgroundJobWorker>.Instance);
    }
}
