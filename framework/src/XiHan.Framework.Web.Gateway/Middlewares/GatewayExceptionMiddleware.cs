#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GatewayExceptionMiddleware
// Guid:4b5c6d7e-8f9a-0b1c-2d3e-4f5a6b7c8d9e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Web.Gateway.Constants;
using XiHan.Framework.Web.Gateway.Models;

namespace XiHan.Framework.Web.Gateway.Middlewares;

/// <summary>
/// 网关异常处理中间件
/// </summary>
/// <remarks>
/// 职责：
/// 1. 捕获所有未处理的异常
/// 2. 规范化响应格式
/// 3. 记录异常日志
/// </remarks>
public class GatewayExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayExceptionMiddleware> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public GatewayExceptionMiddleware(RequestDelegate next, ILogger<GatewayExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// 执行中间件
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// 处理异常
    /// </summary>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.Items[GatewayConstants.TraceIdKey]?.ToString() ?? context.TraceIdentifier;

        // 记录异常日志
        _logger.LogError(exception,
            "网关异常: {Message}, TraceId: {TraceId}, Path: {Path}",
            exception.Message,
            traceId,
            context.Request.Path);

        // 构造响应
        var response = new GatewayErrorResponse
        {
            TraceId = traceId,
            ErrorCode = "GATEWAY_ERROR",
            ErrorMessage = exception.Message,
            Path = context.Request.Path,
            Timestamp = DateTime.UtcNow
        };

        // 根据异常类型设置状态码
        context.Response.StatusCode = exception switch
        {
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
