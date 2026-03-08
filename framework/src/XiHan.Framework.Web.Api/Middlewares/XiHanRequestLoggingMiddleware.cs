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

using System.Text;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Logging;
using Microsoft.AspNetCore.Http.Features;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// WebApi 请求日志中间件
/// </summary>
public class XiHanRequestLoggingMiddleware(RequestDelegate next, ILogger<XiHanRequestLoggingMiddleware> logger)
{
    private const int RequestBodyLimit = 4096;

    /// <summary>
    /// 执行中间件
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var cancellationToken = context.RequestAborted;
        var traceId = context.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
            ?? context.TraceIdentifier;
        var currentUser = context.RequestServices.GetService<ICurrentUser>();
        var queryString = context.Request.QueryString.HasValue
            ? context.Request.QueryString.Value
            : null;
        var requestBody = await TryReadRequestBodyAsync(context.Request, cancellationToken);

        if (!string.IsNullOrWhiteSpace(queryString))
        {
            context.Items[XiHanWebApiConstants.RequestQueryItemKey] = queryString;
        }

        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            context.Items[XiHanWebApiConstants.RequestBodyItemKey] = requestBody;
        }

        var startAt = DateTimeOffset.UtcNow;
        Exception? unhandledException = null;
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
        catch (Exception ex)
        {
            unhandledException = ex;
            throw;
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

            try
            {
                var writer = context.RequestServices.GetService<IAccessLogWriter>();
                if (writer is not null)
                {
                    await writer.WriteAsync(new AccessLogRecord
                    {
                        TraceId = traceId,
                        UserId = currentUser?.UserId,
                        UserName = currentUser?.UserName,
                        SessionId = context.Features.Get<ISessionFeature>()?.Session?.Id,
                        Method = context.Request.Method,
                        Path = context.Request.Path.ToString(),
                        QueryString = queryString,
                        RequestBody = requestBody,
                        StatusCode = context.Response.StatusCode,
                        RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = context.Request.Headers.UserAgent.ToString(),
                        Referer = context.Request.Headers.Referer.ToString(),
                        ElapsedMilliseconds = (long)Math.Round(elapsedMs),
                        ResponseSize = context.Response.ContentLength ?? 0,
                        ErrorMessage = unhandledException?.Message
                    }, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "访问日志写入失败，TraceId: {TraceId}", traceId);
            }
        }
    }

    private static bool ShouldCaptureBody(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return false;
        }

        var contentType = request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var normalized = contentType.Trim().ToLowerInvariant();
        if (normalized.StartsWith("multipart/", StringComparison.Ordinal))
        {
            return false;
        }

        return normalized.Contains("application/json", StringComparison.Ordinal) ||
               normalized.Contains("application/xml", StringComparison.Ordinal) ||
               normalized.Contains("application/x-www-form-urlencoded", StringComparison.Ordinal) ||
               normalized.StartsWith("text/", StringComparison.Ordinal);
    }

    private static async Task<string?> TryReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!ShouldCaptureBody(request))
        {
            return null;
        }

        try
        {
            request.EnableBuffering();
            using var reader = new StreamReader(
                request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);

            var buffer = new char[RequestBodyLimit];
            var readCount = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }

            if (readCount <= 0)
            {
                return null;
            }

            var body = new string(buffer, 0, readCount);
            if (readCount >= RequestBodyLimit)
            {
                body += "...(truncated)";
            }

            return body;
        }
        catch
        {
            return null;
        }
    }
}
