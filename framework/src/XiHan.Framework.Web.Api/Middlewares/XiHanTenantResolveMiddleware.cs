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

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.MultiTenancy;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi 租户解析中间件
/// </summary>
public class XiHanTenantResolveMiddleware(
    RequestDelegate next,
    IOptions<XiHanTenantResolveOptions> options,
    ILogger<XiHanTenantResolveMiddleware> logger)
{
    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="currentTenant"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext httpContext, ICurrentTenant currentTenant)
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
        var tenantId = ResolveTenantId(tenantKey);
        if (!long.TryParse(tenantKey, out _))
        {
            logger.LogDebug(
                "租户标识 {TenantKey} 非 long，已映射为稳定 long {TenantId}",
                tenantKey,
                tenantId);
        }

        using (currentTenant.Change(tenantId, tenantKey))
        {
            await next(httpContext);
        }
    }

    private static long ResolveTenantId(string tenantIdOrName)
    {
        if (long.TryParse(tenantIdOrName, out var longTenantId))
        {
            return longTenantId;
        }

        return ConvertStringToStableLong(tenantIdOrName);
    }

    private static long ConvertStringToStableLong(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt64(bytes, 0) & long.MaxValue;
    }

    private sealed class TenantResolveContext(IServiceProvider serviceProvider) : ITenantResolveContext
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public string? TenantIdOrName { get; set; }

        public bool Handled { get; set; }
    }
}
