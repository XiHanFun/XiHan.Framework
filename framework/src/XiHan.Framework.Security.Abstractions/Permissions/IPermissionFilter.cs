// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 权限收窄
/// </summary>
/// <remarks>
/// 授予命中之后再判定一次，成功的 <see langword="false"/> 即视为不持有。用于套餐白名单（租户的有效权限与其套餐求交，持 <c>*</c> 的租户管理员也被收窄）、显式拒绝、委托到期这类"授予之外"的约束。<see cref="PermissionChecker"/> 在授予命中之后按登记顺序逐个调用全部过滤器，全部放行才算持有；匿名主体与授予未命中时不调用。按单例登记（与检查器同寿命）：<c>services.AddSingleton&lt;IPermissionFilter, EditionPermissionFilter&gt;()</c>。过滤器依赖的数据（套餐、拒绝规则、委托）读不到时返回失败（如 <see cref="SecurityErrors.StoreUnavailable"/>），检查器原样返回该失败、不再调用后面的过滤器，调用以 503 结束，不当作放行或拒绝；抛出的异常照常传播
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface IPermissionFilter
{
    /// <summary>
    /// 授予已命中 <paramref name="permission"/> 之后，判定是否仍然放行
    /// </summary>
    /// <param name="security">已认证主体与当前租户</param>
    /// <param name="permission">被判定的权限码（不是 <c>*</c>：通配命中时传入的也是具体权限码）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功的 <see langword="true"/> 放行、<see langword="false"/> 收窄；无法判定时为失败</returns>
    ValueTask<Result<bool>> AllowsAsync(SecurityContext security, string permission, CancellationToken cancellationToken);
}
