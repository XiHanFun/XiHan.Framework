// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 领导租约与应用生命周期（内存实现）。
/// </summary>
/// <remarks>
/// 应用不在服务状态（启动中、排空中）的周期不拿也不续领导租约，持有就立即释放，另一个持有者随即能拿到，不等租约到期。探针特性依赖 <c>scheduling.jobs</c>，在调度特性之后启动、之前停止，两次都正赶上应用不在服务状态；时钟由用例推进，周期由用例经 <see cref="JobSchedulerEngine.PollOnceAsync"/> 直接执行，不靠计时器与真实等待。
/// </remarks>
public sealed class LeadershipLifecycleTests
{
    [Fact]
    public async Task Leadership_IsNotTakenWhileStarting_AndIsReleasedWhileDraining()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        JobSchedulerEngine? engine = null;
        ILeaderElection? election = null;
        SchedulerOptions? options = null;
        App? app = null;
        var observed = new List<(string Phase, LifecycleState State, bool Leader, bool OtherTookOver)>();
        var probe = new StubFeature(TestManifests.Create("xihan.test.scheduling-leadership", requires: ["scheduling.jobs@1"]))
        {
            OnStart = async (services, cancellationToken) =>
            {
                engine = services.GetRequiredService<JobSchedulerEngine>();
                election = services.GetRequiredService<ILeaderElection>();
                options = services.GetRequiredService<SchedulerOptions>();
                app = services.GetRequiredService<App>();
                await engine.PollOnceAsync(cancellationToken);
                observed.Add(("starting", app.State, engine.IsLeader, false));
            },
            OnStop = async cancellationToken =>
            {
                // 排空中：时钟走了半个租约，租约还没到期；这个周期若还持有，另一个持有者就拿不到
                clock.Advance(options!.LeaseTimeToLive / 2);
                await engine!.PollOnceAsync(cancellationToken);
                var other = await election!.TryAcquireAsync(options.LeaseResource, options.LeaseTimeToLive, cancellationToken);
                observed.Add(("draining", app!.State, engine.IsLeader, other.IsSuccess));
                if (other.TryGetValue(out var lease))
                {
                    await lease.ReleaseAsync();
                }
            },
        };

        await using (var host = await TestHost.StartAsync(
            b => b.UseFeature<InMemorySchedulingFeature>().UseFeature(probe),
            [new("XiHan:Scheduling:PollIntervalSeconds", "3600")],
            services => services.AddSingleton<TimeProvider>(clock)))
        {
            // 服务中：本节点拿到租约，另一个持有者拿不到
            Assert.True(host.App.State.IsServing());
            await engine!.PollOnceAsync(CancellationToken.None);
            Assert.True(engine.IsLeader);
            Assert.False((await election!.TryAcquireAsync(options!.LeaseResource, options.LeaseTimeToLive, CancellationToken.None)).IsSuccess);
        }

        Assert.Equal(2, observed.Count);
        Assert.Equal(("starting", false), (observed[0].Phase, observed[0].State.IsServing()));
        Assert.False(observed[0].Leader, "A node that is not serving must not take the leader lease.");
        Assert.Equal(("draining", LifecycleState.Draining), (observed[1].Phase, observed[1].State));
        Assert.False(observed[1].Leader, "A draining node must release the leader lease instead of keeping it.");
        Assert.True(observed[1].OtherTookOver, "Another holder must get the leader lease while this node drains.");
    }
}
