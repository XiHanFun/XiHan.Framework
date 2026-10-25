// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Scheduling.Kubernetes.Leases;

/// <summary>
/// Lease 的规约：持有者、存活秒数、获取与续约时刻（MicroTime 文本）、交接次数。
/// </summary>
internal sealed class LeaseSpec
{
    /// <summary>
    /// 持有者标识；空表示无人持有。
    /// </summary>
    public string? HolderIdentity { get; set; }

    /// <summary>
    /// 存活秒数（整数）。
    /// </summary>
    public int? LeaseDurationSeconds { get; set; }

    /// <summary>
    /// 获取时刻。
    /// </summary>
    public string? AcquireTime { get; set; }

    /// <summary>
    /// 最近续约时刻。
    /// </summary>
    public string? RenewTime { get; set; }

    /// <summary>
    /// 持有者变更次数。
    /// </summary>
    public int? LeaseTransitions { get; set; }
}
