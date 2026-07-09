#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanExceptionLoggingMiddleware
// Guid:3f84c7a1-7ab7-4bd2-9f6c-5a6f6a18e7b4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/08 21:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Diagnostics;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Contexts;
using XiHan.Framework.Web.Api.Filters;
using XiHan.Framework.Web.Api.Logging;
using XiHan.Framework.Auditing;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi 异常日志中间件：仅负责日志（ILogger 分级 + 异常日志表落库），不产出响应。
/// </summary>
/// <remarks>
/// 异常响应统一由 <see cref="XiHanApiResponseResultFilter"/>（MVC 异常过滤器）产出，与正常响应同一套序列化
/// （camelCase + 中文不转义 + 业务码 int）。能到达本中间件的是 MVC 管线之外的异常（鉴权/租户/日志等中间件），
/// 此处只记录日志，并在响应未开始时回填状态码，避免出现空 200。
/// </remarks>
public class XiHanExceptionLoggingMiddleware(RequestDelegate next, ILogger<XiHanExceptionLoggingMiddleware> logger)
{
    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // 未捕获异常落到当前请求 span：trace 后端可见错误（OTel 未激活时 Activity.Current 为 null，安全跳过）
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.AddException(exception);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        }

        var requestContext = context.RequestServices.GetService<IRequestContextAccessor>()?.Current;
        var traceId = requestContext?.TraceId ?? ResolveTraceId(context);
        // 与 MVC 异常过滤器共用同一套异常→状态码映射，保证语义一致（仅取状态码，响应由 MVC 过滤器产出）
        var (statusCode, _) = XiHanApiResponseResultFilter.MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "未处理异常: {Message}, TraceId: {TraceId}, Path: {Path}",
                exception.Message,
                traceId,
                context.Request.Path);
        }
        else
        {
            logger.LogWarning(
                exception,
                "请求异常: {Message}, TraceId: {TraceId}, Path: {Path}",
                exception.Message,
                traceId,
                context.Request.Path);
        }

        await ExceptionLogReporter.ReportAsync(context, exception, statusCode, traceId, context.RequestAborted);

        // 不写响应体：响应统一由 MVC 异常过滤器产出。到此说明异常发生在 MVC 管线之外，
        // 仅在响应未开始时回填状态码，避免下游拿到空 200。
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = statusCode;
        }
    }

    private static string ResolveTraceId(HttpContext httpContext)
    {
        var requestContext = httpContext.RequestServices.GetService<IRequestContextAccessor>()?.Current;
        if (!string.IsNullOrWhiteSpace(requestContext?.TraceId))
        {
            return requestContext.TraceId;
        }

        return httpContext.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
            ?? httpContext.TraceIdentifier;
    }
}
