// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 健康项 <see cref="OutboxFeature.HealthName"/>：发件箱的积压与死信
/// </summary>
/// <remarks>
/// 周期探测缓存的状态加上探针最近一次数出的数字（未投递的行数、最老一条等了多久、死信行数、数过几个库）。探测失败时条目 Degraded、数字为空
/// </remarks>
/// <param name="periodic">周期探测与缓存</param>
/// <param name="probe">探针，持有最近一次数出的数字</param>
internal sealed class OutboxHealth(PeriodicHealthContributor periodic, OutboxBacklogProbe probe) : IHealthContributor
{
    /// <summary>
    /// 周期探测与缓存
    /// </summary>
    private readonly PeriodicHealthContributor _periodic = periodic ?? throw new ArgumentNullException(nameof(periodic));

    /// <summary>
    /// 探针
    /// </summary>
    private readonly OutboxBacklogProbe _probe = probe ?? throw new ArgumentNullException(nameof(probe));

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check() => [_periodic.Check()[0] with { Data = _probe.Data() }];
}
