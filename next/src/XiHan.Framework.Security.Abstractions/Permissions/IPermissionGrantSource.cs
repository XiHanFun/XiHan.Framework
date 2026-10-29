// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 授予来源。
/// </summary>
/// <remarks>
/// 主体在授予存储之外的额外授予，与 <see cref="IPermissionFilter"/> 对称——过滤器只能拿走，来源只能加。用于角色继承（祖先角色的授予传给子角色的持有者）、限时委托（被委托人在有效期内拿到委托人的权限码）这类"不落在主体自己授予行上"的授予。<see cref="PermissionChecker"/> 在用户与角色的存储授予都没命中时按登记顺序逐个查询来源，任一来源命中即算授予命中，之后照常经全部过滤器收窄；匿名主体不查询。按单例登记（与检查器同寿命）：<c>services.AddSingleton&lt;IPermissionGrantSource, RoleInheritanceGrantSource&gt;()</c>。来源抛出的异常照常传播，不当作没有授予。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPermissionGrantSource
{
    /// <summary>
    /// 主体在当前租户下的额外授予。
    /// </summary>
    /// <remarks>
    /// 权限码或 <see cref="PermissionGrant.All"/>；没有时返回空集合。
    /// </remarks>
    /// <param name="security">已认证主体与当前租户。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    ValueTask<IReadOnlyCollection<string>> GetGrantsAsync(SecurityContext security, CancellationToken cancellationToken);
}
