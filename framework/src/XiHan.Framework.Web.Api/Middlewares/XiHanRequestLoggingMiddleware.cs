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
                        StatusCode = context.Response.StatusCode,
                        RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = context.Request.Headers.UserAgent.ToString(),
                        Referer = context.Request.Headers.Referer.ToString(),
                        ElapsedMilliseconds = (long)Math.Round(elapsedMs),
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
}
