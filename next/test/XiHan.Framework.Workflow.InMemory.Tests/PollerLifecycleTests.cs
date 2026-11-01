// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Engine;

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
}
