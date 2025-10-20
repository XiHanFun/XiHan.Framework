#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyValidator
// Guid:d9i1k3f5-eg6h-0i2j-fk8l-7g9h0i1j2k3e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;
using XiHan.Framework.Http.Models;

namespace XiHan.Framework.Http.Proxy;

/// <summary>
/// 代理验证器实现
/// </summary>
public class ProxyValidator : IProxyValidator
{
    private readonly ILogger<ProxyValidator> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public ProxyValidator(ILogger<ProxyValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 验证代理是否可用
    /// </summary>
    /// <param name="proxy">代理配置</param>
    /// <param name="testUrl">测试URL</param>
    /// <param name="timeoutSeconds">超时时间(秒)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<ProxyValidationResult> ValidateAsync(ProxyConfiguration proxy, string testUrl, int timeoutSeconds = 10, CancellationToken cancellationToken = default)
    {
        if (!proxy.Validate())
        {
            return ProxyValidationResult.Failure(proxy, "代理配置无效");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var handler = CreateProxyHandler(proxy);
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };

            var response = await client.GetAsync(testUrl, linkedCts.Token);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("代理验证成功: {ProxyAddress}, 响应时间: {ResponseTime}ms",
                    proxy.GetProxyAddress(), stopwatch.ElapsedMilliseconds);

                return ProxyValidationResult.Success(proxy, stopwatch.ElapsedMilliseconds);
            }

            var errorMessage = $"HTTP {(int)response.StatusCode} {response.StatusCode}";
            _logger.LogWarning("代理验证失败: {ProxyAddress}, 错误: {Error}",
                proxy.GetProxyAddress(), errorMessage);

            return ProxyValidationResult.Failure(proxy, errorMessage);
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            var errorMessage = "代理连接超时";
            _logger.LogWarning(ex, "代理验证超时: {ProxyAddress}", proxy.GetProxyAddress());
            return ProxyValidationResult.Failure(proxy, errorMessage);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var errorMessage = $"HTTP请求错误: {ex.Message}";
            _logger.LogWarning(ex, "代理验证失败: {ProxyAddress}, 错误: {Error}",
                proxy.GetProxyAddress(), ex.Message);
            return ProxyValidationResult.Failure(proxy, errorMessage);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorMessage = $"未知错误: {ex.Message}";
            _logger.LogError(ex, "代理验证出错: {ProxyAddress}", proxy.GetProxyAddress());
            return ProxyValidationResult.Failure(proxy, errorMessage);
        }
    }

    /// <summary>
    /// 批量验证代理
    /// </summary>
    /// <param name="proxies">代理配置列表</param>
    /// <param name="testUrl">测试URL</param>
    /// <param name="timeoutSeconds">超时时间(秒)</param>
    /// <param name="maxConcurrency">最大并发数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<IEnumerable<ProxyValidationResult>> ValidateBatchAsync(IEnumerable<ProxyConfiguration> proxies, string testUrl, int timeoutSeconds = 10, int maxConcurrency = 10, CancellationToken cancellationToken = default)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = proxies.Select(async proxy =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ValidateAsync(proxy, testUrl, timeoutSeconds, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 创建代理处理器
    /// </summary>
    /// <param name="proxy">代理配置</param>
    /// <returns></returns>
    private static HttpClientHandler CreateProxyHandler(ProxyConfiguration proxy)
    {
        var handler = new HttpClientHandler();

        var scheme = proxy.Type switch
        {
            ProxyType.Http => "http",
            ProxyType.Https => "https",
            ProxyType.Socks4 => "socks4",
            ProxyType.Socks4A => "socks4a",
            ProxyType.Socks5 => "socks5",
            _ => "http"
        };

        var webProxy = new WebProxy
        {
            Address = new Uri($"{scheme}://{proxy.Host}:{proxy.Port}"),
            BypassProxyOnLocal = !proxy.UseProxyForLocalAddress,
            UseDefaultCredentials = false
        };

        // 设置绕过列表
        if (proxy.BypassList.Count > 0)
        {
            webProxy.BypassList = [.. proxy.BypassList];
        }

        // 设置认证
        if (!string.IsNullOrEmpty(proxy.Username) && !string.IsNullOrEmpty(proxy.Password))
        {
            webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
        }

        handler.Proxy = webProxy;
        handler.UseProxy = true;

        return handler;
    }
}

