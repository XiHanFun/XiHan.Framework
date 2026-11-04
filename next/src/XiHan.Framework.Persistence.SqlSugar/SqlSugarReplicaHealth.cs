// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 从库的健康条目
/// </summary>
/// <remarks>
/// 周期探测的结果加上每个从库的情况：有从库不可读时条目 Degraded，读仍然在别的从库或主库上
/// </remarks>
internal sealed class SqlSugarReplicaHealth(PeriodicHealthContributor probe, SqlSugarReplicaMonitor monitor) : IHealthContributor
{
    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check() => [probe.Check()[0] with { Data = monitor.Data() }];
}
