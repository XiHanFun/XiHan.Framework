// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.MultiTenancy;

/// <summary>
/// 当前用户租户解析贡献者
/// </summary>
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
        if (!currentUser.IsAuthenticated || !currentUser.TenantId.HasValue)
        {
            return Task.CompletedTask;
        }

        context.TenantIdOrName = currentUser.TenantId.Value.ToString();
        context.Handled = true;

        return Task.CompletedTask;
    }
}
