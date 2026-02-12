#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanGlobalExceptionFilter
// Guid:4f2a4bdb-5774-4f93-94fe-10d0369f8665
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Logging;

namespace XiHan.Framework.Web.Api.Filters;

/// <summary>
/// WebApi 全局异常过滤器
/// </summary>
public class XiHanGlobalExceptionFilter(ILogger<XiHanGlobalExceptionFilter> logger) : IAsyncExceptionFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// 异常处理
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var exception = context.Exception;
        var cancellationToken = context.HttpContext.RequestAborted;
        var traceId = ResolveTraceId(context.HttpContext);
        var currentUser = context.HttpContext.RequestServices.GetService<ICurrentUser>();
        var (statusCode, code, message) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "未处理异常: {Message}, TraceId: {TraceId}, Path: {Path}",
                exception.Message,
                traceId,
                context.HttpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(
                exception,
                "请求异常: {Message}, TraceId: {TraceId}, Path: {Path}",
                exception.Message,
                traceId,
                context.HttpContext.Request.Path);
        }

        context.Result = new ObjectResult(ApiResponse.Fail(message, code, traceId))
        {
            StatusCode = statusCode
        };
        context.ExceptionHandled = true;

        try
        {
            var writer = context.HttpContext.RequestServices.GetService<IExceptionLogWriter>();
            if (writer is not null)
            {
                await writer.WriteAsync(new ExceptionLogRecord
                {
                    TraceId = traceId,
                    UserId = currentUser?.UserId,
                    UserName = currentUser?.UserName,
                    Path = context.HttpContext.Request.Path.ToString(),
                    Method = context.HttpContext.Request.Method,
                    ControllerName = (context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)?.ControllerName,
                    ActionName = (context.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)?.ActionName,
                    StatusCode = statusCode,
                    ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                    ExceptionMessage = exception.Message,
                    ExceptionStackTrace = exception.StackTrace,
                    RequestHeaders = SafeSerialize(context.HttpContext.Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString())),
                    RequestParams = SafeSerialize(context.RouteData.Values.ToDictionary(k => k.Key, v => v.Value)),
                    RemoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.HttpContext.Request.Headers.UserAgent.ToString()
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "异常日志写入失败，TraceId: {TraceId}", traceId);
        }
    }

    private static string ResolveTraceId(HttpContext httpContext)
    {
        return httpContext.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
            ?? httpContext.TraceIdentifier;
    }

    private static (int StatusCode, ApiResponseCodes Code, string Message) MapException(Exception exception)
    {
        return exception switch
        {
            UserFriendlyException userFriendlyException => (
                StatusCodes.Status400BadRequest,
                ApiResponseCodes.BadRequest,
                string.IsNullOrWhiteSpace(userFriendlyException.Message) ? "请求失败" : userFriendlyException.Message),
            BusinessException businessException => (
                StatusCodes.Status400BadRequest,
                ApiResponseCodes.BadRequest,
                string.IsNullOrWhiteSpace(businessException.Message) ? "业务处理失败" : businessException.Message),
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                ApiResponseCodes.Unauthorized,
                "未授权访问"),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                ApiResponseCodes.NotFound,
                "资源不存在"),
            ArgumentException argumentException => (
                StatusCodes.Status400BadRequest,
                ApiResponseCodes.BadRequest,
                string.IsNullOrWhiteSpace(argumentException.Message) ? "请求参数错误" : argumentException.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                ApiResponseCodes.Failed,
                "服务端处理异常")
        };
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
