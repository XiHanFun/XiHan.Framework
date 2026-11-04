// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using Xunit;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// <see cref="ILeaderElection"/> 的一致性套件
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateElection"/> 与 <see cref="StartNodeAsync"/>，全部用例随之执行。到期按套件传入的时钟判定，用例推进时钟而不是等待真实时间，结果与机器快慢无关。<see cref="StartNodeAsync"/> 供应用生命周期用例：选举接在调度引擎上，应用不在服务状态（启动中、排空中）时引擎不持有租约、持有就释放，释放在那时也必须真正生效（ADR-0091、ADR-0094）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class LeaderElectionConformance
{
    /// <summary>
    /// 过期用例使用的存活时间
    /// </summary>
    /// <remarks>
    /// 只有整数秒粒度的 Provider（如 Kubernetes Lease）改为 1 秒
    /// </remarks>
    protected virtual TimeSpan LeaseTimeToLive => TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// 创建被测选举
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次；选举与它产生的租约都按 <paramref name="clock"/> 判定到期
    /// </remarks>
    protected abstract ILeaderElection CreateElection(TimeProvider clock);

    /// <summary>
    /// 启动一个用被测选举的调度节点，并给出与它争同一租约的竞争者
    /// </summary>
    /// <remarks>
    /// 应用生命周期用例调用。节点是一个宿主：装配调度引擎（<see cref="JobSchedulerEngine"/>，提供 <c>scheduling.jobs</c>）与被测选举，把 <paramref name="clock"/> 登记为时钟（在 Provider 装配之前，引擎与选举都按它计时），轮询周期设得很长（如 <c>XiHan:Scheduling:PollIntervalSeconds = 3600</c>）让后台循环不动作，再装配 <paramref name="probe"/>（<c>UseFeature(probe)</c>）。<c>Competitor</c> 是另一个持有者的选举：与节点争同一个资源，节点启动、服务、排空与停止期间都要能获取与释放（数据库实现是同一个库上另一个在服务的节点，进程内实现是节点自己的选举实例）。<c>Host</c> 释放节点与竞争者用到的一切；用例可能已经停止了节点
    /// </remarks>
    protected abstract Task<(ILeaderElection Competitor, IAsyncDisposable Host)> StartNodeAsync(TimeProvider clock, IFeature probe);

    /// <summary>
    /// 用例 1：空闲资源可获取
    /// </summary>
    /// <remarks>
    /// 被持有时拒绝；释放后可再获取；不同资源互不影响
    /// </remarks>
    [Fact]
    public async Task Acquire_RefusesWhileHeld_AndSucceedsAfterRelease()
    {
        var clock = new FakeTimeProvider(Start);
        var election = CreateElection(clock);
        var resource = Resource();

        var lease = (await election.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal(resource, lease.Resource);
        Assert.True(lease.IsHeld);
        Assert.True(lease.ExpiresAt > clock.GetUtcNow());

        var refused = await election.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(SchedulingErrors.LeaseHeld, refused.Error?.Code);
        Assert.True((await election.TryAcquireAsync(Resource(), TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).IsSuccess);

        await lease.ReleaseAsync().ConfigureAwait(false);
        Assert.False(lease.IsHeld);
        await lease.ReleaseAsync().ConfigureAwait(false);
        // 持有者标识是实例身份（如 Pod 名），同一实例再次获取可以相同；旧租约对象必须已失效
        var again = (await election.TryAcquireAsync(resource, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.True(again.IsHeld);
        Assert.Equal(SchedulingErrors.LeaseHeld, (await lease.RenewAsync(CancellationToken.None).ConfigureAwait(false)).Error?.Code);
    }

    /// <summary>
    /// 用例 2：到期后他人可获取，原持有者续约失败
    /// </summary>
    /// <remarks>
    /// 到期前续约延长到期时刻
    /// </remarks>
    [Fact]
    public async Task Lease_ExpiresUnlessRenewed()
    {
        var clock = new FakeTimeProvider(Start);
        var election = CreateElection(clock);
        var resource = Resource();
        var ttl = LeaseTimeToLive;

        var lease = (await election.TryAcquireAsync(resource, ttl, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        clock.Advance(ttl / 2);
        var before = lease.ExpiresAt;
        Assert.True((await lease.RenewAsync(CancellationToken.None).ConfigureAwait(false)).IsSuccess);
        Assert.True(lease.ExpiresAt > before);
        Assert.True(lease.IsHeld);

        // 越过续约后的到期时刻：本地不再持有，他人可获取，原持有者续约失败
        clock.Advance(ttl + TimeSpan.FromMilliseconds(1));
        Assert.False(lease.IsHeld);
        var successor = (await election.TryAcquireAsync(resource, ttl, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.True(successor.IsHeld);
        Assert.Equal(SchedulingErrors.LeaseHeld, (await lease.RenewAsync(CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        await successor.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 用例 3：引擎只在应用服务时持有领导租约
    /// </summary>
    /// <remarks>
    /// 启动中（探针的启动钩子）的周期不拿，资源空着、竞争者能拿到；进入服务后，竞争者放手，下一个周期就拿到，竞争者随即被拒；排空中（探针的停止钩子）时钟只走半个租约、远没到期，这个周期释放，竞争者立即拿到。释放在排空中也必须真正生效（RFC-0013：<c>LeaderLease.Release</c> 在应用不在服务状态时也要生效）。时钟由用例推进，周期经 <see cref="JobSchedulerEngine.PollOnceAsync"/> 直接执行，不靠后台循环与真实等待。内核里应用离开服务状态只有启动中与排空中两段，排空之后不会回到服务状态，「恢复服务后重新拿」就是启动中让出、进入服务后再拿到
    /// </remarks>
    [Fact]
    public async Task Engine_HoldsTheLeaseOnlyWhileTheApplicationServes()
    {
        var clock = new FakeTimeProvider(Start);
        JobSchedulerEngine? engine = null;
        SchedulerOptions? options = null;
        App? app = null;
        ILeaderElection? competitor = null;
        (bool Serving, bool Leader)? starting = null;
        (LifecycleState State, bool Leader, bool CompetitorTookOver)? draining = null;
        // 钩子里只记录、不断言：断言失败会变成特性启动或停止失败，掩盖真正的原因
        var probe = new LeadershipProbeFeature(
            async (services, cancellationToken) =>
            {
                engine = services.GetRequiredService<JobSchedulerEngine>();
                options = services.GetRequiredService<SchedulerOptions>();
                app = services.GetRequiredService<App>();
                await engine.PollOnceAsync(cancellationToken).ConfigureAwait(false);
                starting = (app.State.IsServing(), engine.IsLeader);
            },
            async cancellationToken =>
            {
                // 排空中：时钟只走半个租约，租约远没到期；这个周期若还持有，竞争者就拿不到
                clock.Advance(options!.LeaseTimeToLive / 2);
                await engine!.PollOnceAsync(cancellationToken).ConfigureAwait(false);
                var tookOver = await competitor!.TryAcquireAsync(options.LeaseResource, options.LeaseTimeToLive, cancellationToken).ConfigureAwait(false);
                draining = (app!.State, engine.IsLeader, tookOver.IsSuccess);
                if (tookOver.TryGetValue(out var lease))
                {
                    await lease.ReleaseAsync().ConfigureAwait(false);
                }
            });

        var (rival, host) = await StartNodeAsync(clock, probe).ConfigureAwait(false);
        competitor = rival;
        await using (host.ConfigureAwait(false))
        {
            Assert.NotNull(engine);
            Assert.True(app!.State.IsServing(), "The node must be serving once it has started.");

            // 启动中让出的租约空着：竞争者能拿到，节点这个周期拿不到
            var held = (await competitor.TryAcquireAsync(options!.LeaseResource, options.LeaseTimeToLive, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            await engine.PollOnceAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.False(engine.IsLeader, "The node must not take a lease that another holder holds.");

            // 竞争者放手：在服务的节点下一个周期重新拿到，竞争者随即被拒
            await held.ReleaseAsync().ConfigureAwait(false);
            await engine.PollOnceAsync(CancellationToken.None).ConfigureAwait(false);
            Assert.True(engine.IsLeader, "A serving node must take the free lease.");
            Assert.Equal(SchedulingErrors.LeaseHeld, (await competitor.TryAcquireAsync(options.LeaseResource, options.LeaseTimeToLive, CancellationToken.None).ConfigureAwait(false)).Error?.Code);

            // 停止节点：探针在调度之前停止，那时应用在排空
            await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        Assert.NotNull(starting);
        Assert.False(starting.Value.Serving, "The probe must start while the application is not serving yet.");
        Assert.False(starting.Value.Leader, "A node that is not serving must not take the leader lease.");
        Assert.NotNull(draining);
        Assert.Equal(LifecycleState.Draining, draining.Value.State);
        Assert.False(draining.Value.Leader, "A draining node must release the leader lease instead of keeping it.");
        Assert.True(draining.Value.CompetitorTookOver, "Another holder must get the leader lease while the node drains, before the lease expires.");
    }

    /// <summary>
    /// 用例时钟的起点
    /// </summary>
    /// <remarks>
    /// 整秒，避开只有微秒精度的存储（如 Kubernetes MicroTime）的截断
    /// </remarks>
    private static DateTimeOffset Start { get; } = new(2026, 10, 31, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 套件内唯一的资源名
    /// </summary>
    private static JobName Resource() => new("conformance.lease." + Guid.NewGuid().ToString("N")[..8]);
}
