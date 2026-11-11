// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Authorization;

namespace XiHan.Framework.Hosting.AspNetCore.Endpoints;

/// <summary>
/// 把授权要求挂在端点元数据上
/// </summary>
/// <remarks>
/// <c>UseXiHan()</c> 在构造调用上下文时读取端点上的全部要求，Authorize 阶段把它们与操作契约的要求叠加判定：组上与端点上各加的约定都要满足，任何一条都不顶替契约的权限（ADR-0174）。要求不会被静默跳过：挂在绕过管道的端点上启动即失败，<c>UseXiHan()</c> 没判定到（没调或排在路由之前）时端点拒绝执行（ADR-0175）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    /// 端点要求已认证
    /// </summary>
    public static TBuilder RequireXiHanAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Require(builder, AuthorizationRequirement.Authenticated);
    }

    /// <summary>
    /// 端点要求权限（权限码须由某个特性登记）
    /// </summary>
    /// <remarks>
    /// <c>resource</c> 取 <c>null</c>
    /// </remarks>
    public static TBuilder RequireXiHanPermission<TBuilder>(this TBuilder builder, string permissionCode)
        where TBuilder : IEndpointConventionBuilder
        => RequireXiHanPermission<TBuilder>(builder, permissionCode, null);

    /// <summary>
    /// 端点要求权限（权限码须由某个特性登记）
    /// </summary>
    public static TBuilder RequireXiHanPermission<TBuilder>(this TBuilder builder, string permissionCode, ResourceName? resource)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Require(builder, AuthorizationRequirement.Permission(permissionCode, resource));
    }

    /// <summary>
    /// 端点要求命名策略
    /// </summary>
    /// <remarks>
    /// <c>resource</c> 取 <c>null</c>
    /// </remarks>
    public static TBuilder RequireXiHanPolicy<TBuilder>(this TBuilder builder, string policyName)
        where TBuilder : IEndpointConventionBuilder
        => RequireXiHanPolicy<TBuilder>(builder, policyName, null);

    /// <summary>
    /// 端点要求命名策略
    /// </summary>
    public static TBuilder RequireXiHanPolicy<TBuilder>(this TBuilder builder, string policyName, ResourceName? resource)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Require(builder, new AuthorizationRequirement(policyName, resource));
    }

    /// <summary>
    /// 挂上要求，并在端点执行前确认它已被判定
    /// </summary>
    /// <remarks>
    /// 守卫是最终约定：端点的处理委托生成之后才能包；同一端点多次加要求只包一层（ADR-0175）
    /// </remarks>
    private static TBuilder Require<TBuilder>(TBuilder builder, AuthorizationRequirement requirement)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(requirement);
        builder.Finally(EndpointRequirementGuard.Apply);
        return builder;
    }
}
