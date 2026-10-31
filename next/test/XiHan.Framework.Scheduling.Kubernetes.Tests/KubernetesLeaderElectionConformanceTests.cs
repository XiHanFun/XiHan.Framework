// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Conformance;
using XiHan.Framework.Scheduling.Kubernetes.Leases;

namespace XiHan.Framework.Scheduling.Kubernetes.Tests;

/// <summary>
/// 让 <see cref="KubernetesLeaderElection"/>跑完整套 <see cref="LeaderElectionConformance"/> 用例。
/// </summary>
/// <remarks>
/// 对着内存 API Server 桩。Lease 只有整数秒，存活时间取 1 秒。
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
}
