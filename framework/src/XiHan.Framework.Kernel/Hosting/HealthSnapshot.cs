// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 健康快照，直接映射 Kubernetes 的三种探针
/// </summary>
/// <remarks>
/// 成员都经初始化器给出，以后加字段只加属性（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record HealthSnapshot
{
    /// <summary>
    /// startup 探针：所有特性都已到达 <see cref="LifecycleState.Started"/> 之后的状态
    /// </summary>
    public required bool Started { get; init; }

    /// <summary>
    /// readiness 探针：应用处于 <see cref="LifecycleState.Ready"/> 或 <see cref="LifecycleState.Degraded"/>
    /// </summary>
    public required bool Ready { get; init; }

    /// <summary>
    /// liveness 探针：应用未处于 <see cref="LifecycleState.Failed"/>
    /// </summary>
    public required bool Live { get; init; }

    /// <summary>
    /// 应用聚合状态（含 <see cref="IHealthContributor"/> 条目的影响）
    /// </summary>
    public required LifecycleState State { get; init; }

    /// <summary>
    /// 各特性状态
    /// </summary>
    public required IReadOnlyList<FeatureStatus> Features { get; init; }

    /// <summary>
    /// 各 <see cref="IHealthContributor"/> 条目的状态
    /// </summary>
    public required IReadOnlyList<ContributorStatus> Contributions { get; init; }
}
