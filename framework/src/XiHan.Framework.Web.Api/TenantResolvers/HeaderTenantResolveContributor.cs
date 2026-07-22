// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
