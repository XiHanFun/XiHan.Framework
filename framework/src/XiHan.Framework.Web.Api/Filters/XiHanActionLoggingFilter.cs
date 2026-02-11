#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanActionLoggingFilter
// Guid:abde6f4d-7f95-488f-832c-0e834ac31153
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using XiHan.Framework.Web.Api.Constants;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// WebApi Action 执行日志过滤器
/// </summary>
public class XiHanActionLoggingFilter(ILogger<XiHanActionLoggingFilter> logger) : IAsyncActionFilter
{
    /// <summary>
    /// Action 执行前后日志
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var traceId = ResolveTraceId(context.HttpContext);
        var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var actionName = actionDescriptor?.ActionName ?? context.ActionDescriptor.DisplayName;
        var controllerName = actionDescriptor?.ControllerName ?? "UnknownController";
        var startAt = DateTimeOffset.UtcNow;

        logger.LogInformation(
            "Action开始: {Controller}.{Action}, TraceId: {TraceId}, Method: {Method}, Path: {Path}",
            controllerName,
            actionName,
            traceId,
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path);

        var executedContext = await next();
        var elapsedMs = (DateTimeOffset.UtcNow - startAt).TotalMilliseconds;

        logger.LogInformation(
            "Action结束: {Controller}.{Action}, TraceId: {TraceId}, StatusCode: {StatusCode}, 耗时: {Elapsed}ms",
            controllerName,
            actionName,
            traceId,
            context.HttpContext.Response.StatusCode,
            elapsedMs);

        if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
        {
            logger.LogWarning(
                executedContext.Exception,
                "Action抛出异常: {Controller}.{Action}, TraceId: {TraceId}",
                controllerName,
                actionName,
                traceId);
        }
    }

    private static string ResolveTraceId(HttpContext httpContext)
    {
        return httpContext.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
            ?? httpContext.TraceIdentifier;
    }
}
