// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Security.InMemory;

/// <summary>
/// 健康项 <see cref="InMemorySecurityFeature.HealthName"/>：主体存储或授予存储任一满即 Degraded（再登记会被 <see cref="SecurityErrors.CapacityExceeded"/> 拒绝）。
/// 两个存储只在对应能力被本包提供时存在，缺的那个不参与判定。同步计算，不起后台循环。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemorySecurityHealthContributor : IHealthContributor
{
    /// <summary>
    /// 主体存储；认证能力被别的 Provider 提供时为 <see langword="null"/>。
    /// </summary>
    private readonly InMemoryPrincipalStore? _principals;

    /// <summary>
    /// 授予存储；授权能力被别的 Provider 提供时为 <see langword="null"/>。
    /// </summary>
    private readonly InMemoryPermissionGrantStore? _grants;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemorySecurityHealthContributor(InMemoryPrincipalStore? principals, InMemoryPermissionGrantStore? grants)
    {
        _principals = principals;
        _grants = grants;
    }

    /// <inheritdoc />
    public IReadOnlyList<ContributorStatus> Check()
    {
        // 先看主体再看授予：两者都满时报主体存储，报告稳定
        if (_principals is { } principals && principals.Count >= principals.Capacity)
        {
            return [new ContributorStatus(InMemorySecurityFeature.HealthName, LifecycleState.Degraded, Error.Unavailable(SecurityErrors.CapacityExceeded, SecurityMessages.PrincipalStoreCapacityExceeded, MessageArguments.Of(("capacity", principals.Capacity))))];
        }

        if (_grants is { } grants && grants.Count >= grants.Capacity)
        {
            return [new ContributorStatus(InMemorySecurityFeature.HealthName, LifecycleState.Degraded, Error.Unavailable(SecurityErrors.CapacityExceeded, SecurityMessages.GrantStoreCapacityExceeded, MessageArguments.Of(("capacity", grants.Capacity))))];
        }

        return [new ContributorStatus(InMemorySecurityFeature.HealthName, LifecycleState.Ready)];
    }
}
