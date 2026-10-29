// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tenants;

/// <summary>
/// 租户存储，Provider 实现它。
/// </summary>
/// <remarks>
/// ID 区分大小写。基础设施失败抛出异常，不伪装成不存在。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface ITenantStore
{
    /// <summary>
    /// 按 ID 查找。
    /// </summary>
    ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// 枚举全部租户。
    /// </summary>
    ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken);
}
