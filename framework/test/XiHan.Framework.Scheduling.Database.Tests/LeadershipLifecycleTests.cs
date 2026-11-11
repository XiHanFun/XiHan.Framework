// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 领导租约与应用生命周期（数据库实现）
/// </summary>
/// <remarks>
/// 两个节点共用一个 SQLite 文件与一个由用例推进的时钟，轮询周期 1 小时（后台循环不动作），周期由用例经 <see cref="JobSchedulerEngine.PollOnceAsync"/> 直接执行。停止时删行，另一个节点立刻拿到，不记告警——那时管道已拒绝调用，所以释放不经管道。启动中不拿、排空即释放由一致性套件的应用生命周期用例覆盖（<c>LeaderElectionConformance.Engine_HoldsTheLeaseOnlyWhileTheApplicationServes</c>，<see cref="DatabaseLeaderElectionConformanceTests"/>）
/// </remarks>
public sealed class LeadershipLifecycleTests
{
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
        await lease.ReleaseAsync(CancellationToken.None);
        Assert.DoesNotContain(logs.Entries, static entry => entry.Category.StartsWith("XiHan.Framework.Scheduling", StringComparison.Ordinal) && entry.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// 一个节点的配置：节点 ID，轮询周期沿用夹具的 1 小时
    /// </summary>
    private static List<KeyValuePair<string, string?>> NodeSettings(string nodeId) =>
    [
        new($"{SchedulerOptions.ConfigurationSection}:NodeId", nodeId),
    ];
}
