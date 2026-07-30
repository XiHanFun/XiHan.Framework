// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Web.Api.RateLimiting;

/// <summary>
/// 入站限流服务集合扩展
/// </summary>
public static class XiHanRateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒入站限流（按客户端 IP 固定窗口；仅启用时注册限流器）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(XiHanRateLimitingOptions.SectionName);
        services.Configure<XiHanRateLimitingOptions>(section);

        var options = section.Get<XiHanRateLimitingOptions>() ?? new XiHanRateLimitingOptions();
        if (!options.IsEnabled)
        {
            return services;
        }

        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiterOptions.OnRejected = static async (rejectedContext, cancellationToken) =>
            {
                if (rejectedContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    rejectedContext.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                rejectedContext.HttpContext.Response.ContentType = "application/json; charset=utf-8";
                await rejectedContext.HttpContext.Response.WriteAsync(
                    "{\"success\":false,\"message\":\"请求过于频繁，请稍后再试。\"}", cancellationToken);
            };

            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (IsExempt(httpContext.Request.Path, options.ExemptPathPrefixes))
                {
                    return RateLimitPartition.GetNoLimiter("xihan-rate-limit-exempt");
                }

                var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.WindowSeconds),
                    QueueLimit = options.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
            });
        });

        return services;
    }

    private static bool IsExempt(PathString path, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (!string.IsNullOrEmpty(prefix) && path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
