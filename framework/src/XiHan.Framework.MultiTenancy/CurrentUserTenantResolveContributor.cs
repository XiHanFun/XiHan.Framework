// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 当前用户租户解析贡献者
/// </summary>
/// <remarks>
/// 已认证请求的租户只以令牌为准：带租户声明即该租户，不带即宿主（平台）。两种情况都短路解析链——
/// 请求头、查询参数这类外部输入不得覆盖已认证身份的租户，否则宿主身份可以逐请求自选任意租户上下文，绕过成员关系。
/// 未认证请求原样放行，交给后续贡献者（如租户专属登录页按请求头或域名识别租户）。
/// </remarks>
public class CurrentUserTenantResolveContributor : TenantResolveContributorBase
{
    /// <summary>
    /// 贡献者名称
    /// </summary>
    public const string ContributorName = "CurrentUser";

    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => ContributorName;

    /// <summary>
    /// 解析租户
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override Task ResolveAsync(ITenantResolveContext context)
    {
        var currentUser = context.ServiceProvider.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated)
        {
            return Task.CompletedTask;
        }

        context.TenantIdOrName = currentUser.TenantId?.ToString();
        context.Handled = true;

        return Task.CompletedTask;
    }
}
