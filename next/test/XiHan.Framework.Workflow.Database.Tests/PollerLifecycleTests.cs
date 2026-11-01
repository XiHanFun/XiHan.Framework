// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Storage;
using XiHan.Framework.Workflow.Database.Polling;
using XiHan.Framework.Workflow.Database.Storage;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 到期书签轮询与应用生命周期（数据库实现）。
/// </summary>
/// <remarks>
/// 领导节点每个周期调用 <see cref="DueBookmarkPoller.RunCycleAsync"/>：应用启动期间与排空期间不在服务状态，周期什么也不做、不记告警，进入服务状态后照常轮询；周期中途进入排空的，剩下的分区不再调用、不告警。轮询循环（<see cref="DueBookmarkPollingLoop.RunCycleAsync"/>）在这两个阶段不拿也不续领导权，持有就释放，另一个实例随即接手。探针排在数据库工作流之后启停，在这两个阶段各跑一个周期，不靠计时器与真实等待。
/// </remarks>
public sealed class PollerLifecycleTests
{
    [Fact]
    public async Task Cycles_DoNothingAndLogNoWarnings_WhileTheAppIsNotServing()
    {
        var logs = new LogCapture();
        var cycles = new List<(LifecycleState State, int Resumed)>();
        DueBookmarkPoller? poller = null;
        App? app = null;
        // 依赖 workflow.execution：它在数据库工作流（已起轮询循环）之后启动、之前停止，两次都正赶上应用不在服务状态
        var probe = new StubFeature(TestManifests.Create("xihan.test.workflow-probe", requires: ["workflow.execution@1"]))
        {
            OnStart = async (services, cancellationToken) =>
            {
                poller = services.GetRequiredService<DueBookmarkPoller>();
                app = services.GetRequiredService<App>();
                cycles.Add((app.State, await poller.RunCycleAsync(10, cancellationToken)));
            },
            OnStop = async cancellationToken => cycles.Add((app!.State, await poller!.RunCycleAsync(10, cancellationToken))),
        };

        await using (var database = new SqliteDatabase())
        {
            var host = await database.StartHostAsync(
                [new($"{WorkflowOptions.ConfigurationSection}:RetentionDays", "1")],
                services => services.AddSingleton<ILoggerFactory>(logs),
                b => b.UseFeature(probe));
            Assert.True(host.App.State.IsServing());
            Assert.True(host.Services.GetRequiredService<DueBookmarkPollingLoop>().IsRunning);
            Assert.Equal(0, await poller!.RunCycleAsync(10, CancellationToken.None));
        }

        Assert.Equal(2, cycles.Count);
        Assert.All(cycles, static cycle => Assert.False(cycle.State.IsServing()));
        Assert.Equal(LifecycleState.Draining, cycles[1].State);
        Assert.All(cycles, static cycle => Assert.Equal(0, cycle.Resumed));
        Assert.DoesNotContain(logs.Entries, static entry => entry.Category.StartsWith("XiHan.Framework.Workflow", StringComparison.Ordinal) && entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Leadership_IsNotTakenWhileStarting_AndIsReleasedWhileDraining_SoAnotherInstanceTakesOver()
    {
        // 租约的到期、续约点都按注入的时钟判定；周期由用例直接调用，不靠计时器
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var lease = TimeSpan.FromSeconds(30);
        var logs = new LogCapture();
        DueBookmarkPollingLoop? loop = null;
        PollerLeadership? other = null;
        App? app = null;
        var observed = new List<(string Phase, LifecycleState State, bool Leader, bool OtherTookOver)>();
        // 依赖 workflow.execution：在数据库工作流（已起循环）之后启动、之前停止，两次都正赶上应用不在服务状态
        var probe = new StubFeature(TestManifests.Create("xihan.test.workflow-leadership", requires: ["workflow.execution@1"]))
        {
            OnStart = async (services, cancellationToken) =>
            {
                loop = services.GetRequiredService<DueBookmarkPollingLoop>();
                app = services.GetRequiredService<App>();
                // 另一个实例：同一张租约表、另一个持有者
                other = new PollerLeadership(services, services.GetRequiredService<DatabaseWorkflowOptions>() with { HolderId = "node-2" }, clock, logs);
                await loop.RunCycleAsync(cancellationToken);
                observed.Add(("starting", app.State, loop.IsLeader, false));
            },
            OnStop = async cancellationToken =>
            {
                // 排空中：时钟过了续约点（三分之一租约），这个周期若还持有就会续约
                clock.Advance(lease / 2);
                await loop!.RunCycleAsync(cancellationToken);
                var tookOver = await other!.TryHoldAsync(cancellationToken);
                observed.Add(("draining", app!.State, loop.IsLeader, tookOver));
                await other.ReleaseAsync(cancellationToken);
            },
        };

        await using (var database = new SqliteDatabase())
        {
            var host = await database.StartHostAsync(
                [new($"{DatabaseWorkflowOptions.ConfigurationSection}:LockLeaseSeconds", "30")],
                services =>
                {
                    services.AddSingleton<ILoggerFactory>(logs);
                    services.AddSingleton<TimeProvider>(clock);
                },
                b => b.UseFeature(probe));

            // 服务中：本节点拿到领导权，另一个实例拿不到
            Assert.True(host.App.State.IsServing());
            await loop!.RunCycleAsync(CancellationToken.None);
            Assert.True(loop.IsLeader);
            Assert.False(await other!.TryHoldAsync(CancellationToken.None));
        }

        Assert.Equal(2, observed.Count);
        Assert.Equal(("draining", LifecycleState.Draining), (observed[1].Phase, observed[1].State));
        Assert.False(observed[1].Leader, "A draining node must release the poller leadership instead of renewing it.");
        Assert.True(observed[1].OtherTookOver, "Another instance must take over the leadership while this node drains.");
        Assert.Equal(("starting", false), (observed[0].Phase, observed[0].State.IsServing()));
        Assert.False(observed[0].Leader, "A node that is not serving must not take the poller leadership.");
        Assert.DoesNotContain(logs.Entries, static entry => entry.Category.StartsWith("XiHan.Framework.Workflow", StringComparison.Ordinal) && entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Cycle_ThatLeavesTheServingStateMidway_StopsQuietly_AndTheNextCycleReleasesTheLeadership()
    {
        var logs = new LogCapture();
        var scanned = new List<string?>();
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TestHost? host = null;
        Task? stopping = null;
        // 依赖 workflow.execution：停机时它先停，停在闸门前，应用就一直处在排空中
        var probe = new StubFeature(TestManifests.Create("xihan.test.workflow-drain", requires: ["workflow.execution@1"]))
        {
            OnStop = async _ =>
            {
                stopEntered.SetResult();
                await stopGate.Task;
            },
        };

        await using var database = new SqliteDatabase();
        host = await database.StartHostAsync(
            [
                // 平台加三个租户（与平台共库）：一个周期四个分区；开着保留期清理，清理的分区调用同样会被拒
                new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme"),
                new("XiHan:MultiTenancy:Tenants:contoso:DisplayName", "Contoso"),
                new("XiHan:MultiTenancy:Tenants:globex:DisplayName", "Globex"),
                new($"{WorkflowOptions.ConfigurationSection}:RetentionDays", "1"),
            ],
            services =>
            {
                services.AddSingleton<ILoggerFactory>(logs);
                services.AddScoped<IWorkflowBookmarkStore>(sp => new ScanHookBookmarkStore(sp.GetRequiredService<DatabaseWorkflowBookmarkStore>(), async tenantId =>
                {
                    scanned.Add(tenantId);
                    if (stopping is null)
                    {
                        // 第一个分区（平台）的扫描进行中开始停机：周期开头还在服务状态、已拿到领导权，之后的分区调用都被管道拒绝
                        stopping = host!.App.StopAsync(CancellationToken.None);
                        await stopEntered.Task;
                    }
                }));
            },
            b => b.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(probe));

        var loop = host.Services.GetRequiredService<DueBookmarkPollingLoop>();
        Assert.True(host.App.State.IsServing());
        Assert.Equal(0, await loop.RunCycleAsync(CancellationToken.None));
        Assert.Equal(LifecycleState.Draining, host.App.State);
        Assert.True(loop.IsLeader);

        // 下一个周期应用仍在排空：释放领导权（第 7 条），也不再扫描
        Assert.Equal(0, await loop.RunCycleAsync(CancellationToken.None));
        Assert.False(loop.IsLeader);
        stopGate.SetResult();
        await stopping!;

        Assert.Equal([null], scanned);
        // 应用在周期中途离开服务状态：剩下的分区与清理不再一个个告警（库并没有不可达）
        Assert.DoesNotContain(logs.Entries, static entry => entry.Category.StartsWith("XiHan.Framework.Workflow", StringComparison.Ordinal) && entry.Level >= LogLevel.Warning);
    }
}
