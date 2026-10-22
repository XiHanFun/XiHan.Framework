// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Builder;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;

namespace XiHan.Framework.Hosting.AspNetCore;

/// <summary>
/// 把授权要求挂在端点元数据上；<c>UseXiHan()</c> 在构造调用上下文时读取它，Authorize 阶段据此判定。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    /// 端点要求已认证。
    /// </summary>
    public static TBuilder RequireXiHanAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(AuthorizationRequirement.Authenticated);
    }

    /// <summary>
    /// 端点要求权限（权限码须由某个特性登记）。
    /// </summary>
    public static TBuilder RequireXiHanPermission<TBuilder>(this TBuilder builder, string permissionCode, ResourceName? resource = null)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(AuthorizationRequirement.Permission(permissionCode, resource));
    }

    /// <summary>
    /// 端点要求命名策略。
    /// </summary>
    public static TBuilder RequireXiHanPolicy<TBuilder>(this TBuilder builder, string policyName, ResourceName? resource = null)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new AuthorizationRequirement(policyName, resource));
    }
}
