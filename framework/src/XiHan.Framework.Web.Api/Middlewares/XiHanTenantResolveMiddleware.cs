#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanTenantResolveMiddleware
// Guid:7e0c4ced-4bb5-4e8c-95b0-d5de577f3c43
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Options;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.ConfigurationStore;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi 租户解析中间件
/// </summary>
public class XiHanTenantResolveMiddleware(
    RequestDelegate next,
    IOptions<XiHanTenantResolveOptions> options)
{
    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="currentTenant"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext httpContext, ICurrentTenant currentTenant, ITenantStore tenantStore)
    {
        var resolveContext = new TenantResolveContext(httpContext.RequestServices);
        foreach (var resolver in options.Value.TenantResolvers)
        {
            await resolver.ResolveAsync(resolveContext);
            if (resolveContext.Handled)
            {
                break;
            }
        }

        var tenantIdOrName = resolveContext.TenantIdOrName;
        if (tenantIdOrName.IsNullOrWhiteSpace() && !resolveContext.Handled)
        {
            tenantIdOrName = options.Value.FallbackTenant;
        }

        if (tenantIdOrName.IsNullOrWhiteSpace())
        {
            await next(httpContext);
            return;
        }

        var tenantKey = tenantIdOrName.Trim();
        var tenantConfiguration = await ResolveTenantAsync(tenantStore, tenantKey, httpContext.RequestAborted);
        if (tenantConfiguration is not null)
        {
            using (currentTenant.Change(tenantConfiguration.Id, tenantConfiguration.Name))
            {
                await next(httpContext);
            }

            return;
        }

        long? tenantId = long.TryParse(tenantKey, out var parsedTenantId) ? parsedTenantId : null;

        using (currentTenant.Change(tenantId, tenantKey))
        {
            await next(httpContext);
        }
    }

    private static async Task<TenantConfiguration?> ResolveTenantAsync(
        ITenantStore tenantStore,
        string tenantIdOrName,
        CancellationToken cancellationToken)
    {
        if (long.TryParse(tenantIdOrName, out var tenantId))
        {
            var tenantById = await tenantStore.FindAsync(tenantId, cancellationToken);
            if (tenantById is not null)
            {
                return tenantById;
            }
        }

        return await tenantStore.FindAsync(tenantIdOrName, cancellationToken);
    }

    private sealed class TenantResolveContext(IServiceProvider serviceProvider) : ITenantResolveContext
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public string? TenantIdOrName { get; set; }

        public bool Handled { get; set; }
    }
}
