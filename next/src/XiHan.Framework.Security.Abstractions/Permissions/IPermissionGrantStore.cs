// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 权限授予存储，Provider 实现它。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPermissionGrantStore
{
    /// <summary>
    /// 取主体在租户下的有效授予：全局授予加该租户的授予；<paramref name="tenantId"/> 为 <see langword="null"/> 时只有全局授予。
    /// </summary>
    ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(GrantSubject subject, string? tenantId, CancellationToken cancellationToken);
}
