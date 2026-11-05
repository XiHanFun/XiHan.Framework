// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Scheduling.InMemory;
using XiHan.Framework.Scheduling.Kubernetes.Leases;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Scheduling.Kubernetes.Tests;

/// <summary>
/// 让 <see cref="KubernetesLeaderElection"/>跑完整套 <see cref="LeaderElectionConformance"/> 用例
/// </summary>
/// <remarks>
/// 对着内存 API Server 桩。Lease 只有整数秒，存活时间取 1 秒。应用生命周期用例的节点是装配了内存调度与 Kubernetes 选举的宿主（引擎用 Kubernetes 选举），竞争者是同一个桩上另一个持有者的选举
/// </remarks>
public sealed class KubernetesLeaderElectionConformanceTests : LeaderElectionConformance
{
    /// <inheritdoc />
    protected override TimeSpan LeaseTimeToLive => TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    protected override ILeaderElection CreateElection(TimeProvider clock)
    {
        var options = new KubernetesSchedulingOptions { Lease = "conformance", Namespace = "tests", Holder = "pod-" + Guid.NewGuid().ToString("N")[..6], Token = FakeLeaseApi.Token, ApiServer = new Uri("https://api.test") };
        var client = new KubernetesLeaseClient(new HttpClient(new FakeLeaseApi()) { BaseAddress = options.ApiServer }, options);
        return new KubernetesLeaderElection(client, options, clock, NullLoggerFactory.Instance);
    }

    /// <inheritdoc />
    protected override async Task<(ILeaderElection Competitor, IAsyncDisposable Host)> StartNodeAsync(TimeProvider clock, IFeature probe)
    {
        // 节点与竞争者共用一个桩：同一个 Lease 名，持有者不同
        var api = new FakeLeaseApi();
        var node = new KubernetesSchedulingOptions { Lease = "conformance", Namespace = "tests", Holder = "pod-node", Token = FakeLeaseApi.Token, ApiServer = new Uri("https://api.test") };
        var host = await TestHost.StartAsync(
            b => b.UseFeature<InMemorySchedulingFeature>().UseFeature<KubernetesSchedulingFeature>().UseFeature(probe),
            [
                new($"{SchedulerOptions.ConfigurationSection}:PollIntervalSeconds", "3600"),
                new("XiHan:Scheduling:Kubernetes:Lease", node.Lease),
                new("XiHan:Scheduling:Kubernetes:Namespace", node.Namespace),
                new("XiHan:Scheduling:Kubernetes:Holder", node.Holder),
            ],
            services =>
            {
                services.AddSingleton(clock);
                // 预先注册的客户端替代集群内构造；令牌与 API Server 指向桩
                services.AddSingleton(new KubernetesLeaseClient(new HttpClient(api) { BaseAddress = node.ApiServer }, node));
            });
        var rival = node with { Holder = "pod-rival" };
        var competitor = new KubernetesLeaderElection(new KubernetesLeaseClient(new HttpClient(api) { BaseAddress = rival.ApiServer }, rival), rival, clock, NullLoggerFactory.Instance);
        return (competitor, host);
    }
}
