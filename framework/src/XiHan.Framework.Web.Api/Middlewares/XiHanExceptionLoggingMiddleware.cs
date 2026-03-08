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

using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using XiHan.Framework.Application.Contracts.Dtos;
using XiHan.Framework.Application.Contracts.Enums;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Logging;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi 异常日志中间件
/// </summary>
public class XiHanExceptionLoggingMiddleware(RequestDelegate next, ILogger<XiHanExceptionLoggingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

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
        var cancellationToken = context.RequestAborted;
        var traceId = ResolveTraceId(context);
        var currentUser = context.RequestServices.GetService<ICurrentUser>();
        var (statusCode, _, message) = MapException(exception);

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

        await TryWriteExceptionLogAsync(context, currentUser, traceId, exception, statusCode, cancellationToken);

        if (context.Response.HasStarted)
        {
            logger.LogWarning("响应已开始，无法写入异常响应，TraceId: {TraceId}", traceId);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = ApiResponse.Fail(message, traceId);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await context.Response.WriteAsync(json, cancellationToken);
    }

    private async Task TryWriteExceptionLogAsync(
        HttpContext context,
        ICurrentUser? currentUser,
        string traceId,
        Exception exception,
        int statusCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var writer = context.RequestServices.GetService<IExceptionLogWriter>();
            if (writer is null)
            {
                return;
            }

            var actionDescriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            var controllerName = actionDescriptor?.ControllerName;
            var actionName = actionDescriptor?.ActionName;

            await writer.WriteAsync(new ExceptionLogRecord
            {
                TraceId = traceId,
                UserId = currentUser?.UserId,
                UserName = currentUser?.UserName,
                Path = context.Request.Path.ToString(),
                Method = context.Request.Method,
                ControllerName = controllerName,
                ActionName = actionName,
                StatusCode = statusCode,
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                ExceptionMessage = exception.Message,
                ExceptionStackTrace = exception.StackTrace,
                RequestHeaders = SafeSerialize(context.Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString())),
                RequestParams = SafeSerialize(BuildRequestParams(context)),
                RequestBody = ResolveRequestBody(context),
                RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString()
            }, cancellationToken);
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

    private static object? BuildRequestParams(HttpContext context)
    {
        var routeValues = context.Request.RouteValues;
        var queryValues = context.Request.Query;
        if (routeValues.Count == 0 && queryValues.Count == 0)
        {
            return null;
        }

        var payload = new Dictionary<string, object?>();
        if (routeValues.Count > 0)
        {
            payload["route"] = routeValues.ToDictionary(item => item.Key, item => item.Value);
        }

        if (queryValues.Count > 0)
        {
            payload["query"] = queryValues.ToDictionary(item => item.Key, item => item.Value.ToString());
        }

        return payload;
    }

    private static string? ResolveRequestBody(HttpContext context)
    {
        return context.Items.TryGetValue(XiHanWebApiConstants.RequestBodyItemKey, out var requestBody)
            ? requestBody?.ToString()
            : null;
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
