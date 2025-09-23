#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HttpLoggingMiddleware
// Guid:ba03e6e7-abec-465d-ba50-1a7df34a6191
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/5/31 20:15:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Http.Middleware;

/// <summary>
/// HTTP 日志中间件
/// </summary>
public class HttpLoggingMiddleware : DelegatingHandler
{
    private readonly ILogger<HttpLoggingMiddleware> _logger;
    private readonly HttpClientOptions _options;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="options">HTTP客户端选项</param>
    public HttpLoggingMiddleware(ILogger<HttpLoggingMiddleware> logger, HttpClientOptions options)
    {
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// 发送HTTP请求
    /// </summary>
    /// <param name="request">HTTP请求消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestId = GetRequestId(request);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 记录请求日志
            if (_options.EnableRequestLogging)
            {
                await LogRequestAsync(request, requestId);
            }

            // 发送请求
            var response = await base.SendAsync(request, cancellationToken);

            stopwatch.Stop();

            // 记录响应日志
            if (_options.EnableResponseLogging)
            {
                await LogResponseAsync(response, requestId, stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogException(ex, request, requestId, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// 获取请求Id
    /// </summary>
    /// <param name="request">HTTP请求</param>
    /// <returns></returns>
    private static string GetRequestId(HttpRequestMessage request)
    {
        return request.Headers.TryGetValues("X-Request-Id", out var values)
            ? values.FirstOrDefault() ?? Guid.NewGuid().ToString("N")[..8]
            : Guid.NewGuid().ToString("N")[..8];
    }

    /// <summary>
    /// 判断是否为敏感请求头
    /// </summary>
    /// <param name="headerName">请求头名称</param>
    /// <returns></returns>
    private static bool IsSensitiveHeader(string headerName)
    {
        var sensitiveHeaders = new[]
        {
            "Authorization",
            "Cookie",
            "Set-Cookie",
            "X-API-Key",
            "X-Auth-Token",
            "Proxy-Authorization"
        };

        return sensitiveHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 记录请求日志
    /// </summary>
    /// <param name="request">HTTP请求</param>
    /// <param name="requestId">请求Id</param>
    private async Task LogRequestAsync(HttpRequestMessage request, string requestId)
    {
        var logBuilder = new StringBuilder();
        logBuilder.AppendLine($"HTTP Request [{requestId}]");
        logBuilder.AppendLine($"Method: {request.Method}");
        logBuilder.AppendLine($"Url: {request.RequestUri}");
        logBuilder.AppendLine($"Version: {request.Version}");

        // 记录请求头
        var headers = new StringBuilder();
        if (request.Headers.Any())
        {
            headers.AppendLine("Headers:");
            foreach (var header in request.Headers)
            {
                var headerValue = _options.LogSensitiveData || !IsSensitiveHeader(header.Key)
                    ? string.Join(", ", header.Value)
                    : "***";
                headers.AppendLine($"  {header.Key}: {headerValue}");
            }
        }

        // 记录请求内容
        var content = string.Empty;
        if (request.Content != null)
        {
            if (request.Content.Headers.Any())
            {
                logBuilder.AppendLine("Content Headers:");
                foreach (var header in request.Content.Headers)
                {
                    logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }
            }

            if (_options.LogSensitiveData)
            {
                content = await request.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(content))
                {
                    logBuilder.AppendLine($"Content: {content}");
                }
            }
        }

        _logger.LogInformation("HTTP Request [{RequestId}]\nMethod: {Method}\nUrl: {Url}\nVersion: {Version}\nHeaders: {Headers}\nContent: {Content}",
            requestId, request.Method, request.RequestUri, request.Version, headers.ToString(), content);
    }

    /// <summary>
    /// 记录响应日志
    /// </summary>
    /// <param name="response">HTTP响应</param>
    /// <param name="requestId">请求Id</param>
    /// <param name="elapsedMilliseconds">耗时</param>
    private async Task LogResponseAsync(HttpResponseMessage response, string requestId, long elapsedMilliseconds)
    {
        var logBuilder = new StringBuilder();
        logBuilder.AppendLine($"HTTP Response [{requestId}]");
        logBuilder.AppendLine($"Status: {(int)response.StatusCode} {response.StatusCode}");
        logBuilder.AppendLine($"Version: {response.Version}");
        logBuilder.AppendLine($"Elapsed: {elapsedMilliseconds}ms");

        // 记录响应头
        var headers = new StringBuilder();
        if (response.Headers.Any())
        {
            headers.AppendLine("Headers:");
            foreach (var header in response.Headers)
            {
                headers.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        // 记录响应内容
        if (response.Content.Headers.Any())
        {
            logBuilder.AppendLine("Content Headers:");
            foreach (var header in response.Content.Headers)
            {
                logBuilder.AppendLine($"  {header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        var content = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrEmpty(content))
        {
            var truncatedContent = content.Truncate(_options.MaxResponseContentLength);
            logBuilder.AppendLine($"Content: {truncatedContent}");
        }

        var logLevel = response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning;
        _logger.Log(logLevel, "HTTP Response [{RequestId}]\nStatus: {Status}\nVersion: {Version}\nElapsed: {ElapsedMilliseconds}ms\nHeaders: {Headers}\nContent: {Content}",
            requestId, $"{(int)response.StatusCode} {response.StatusCode}", response.Version, elapsedMilliseconds, headers.ToString(), content);
    }

    /// <summary>
    /// 记录异常日志
    /// </summary>
    /// <param name="exception">异常</param>
    /// <param name="request">HTTP请求</param>
    /// <param name="requestId">请求Id</param>
    /// <param name="elapsedMilliseconds">耗时</param>
    private void LogException(Exception exception, HttpRequestMessage request, string requestId, long elapsedMilliseconds)
    {
        _logger.LogError(exception,
            "HTTP Request [{RequestId}] failed after {ElapsedMilliseconds}ms. Method: {Method}, Url: {Url}",
            requestId, elapsedMilliseconds, request.Method, request.RequestUri);
    }
}
