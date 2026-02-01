#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RequestTracingMiddleware
// Guid:3a4b5c6d-7e8f-9a0b-1c2d-3e4f5a6b7c8d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Web.Gateway.Constants;

namespace XiHan.Framework.Web.Gateway.Middlewares;

/// <summary>
/// 请求追踪中间件
/// </summary>
/// <remarks>
/// 职责：
/// 1. 注入 TraceId
/// 2. 记录请求日志
/// 3. 记录响应时间
/// </remarks>
public class RequestTracingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTracingMiddleware> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public RequestTracingMiddleware(RequestDelegate next, ILogger<RequestTracingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// 执行中间件
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // 获取或生成 TraceId
        var traceId = context.Request.Headers["X-Trace-Id"].FirstOrDefault()
            ?? context.TraceIdentifier;

        // 注入到 Response Header
        context.Response.Headers["X-Trace-Id"] = traceId;
        context.Items[GatewayConstants.TraceIdKey] = traceId;

        // 记录请求开始
        var startTime = DateTime.UtcNow;
        _logger.LogInformation(
            "请求开始: {Method} {Path}, TraceId: {TraceId}, ClientIP: {ClientIP}",
            context.Request.Method,
            context.Request.Path,
            traceId,
            context.Connection.RemoteIpAddress);

        try
        {
            await _next(context);
        }
        finally
        {
            // 记录请求结束
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "请求结束: {Method} {Path}, TraceId: {TraceId}, StatusCode: {StatusCode}, 耗时: {Elapsed}ms",
                context.Request.Method,
                context.Request.Path,
                traceId,
                context.Response.StatusCode,
                elapsed.TotalMilliseconds);
        }
    }
}
