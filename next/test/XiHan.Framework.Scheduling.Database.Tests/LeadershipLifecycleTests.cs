// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 领导租约与应用生命周期（数据库实现）。
/// </summary>
/// <remarks>
/// 两个节点共用一个 SQLite 文件与一个由用例推进的时钟，轮询周期 1 小时（后台循环不动作），周期由用例经 <see cref="JobSchedulerEngine.PollOnceAsync"/> 直接执行。应用不在服务状态（启动中、排空中）的周期不拿也不续租约，持有就立即删行，另一个节点随即能拿到；停止时同样删行。删行在排空与停止时也要成功——那时管道已拒绝调用，所以释放不经管道。
/// </remarks>
public sealed class LeadershipLifecycleTests
{
    [Fact]
    public async Task Leadership_IsNotTakenWhileStarting_AndIsReleasedWhileDraining_SoAnotherNodeTakesOver()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        var logs = new LogCapture();
        JobSchedulerEngine? engine = null;
        SchedulerOptions? options = null;
        ILeaderElection? other = null;
        App? app = null;
        var observed = new List<(string Phase, LifecycleState State, bool Leader, bool OtherTookOver)>();
        // 依赖 scheduling.jobs：在数据库调度（已起引擎）之后启动、之前停止，两次都正赶上应用不在服务状态
        var probe = new StubFeature(TestManifests.Create("xihan.test.scheduling-leadership", requires: ["scheduling.jobs@1"]))
        {
            OnStart = async (services, cancellationToken) =>
            {
                engine = services.GetRequiredService<JobSchedulerEngine>();
                options = services.GetRequiredService<SchedulerOptions>();
                app = services.GetRequiredService<App>();
                await engine.PollOnceAsync(cancellationToken);
                observed.Add(("starting", app.State, engine.IsLeader, false));
            },
            OnStop = async cancellationToken =>
            {
                // 排空中：时钟走了半个租约，行还没到期；这个周期若还持有，另一个节点就拿不到
                clock.Advance(options!.LeaseTimeToLive / 2);
                await engine!.PollOnceAsync(cancellationToken);
                var tookOver = await other!.TryAcquireAsync(options.LeaseResource, options.LeaseTimeToLive, cancellationToken);
                observed.Add(("draining", app!.State, engine.IsLeader, tookOver.IsSuccess));
                if (tookOver.TryGetValue(out var lease))
                {
                    await lease.ReleaseAsync();
                }
            },
        };

        await using (var database = new SqliteDatabase())
        {
            var first = await database.StartHostAsync(
                NodeSettings("node-a"),
                services =>
                {
                    services.AddSingleton<ILoggerFactory>(logs);
                    services.AddSingleton<TimeProvider>(clock);
                },
                b => b.UseFeature(probe));
            var second = await database.StartHostAsync(NodeSettings("node-b"), services => services.AddSingleton<TimeProvider>(clock));
            other = second.Services.GetRequiredService<ILeaderElection>();

            // 服务中：本节点拿到租约，另一个节点拿不到
            Assert.True(first.App.State.IsServing());
            await engine!.PollOnceAsync(CancellationToken.None);
            Assert.True(engine.IsLeader);
            Assert.False((await other.TryAcquireAsync(options!.LeaseResource, options.LeaseTimeToLive, CancellationToken.None)).IsSuccess);

            // 只停本节点：另一个节点仍在服务，探针在排空中跑一个周期
            await first.App.StopAsync(CancellationToken.None);
        }

        Assert.Equal(2, observed.Count);
        Assert.Equal(("starting", false), (observed[0].Phase, observed[0].State.IsServing()));
        Assert.False(observed[0].Leader, "A node that is not serving must not take the leader lease.");
        Assert.Equal(("draining", LifecycleState.Draining), (observed[1].Phase, observed[1].State));
        Assert.False(observed[1].Leader, "A draining node must release the leader lease instead of keeping it.");
        Assert.True(observed[1].OtherTookOver, "Another node must take over the leader lease while this node drains.");
        Assert.DoesNotContain(logs.Entries, static entry => entry.Category.StartsWith("XiHan.Framework.Scheduling", StringComparison.Ordinal) && entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Stop_DeletesTheLeaseRow_SoAnotherNodeTakesOverAtOnce()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
        var logs = new LogCapture();
        await using var database = new SqliteDatabase();
        var first = await database.StartHostAsync(
            NodeSettings("node-a"),
            services =>
            {
                services.AddSingleton<ILoggerFactory>(logs);
                services.AddSingleton<TimeProvider>(clock);
            });
        var second = await database.StartHostAsync(NodeSettings("node-b"), services => services.AddSingleton<TimeProvider>(clock));
        var engine = first.Services.GetRequiredService<JobSchedulerEngine>();
        var options = first.Services.GetRequiredService<SchedulerOptions>();
        var other = second.Services.GetRequiredService<ILeaderElection>();
        await engine.PollOnceAsync(CancellationToken.None);
        Assert.True(engine.IsLeader);

        // 停止前没有排空中的周期：引擎停止时释放租约，时钟没动，另一个节点立刻拿到
        await first.App.StopAsync(CancellationToken.None);
        Assert.False(engine.IsLeader);
        var lease = (await other.TryAcquireAsync(options.LeaseResource, options.LeaseTimeToLive, CancellationToken.None)).ValueOrThrow();
        await lease.ReleaseAsync();
        Assert.DoesNotContain(logs.Entries, static entry => entry.Category.StartsWith("XiHan.Framework.Scheduling", StringComparison.Ordinal) && entry.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// 一个节点的配置：节点 ID，轮询周期沿用夹具的 1 小时。
    /// </summary>
    private static List<KeyValuePair<string, string?>> NodeSettings(string nodeId) =>
    [
        new($"{SchedulerOptions.ConfigurationSection}:NodeId", nodeId),
    ];
}
