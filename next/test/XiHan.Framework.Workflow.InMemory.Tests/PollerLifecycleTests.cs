// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Engine;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.InMemory.Tests;

/// <summary>
/// 到期书签轮询与应用生命周期。
/// </summary>
/// <remarks>
/// 应用启动期间与排空期间不在服务状态，管道不接受调用：轮询的周期什么也不做、不记告警，进入服务状态后照常轮询。用一个排在内存工作流之后启停的探针在这两个阶段各跑一个周期，不靠计时器与真实等待。
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
        // 依赖 workflow.execution：它在内存工作流（已起轮询循环）之后启动、之前停止，两次都正赶上应用不在服务状态
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

        await using (var host = await TestHost.StartAsync(
            b => b.UseFeature<InMemoryWorkflowFeature>().UseFeature(probe),
            // 开着保留期清理：清理同样只在服务状态下进行
            [new($"{WorkflowOptions.ConfigurationSection}:RetentionDays", "1")],
            services => services.AddSingleton<ILoggerFactory>(logs)))
        {
            Assert.True(host.App.State.IsServing());
            Assert.Equal(0, await poller!.RunCycleAsync(10, CancellationToken.None));
        }

        Assert.Equal(2, cycles.Count);
        Assert.All(cycles, static cycle => Assert.False(cycle.State.IsServing()));
        Assert.Equal(LifecycleState.Draining, cycles[1].State);
        Assert.All(cycles, static cycle => Assert.Equal(0, cycle.Resumed));
        Assert.DoesNotContain(logs.Entries, static entry => entry.Category.EndsWith(nameof(DueBookmarkPoller), StringComparison.Ordinal) && entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task Cycle_ThatLeavesTheServingStateMidway_StopsQuietly()
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

        host = await TestHost.StartAsync(
            b => b.UseFeature<InMemoryWorkflowFeature>().UseFeature(probe),
            // 内置循环一小时一个刻度，周期由用例直接调用；开着保留期清理，清理的分区调用同样会被拒
            [new($"{WorkflowOptions.ConfigurationSection}:RetentionDays", "1"), new("XiHan:Workflow:InMemory:PollIntervalSeconds", "3600")],
            services =>
            {
                services.AddSingleton<ILoggerFactory>(logs);
                // 平台加三个租户：一个周期四个分区
                services.AddSingleton<ITenantStore>(new FixedTenants("acme", "contoso", "globex"));
                services.AddSingleton<IWorkflowBookmarkStore>(sp => new ScanHookBookmarkStore(sp.GetRequiredService<InMemoryWorkflowBookmarkStore>(), async tenantId =>
                {
                    scanned.Add(tenantId);
                    if (stopping is null)
                    {
                        // 第一个分区（平台）的扫描进行中开始停机：周期开头还在服务状态，之后的分区调用都被管道拒绝
                        stopping = host!.App.StopAsync(CancellationToken.None);
                        await stopEntered.Task;
                    }
                }));
            });
        await using (host)
        {
            Assert.True(host.App.State.IsServing());
            var resumed = await host.Services.GetRequiredService<DueBookmarkPoller>().RunCycleAsync(10, CancellationToken.None);
            Assert.Equal(LifecycleState.Draining, host.App.State);
            stopGate.SetResult();
            await stopping!;

            Assert.Equal(0, resumed);
            Assert.Equal([null], scanned);
        }

        // 应用在周期中途离开服务状态：剩下的分区不再一个个告警（库并没有不可达）
        Assert.DoesNotContain(logs.Entries, static entry => entry.Category.EndsWith(nameof(DueBookmarkPoller), StringComparison.Ordinal) && entry.Level >= LogLevel.Warning);
    }
}
