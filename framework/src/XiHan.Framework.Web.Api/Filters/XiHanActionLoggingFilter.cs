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
using System.Text.Json;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Logging;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// WebApi Action 执行日志过滤器
/// </summary>
public class XiHanActionLoggingFilter(ILogger<XiHanActionLoggingFilter> logger) : IAsyncActionFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Action 执行前后日志
    /// </summary>
    /// <param name="context"></param>
    /// <param name="next"></param>
    /// <returns></returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var cancellationToken = context.HttpContext.RequestAborted;
        var traceId = ResolveTraceId(context.HttpContext);
        var currentUser = context.HttpContext.RequestServices.GetService<ICurrentUser>();
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

        try
        {
            var writer = context.HttpContext.RequestServices.GetService<IOperationLogWriter>();
            if (writer is not null)
            {
                await writer.WriteAsync(new OperationLogRecord
                {
                    TraceId = traceId,
                    UserId = currentUser?.UserId,
                    UserName = currentUser?.UserName,
                    ControllerName = controllerName,
                    ActionName = actionName,
                    Method = context.HttpContext.Request.Method,
                    Path = context.HttpContext.Request.Path.ToString(),
                    RequestParams = SafeSerialize(context.ActionArguments),
                    ResponseResult = SafeSerialize(executedContext.Result),
                    StatusCode = context.HttpContext.Response.StatusCode,
                    ElapsedMilliseconds = (long)Math.Round(elapsedMs),
                    RemoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.HttpContext.Request.Headers.UserAgent.ToString(),
                    ErrorMessage = executedContext.Exception?.Message
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "操作日志写入失败，TraceId: {TraceId}", traceId);
        }
    }

    private static string ResolveTraceId(HttpContext httpContext)
    {
        return httpContext.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
            ?? httpContext.TraceIdentifier;
    }

    private static string? SafeSerialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            return json.Length > 4000 ? json[..4000] : json;
        }
        catch
        {
            return value.ToString();
        }
    }
}
