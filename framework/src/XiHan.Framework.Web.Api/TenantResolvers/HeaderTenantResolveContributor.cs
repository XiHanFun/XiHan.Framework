#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HeaderTenantResolveContributor
// Guid:1943d0cb-4547-4f16-9db7-0cd8eb09c6f7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Web.Api.TenantResolvers;

/// <summary>
/// 基于 Header 的租户解析贡献者
/// </summary>
public class HeaderTenantResolveContributor : TenantResolveContributorBase
{
    /// <summary>
    /// 名称
    /// </summary>
    public override string Name => "Header";

    /// <summary>
    /// 解析租户
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public override Task ResolveAsync(ITenantResolveContext context)
    {
        var options = context.ServiceProvider.GetRequiredService<IOptions<XiHanTenantResolveOptions>>();
        var resolveOptions = options.Value;
        if (!resolveOptions.EnableHeaderResolve || resolveOptions.HeaderKeys.Length == 0)
        {
            return Task.CompletedTask;
        }

        var httpContextAccessor = context.ServiceProvider.GetService<IHttpContextAccessor>();
        var request = httpContextAccessor?.HttpContext?.Request;
        if (request is null)
        {
            return Task.CompletedTask;
        }

        foreach (var key in resolveOptions.HeaderKeys)
        {
            if (key.IsNullOrWhiteSpace())
            {
                continue;
            }

            var value = request.Headers[key].FirstOrDefault();
            if (value.IsNullOrWhiteSpace())
            {
                continue;
            }

            context.TenantIdOrName = value;
            context.Handled = true;
            break;
        }

        return Task.CompletedTask;
    }
}
