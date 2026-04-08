#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanApiLoggingMiddleware
// Guid:2a3b4c5d-6e7f-8091-a2b3-c4d5e6f70819
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/08 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Security.Users;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Web.Api.Logging;
using XiHan.Framework.Web.Api.Logging.Pipelines;
using XiHan.Framework.Web.Api.Security.OpenApi;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// 接口日志中间件
/// 在 OpenApiSecurityMiddleware 之前注册，捕获 API 安全验证结果并写入接口日志
/// 仅当请求携带 OpenApi 安全头时才记录
/// </summary>
public class XiHanApiLoggingMiddleware(
    RequestDelegate next,
    ILogger<XiHanApiLoggingMiddleware> logger)
{
    /// <summary>
    /// 处理请求
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Request.Headers;
        var hasSecurityHeaders = headers.ContainsKey(OpenApiSecurityConstants.AccessKeyHeaderName)
                                 || headers.ContainsKey(OpenApiSecurityConstants.SignatureHeaderName);

        if (!hasSecurityHeaders)
        {
            await next(context);
            return;
        }

        var startAt = DateTimeOffset.UtcNow;
        var accessKey = headers[OpenApiSecurityConstants.AccessKeyHeaderName].FirstOrDefault()?.Trim();
        Exception? unhandledException = null;

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

            try
            {
                var pipeline = context.RequestServices.GetService<IApiLogPipeline>();
                if (pipeline is not null)
                {
                    var traceId = context.Items[XiHanWebApiConstants.TraceIdItemKey]?.ToString()
                                  ?? context.TraceIdentifier;
                    var currentUser = context.RequestServices.GetService<ICurrentUser>();

                    // 安全验证通过后，客户端信息会存储在 Items 中
                    var client = context.Items[OpenApiSecurityConstants.SecurityClientContextKey] as OpenApiSecurityClient;
                    var isSignatureValid = client is not null;

                    await pipeline.WriteAsync(new ApiLogRecord
                    {
                        TraceId = traceId,
                        UserId = currentUser?.UserId,
                        UserName = currentUser?.UserName,
                        ClientId = accessKey,
                        AppId = client?.AccessKey,
                        IsSignatureValid = isSignatureValid,
                        SignatureAlgorithm = client?.SignatureAlgorithm,
                        Method = context.Request.Method,
                        Path = context.Request.Path.ToString(),
                        StatusCode = context.Response.StatusCode,
                        RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = context.Request.Headers.UserAgent.ToString(),
                        Referer = context.Request.Headers.Referer.ToString(),
                        ElapsedMilliseconds = (long)Math.Round(elapsedMs),
                        RequestSize = context.Request.ContentLength ?? 0,
                        ResponseSize = context.Response.ContentLength ?? 0,
                        IsSuccess = context.Response.StatusCode < 400 && unhandledException is null,
                        ErrorMessage = unhandledException?.Message
                    }, context.RequestAborted);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "接口日志写入失败，TraceId: {TraceId}",
                    context.Items[XiHanWebApiConstants.TraceIdItemKey]);
            }
        }
    }
}
