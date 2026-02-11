#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanRequestLoggingMiddleware
// Guid:f6d7b9e2-0360-4184-94ca-3e0f61d9f988
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Web.Api.Constants;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi 请求日志中间件
/// </summary>
public class XiHanRequestLoggingMiddleware(RequestDelegate next, ILogger<XiHanRequestLoggingMiddleware> logger)
{
    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = context.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
            ?? context.TraceIdentifier;

        var startAt = DateTimeOffset.UtcNow;
        logger.LogInformation(
            "请求开始: {Method} {Path}, TraceId: {TraceId}, ClientIP: {ClientIP}",
            context.Request.Method,
            context.Request.Path,
            traceId,
            context.Connection.RemoteIpAddress);

        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMs = (DateTimeOffset.UtcNow - startAt).TotalMilliseconds;
            logger.LogInformation(
                "请求结束: {Method} {Path}, TraceId: {TraceId}, StatusCode: {StatusCode}, 耗时: {Elapsed}ms",
                context.Request.Method,
                context.Request.Path,
                traceId,
                context.Response.StatusCode,
                elapsedMs);
        }
    }
}
