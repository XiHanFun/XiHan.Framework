// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 健康快照，直接映射 Kubernetes 的三种探针。
/// </summary>
/// <param name="Started">startup 探针：所有特性都已到达 <see cref="LifecycleState.Started"/> 之后的状态。</param>
/// <param name="Ready">readiness 探针：应用处于 <see cref="LifecycleState.Ready"/> 或 <see cref="LifecycleState.Degraded"/>。</param>
/// <param name="Live">liveness 探针：应用未处于 <see cref="LifecycleState.Failed"/>。</param>
/// <param name="State">应用聚合状态（含 <see cref="IHealthContributor"/> 条目的影响）。</param>
/// <param name="Features">各特性状态。</param>
/// <param name="Contributions">各 <see cref="IHealthContributor"/> 条目的状态。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record HealthSnapshot(bool Started, bool Ready, bool Live, LifecycleState State, IReadOnlyList<FeatureStatus> Features, IReadOnlyList<ContributorStatus> Contributions);
