// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 让 <see cref="InMemoryLeaderElection"/> 跑完整套 <see cref="LeaderElectionConformance"/> 用例
/// </summary>
/// <remarks>
/// 应用生命周期用例的节点是装配了内存调度的宿主；进程内的选举只在本进程里有效，竞争者就是节点自己的选举实例——它不分持有者，节点持有时谁再获取都被拒
/// </remarks>
public sealed class InMemoryLeaderElectionConformanceTests : LeaderElectionConformance
{
    /// <inheritdoc />
    protected override ILeaderElection CreateElection(TimeProvider clock) => new InMemoryLeaderElection(clock);

    /// <inheritdoc />
    protected override async Task<(ILeaderElection Competitor, IAsyncDisposable Host)> StartNodeAsync(TimeProvider clock, IFeature probe)
    {
        var host = await TestHost.StartAsync(
            b => b.UseFeature<InMemorySchedulingFeature>().UseFeature(probe),
            [new($"{SchedulerOptions.ConfigurationSection}:PollIntervalSeconds", "3600")],
            services => services.AddSingleton(clock));
        return (host.Services.GetRequiredService<ILeaderElection>(), host);
    }
}
