#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CurrentUserTenantResolveContributor
// Guid:cc840858-cac2-436d-ae48-c97e774f52fd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 7:03:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
        if (currentUser.IsAuthenticated)
        {
            context.Handled = true;
            context.TenantIdOrName = currentUser.TenantId?.ToString();
        }

        return Task.CompletedTask;
    }
}
