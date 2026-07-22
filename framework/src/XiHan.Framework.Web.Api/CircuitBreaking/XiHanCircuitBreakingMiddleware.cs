// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace XiHan.Framework.Web.Api.CircuitBreaking;

/// <summary>
/// 入站熔断中间件
/// </summary>
/// <remarks>
/// 置于限流之后、鉴权之前：请求前查询熔断器状态，熔断期直接返回 503 + Retry-After；
/// 请求后按响应 5xx 或未处理异常计失败（异常记录后原样重抛，交由异常处理管线），其余计成功。
/// 豁免路径前缀直接放行且不参与统计。仅 IsEnabled=true 时由模块接入本中间件。
/// </remarks>
/// <param name="next">下一个中间件</param>
/// <param name="state">熔断器状态单例</param>
/// <param name="options">熔断选项</param>
public class XiHanCircuitBreakingMiddleware(RequestDelegate next, XiHanCircuitBreakerState state, IOptions<XiHanCircuitBreakingOptions> options)
{
    private readonly string[] _exemptPathPrefixes = options.Value.ExemptPathPrefixes;

    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="context">请求上下文</param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsExempt(context.Request.Path, _exemptPathPrefixes))
        {
            await next(context);
            return;
        }

        if (!state.TryPass(out var isProbe, out var retryAfterSeconds))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(
                "{\"success\":false,\"message\":\"服务暂时过载，请稍后再试。\"}", context.RequestAborted);
            return;
        }

        try
        {
            await next(context);
            state.Record(context.Response.StatusCode < StatusCodes.Status500InternalServerError, isProbe);
        }
        catch
        {
            // 未处理异常计失败后必须重抛，交由上游异常日志/处理中间件继续处理
            state.Record(false, isProbe);
            throw;
        }
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
