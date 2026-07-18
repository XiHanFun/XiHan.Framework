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

using System.Text;
using Microsoft.AspNetCore.Mvc.Controllers;
using XiHan.Framework.Security.Users;
using XiHan.Framework.Web.Api.Constants;
using XiHan.Framework.Auditing;
using XiHan.Framework.Auditing.Pipelines;
using XiHan.Framework.Web.Api.Security.OpenApi;

namespace XiHan.Framework.Web.Api.Middlewares;

/// <summary>
/// 接口日志中间件
/// 在 OpenApiSecurityMiddleware 之前注册，捕获 API 安全验证结果与请求/响应内容并写入接口日志
/// 仅当请求携带 OpenApi 安全头时才记录
/// </summary>
/// <remarks>
/// 请求体/查询串在上游 <see cref="XiHanRequestLoggingMiddleware"/> 捕获点即脱敏截断，存入 <c>HttpContext.Items</c>，本中间件直接复用；
/// 加密请求的明文由 <see cref="XiHanOpenApiSecurityMiddleware"/> 覆盖写入同一 Items 键（脱敏后）。
/// 响应体经有界包装流捕获（不整体缓冲大响应），仅对 JSON/文本内容类型转文本、超限截断、落库前脱敏。
/// </remarks>
public class XiHanApiLoggingMiddleware(
    RequestDelegate next,
    ILogger<XiHanApiLoggingMiddleware> logger)
{
    private const int ResponseBodyLimit = 4096;

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

        // 包装响应流以捕获响应体与准确响应大小（有界捕获，不整体缓冲；大响应透传、仅留前 N 字节）
        var originalBody = context.Response.Body;
        await using var capture = new BoundedResponseCaptureStream(originalBody, ResponseBodyLimit);
        context.Response.Body = capture;

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
            // 还原响应流（capture 只中转、不持有真实流所有权）
            context.Response.Body = originalBody;

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

                    // 请求体/查询串复用上游已脱敏截断的副本；控制器/动作取自 MVC 端点元数据（Minimal API 无则为空）
                    var requestBody = context.Items[XiHanWebApiConstants.RequestBodyItemKey]?.ToString();
                    var requestParams = context.Items[XiHanWebApiConstants.RequestQueryItemKey]?.ToString();
                    var (controllerName, actionName) = ResolveAction(context);
                    var responseBody = ResolveResponseBody(context, capture);

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
                        ControllerName = controllerName,
                        ActionName = actionName,
                        RequestParams = requestParams,
                        RequestBody = requestBody,
                        ResponseBody = responseBody,
                        StatusCode = context.Response.StatusCode,
                        RemoteIp = context.Connection.RemoteIpAddress?.ToString(),
                        UserAgent = context.Request.Headers.UserAgent.ToString(),
                        Referer = context.Request.Headers.Referer.ToString(),
                        ElapsedMilliseconds = (long)Math.Round(elapsedMs),
                        RequestSize = context.Request.ContentLength ?? 0,
                        ResponseSize = capture.BytesWritten > 0 ? capture.BytesWritten : context.Response.ContentLength ?? 0,
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

    /// <summary>
    /// 从 MVC 端点元数据提取控制器/动作；Minimal API 无 ControllerActionDescriptor 时返回空。
    /// </summary>
    private static (string? ControllerName, string? ActionName) ResolveAction(HttpContext context)
    {
        var actionDescriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        return actionDescriptor is null
            ? (null, null)
            : (actionDescriptor.ControllerName, actionDescriptor.ActionName);
    }

    /// <summary>
    /// 解析响应体文本：仅对 JSON/XML/文本内容类型转文本，脱敏后返回；二进制/空响应返回空。
    /// </summary>
    private static string? ResolveResponseBody(HttpContext context, BoundedResponseCaptureStream capture)
    {
        var contentType = context.Response.ContentType;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var normalized = contentType.ToLowerInvariant();
        var isTextual = normalized.Contains("application/json", StringComparison.Ordinal)
                        || normalized.Contains("application/xml", StringComparison.Ordinal)
                        || normalized.StartsWith("text/", StringComparison.Ordinal);
        if (!isTextual)
        {
            return null;
        }

        return LogSanitizer.MaskSensitiveData(capture.GetCapturedText());
    }

    /// <summary>
    /// 有界响应捕获流：写入透传到真实流，同时旁路留存前 N 字节并累计总字节数（避免整体缓冲大响应）。
    /// </summary>
    private sealed class BoundedResponseCaptureStream(Stream inner, int captureLimit) : Stream
    {
        private readonly MemoryStream _buffer = new();

        /// <summary>
        /// 实际写出的响应总字节数
        /// </summary>
        public long BytesWritten { get; private set; }

        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Flush() => inner.Flush();

        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            Capture(buffer.AsSpan(offset, count));
            inner.Write(buffer, offset, count);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Capture(buffer.AsSpan(offset, count));
            await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        }

        /// <inheritdoc />
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Capture(buffer.Span);
            await inner.WriteAsync(buffer, cancellationToken);
        }

        /// <summary>
        /// 取捕获到的响应文本（超限追加截断标记）；无内容返回空。
        /// </summary>
        public string? GetCapturedText()
        {
            if (_buffer.Length == 0)
            {
                return null;
            }

            var text = Encoding.UTF8.GetString(_buffer.GetBuffer(), 0, (int)_buffer.Length);
            return BytesWritten > captureLimit ? text + "...(truncated)" : text;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _buffer.Dispose();
            }

            base.Dispose(disposing);
        }

        private void Capture(ReadOnlySpan<byte> data)
        {
            BytesWritten += data.Length;
            var remaining = captureLimit - (int)_buffer.Length;
            if (remaining <= 0)
            {
                return;
            }

            var take = Math.Min(remaining, data.Length);
            _buffer.Write(data[..take]);
        }
    }
}
