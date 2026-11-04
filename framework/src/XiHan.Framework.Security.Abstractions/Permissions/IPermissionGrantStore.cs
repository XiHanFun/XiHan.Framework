// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 权限授予存储，Provider 实现它
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPermissionGrantStore
{
    /// <summary>
    /// 取主体在租户下的有效授予
    /// </summary>
    /// <remarks>
    /// 全局授予加该租户的授予；<paramref name="tenantId"/> 为 <see langword="null"/> 时只有全局授予；没有授予时是成功的空集合。存储不可达（连接、超时、事务失败）返回 <see cref="SecurityErrors.StoreUnavailable"/>，不能以空集合代替：空集合会被判定为不持有（403），故障必须以 503 结束
    /// </remarks>
    ValueTask<Result<IReadOnlyCollection<string>>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken);
}
