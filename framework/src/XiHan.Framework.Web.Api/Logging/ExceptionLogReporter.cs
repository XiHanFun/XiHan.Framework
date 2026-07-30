// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Controllers;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Contexts;
using XiHan.Framework.Auditing;
using XiHan.Framework.Auditing.Pipelines;

namespace XiHan.Framework.Web.Api.Logging;

/// <summary>
/// 异常日志上报：从 <see cref="HttpContext"/> 组装异常日志记录并写入异常日志管线（队列异步落库）。
/// </summary>
/// <remarks>
/// 由 MVC 异常过滤器（<c>XiHanApiResponseResultFilter</c>，负责产出响应）与异常中间件
/// （<c>XiHanExceptionLoggingMiddleware</c>，仅日志、非 MVC 兜底）共用，使异常日志表的记录逻辑保持单一来源。
/// </remarks>
public static class ExceptionLogReporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// 组装并写入异常日志记录；写入失败仅告警，不抛出、不影响主流程。
    /// </summary>
    /// <param name="context">当前请求上下文</param>
    /// <param name="exception">异常</param>
    /// <param name="statusCode">最终响应状态码</param>
    /// <param name="traceId">跟踪标识</param>
    /// <param name="cancellationToken">取消标记</param>
    public static async Task ReportAsync(HttpContext context, Exception exception, int statusCode, string traceId, CancellationToken cancellationToken)
    {
        try
        {
            var pipeline = context.RequestServices.GetService<IExceptionLogPipeline>();
            if (pipeline is null)
            {
                return;
            }

            var requestContext = context.RequestServices.GetService<IRequestContextAccessor>()?.Current;
            var currentUser = context.RequestServices.GetService<ICurrentUser>();
            var actionDescriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();

            await pipeline.WriteAsync(new ExceptionLogRecord
            {
                TraceId = traceId,
                UserId = requestContext?.UserId ?? currentUser?.UserId,
                UserName = requestContext?.UserName ?? currentUser?.UserName,
                Path = requestContext?.Path ?? context.Request.Path.ToString(),
                Method = requestContext?.Method ?? context.Request.Method,
                ControllerName = actionDescriptor?.ControllerName,
                ActionName = actionDescriptor?.ActionName,
                StatusCode = statusCode,
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                ExceptionMessage = exception.Message,
                ExceptionStackTrace = exception.StackTrace,
                // 请求头按头名整体掩码（Authorization/Cookie/ApiKey 等），请求参数（route + query）走通用脱敏；
                // 请求体取的是中间件放进 Items 的副本，上游已脱敏。
                RequestHeaders = SafeSerialize(LogSanitizer.MaskHeaders(
                    context.Request.Headers.Select(header => new KeyValuePair<string, string?>(header.Key, header.Value.ToString())))),
                RequestParams = LogSanitizer.MaskSensitiveData(SafeSerialize(BuildRequestParams(context))),
                RequestBody = ResolveRequestBody(context),
                RemoteIp = requestContext?.RemoteIp ?? context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = requestContext?.UserAgent ?? context.Request.Headers.UserAgent.ToString()
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            context.RequestServices.GetService<ILoggerFactory>()?
                .CreateLogger(typeof(ExceptionLogReporter))
                .LogWarning(ex, "异常日志写入失败，TraceId: {TraceId}", traceId);
        }
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
