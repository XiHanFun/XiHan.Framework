#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ProxyUsageExamples
// Guid:i4n6p8k0-jl1m-5n7o-kp3q-2l4m5n6o7p8j
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Http.Proxy;
using XiHan.Framework.Http.Services;

namespace XiHan.Framework.Http.Tests.Examples;

/// <summary>
/// 代理使用示例
/// </summary>
/// <remarks>
/// 这个类提供了各种代理使用场景的示例代码
/// </remarks>
public class ProxyUsageExamples
{
    private readonly IAdvancedHttpService _httpService;
    private readonly IProxyPoolManager _proxyPoolManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="httpService">HTTP服务</param>
    /// <param name="proxyPoolManager">代理池管理器</param>
    public ProxyUsageExamples(IAdvancedHttpService httpService, IProxyPoolManager proxyPoolManager)
    {
        _httpService = httpService;
        _proxyPoolManager = proxyPoolManager;
    }

    /// <summary>
    /// 示例1: 使用单个指定的代理
    /// </summary>
    public async Task Example1_UseSpecificProxyAsync()
    {
        // 创建代理配置
        var proxy = new ProxyConfiguration
        {
            Name = "My Proxy",
            Host = "127.0.0.1",
            Port = 7890,
            Type = ProxyType.Http
        };

        // 创建请求选项并设置代理
        var options = new XiHanHttpRequestOptions()
            .SetProxy(proxy);

        // 发送请求
        var result = await _httpService.GetAsync<string>("https://api.ipify.org?format=json", options);

        if (result.IsSuccess)
        {
            Console.WriteLine($"响应: {result.Data}");
        }
        else
        {
            Console.WriteLine($"错误: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// 示例2: 使用需要认证的代理
    /// </summary>
    public async Task Example2_UseAuthenticatedProxyAsync()
    {
        var proxy = new ProxyConfiguration
        {
            Host = "proxy.example.com",
            Port = 8080,
            Type = ProxyType.Http,
            Username = "myusername",
            Password = "mypassword"
        };

        var options = new XiHanHttpRequestOptions()
            .SetProxy(proxy);

        var result = await _httpService.GetAsync<string>("https://api.example.com/data", options);

        Console.WriteLine($"请求完成: {result.IsSuccess}");
    }

    /// <summary>
    /// 示例3: 使用代理池自动选择代理
    /// </summary>
    public async Task Example3_UseProxyPoolAsync()
    {
        // 启用代理池（将自动从代理池中选择可用的代理）
        var options = new XiHanHttpRequestOptions()
            .EnableProxyPool();

        var result = await _httpService.GetAsync<string>("https://api.example.com/data", options);

        Console.WriteLine($"请求完成: {result.IsSuccess}");
    }

    /// <summary>
    /// 示例4: 动态添加和管理代理
    /// </summary>
    public async Task Example4_ManageProxyPoolAsync()
    {
        // 添加新代理到池
        var newProxy = new ProxyConfiguration
        {
            Name = "Dynamic Proxy",
            Host = "192.168.1.100",
            Port = 8080,
            Type = ProxyType.Http
        };

        var added = await _proxyPoolManager.AddProxyAsync(newProxy);
        Console.WriteLine($"代理添加结果: {added}");

        // 获取代理池大小
        var poolSize = _proxyPoolManager.GetPoolSize();
        Console.WriteLine($"代理池大小: {poolSize}");

        // 获取可用代理数量
        var availableCount = _proxyPoolManager.GetAvailableCount();
        Console.WriteLine($"可用代理数量: {availableCount}");

        // 移除代理
        var removed = _proxyPoolManager.RemoveProxy(newProxy.GetProxyAddress());
        Console.WriteLine($"代理移除结果: {removed}");
    }

    /// <summary>
    /// 示例5: 查看代理统计信息
    /// </summary>
    public void Example5_ViewProxyStatistics()
    {
        var statistics = _proxyPoolManager.GetAllStatistics();

        foreach (var stat in statistics)
        {
            Console.WriteLine($"代理地址: {stat.ProxyAddress}");
            Console.WriteLine($"  总请求数: {stat.TotalRequests}");
            Console.WriteLine($"  成功数: {stat.SuccessCount}");
            Console.WriteLine($"  失败数: {stat.FailureCount}");
            Console.WriteLine($"  成功率: {stat.SuccessRate:P2}");
            Console.WriteLine($"  平均响应时间: {stat.AverageResponseTime:F2}ms");
            Console.WriteLine($"  当前连接数: {stat.CurrentConnections}");
            Console.WriteLine($"  是否可用: {stat.IsAvailable}");
            Console.WriteLine($"  最后使用时间: {stat.LastUsedAt}");
            Console.WriteLine($"  连续失败次数: {stat.ConsecutiveFailures}");
            Console.WriteLine("---");
        }
    }

    /// <summary>
    /// 示例6: 批量请求使用代理池
    /// </summary>
    public async Task Example6_BatchRequestsWithProxyAsync()
    {
        var urls = new[]
        {
            "https://api.example.com/data1",
            "https://api.example.com/data2",
            "https://api.example.com/data3",
            "https://api.example.com/data4",
            "https://api.example.com/data5"
        };

        var tasks = new List<Task>();

        foreach (var url in urls)
        {
            var options = new XiHanHttpRequestOptions()
                .EnableProxyPool()
                .SetRequestId(Guid.NewGuid().ToString());

            tasks.Add(Task.Run(async () =>
            {
                var result = await _httpService.GetAsync<string>(url, options);
                Console.WriteLine($"URL: {url}, 成功: {result.IsSuccess}");
            }));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 示例7: 处理代理失败和重试
    /// </summary>
    public async Task Example7_HandleProxyFailureAsync()
    {
        var maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            var options = new XiHanHttpRequestOptions()
                .EnableProxyPool()
                .SetRequestId($"retry-{retryCount}");

            var result = await _httpService.GetAsync<string>("https://api.example.com/data", options);

            if (result.IsSuccess)
            {
                Console.WriteLine($"请求成功: {result.Data}");
                break;
            }

            retryCount++;
            Console.WriteLine($"请求失败 (第 {retryCount} 次重试): {result.ErrorMessage}");

            if (retryCount < maxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount))); // 指数退避
            }
        }

        if (retryCount >= maxRetries)
        {
            Console.WriteLine("达到最大重试次数，请求失败");
        }
    }

    /// <summary>
    /// 示例8: 组合使用代理和其他选项
    /// </summary>
    public async Task Example8_CombineProxyWithOtherOptionsAsync()
    {
        var options = new XiHanHttpRequestOptions()
            .EnableProxyPool()
            .SetTimeout(TimeSpan.FromSeconds(15))
            .AddHeader("Authorization", "Bearer token123")
            .AddHeader("X-Custom-Header", "custom-value")
            .AddQueryParameter("page", "1")
            .AddQueryParameter("size", "20")
            .SetRequestId("custom-request-id");

        options.LogRequest = true;
        options.LogResponse = true;

        var result = await _httpService.GetAsync<string>("https://api.example.com/data", options);

        Console.WriteLine($"状态码: {result.StatusCode}");
        Console.WriteLine($"响应时间: {result.ElapsedMilliseconds}ms");
        Console.WriteLine($"成功: {result.IsSuccess}");
    }

    /// <summary>
    /// 示例9: 使用代理下载文件
    /// </summary>
    public async Task Example9_DownloadFileWithProxyAsync()
    {
        var options = new XiHanHttpRequestOptions()
            .EnableProxyPool();

        var progress = new Progress<long>(bytes =>
        {
            Console.WriteLine($"已下载: {bytes / 1024}KB");
        });

        var result = await _httpService.DownloadFileAsync(
            "https://example.com/large-file.zip",
            "downloaded-file.zip",
            progress,
            options);

        if (result.IsSuccess)
        {
            Console.WriteLine("文件下载成功");
        }
        else
        {
            Console.WriteLine($"文件下载失败: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// 示例10: 监控代理健康状态
    /// </summary>
    public async Task Example10_MonitorProxyHealthAsync()
    {
        while (true)
        {
            var statistics = _proxyPoolManager.GetAllStatistics();
            var availableProxies = statistics.Count(s => s.IsAvailable);
            var totalProxies = statistics.Count();

            Console.Clear();
            Console.WriteLine($"=== 代理池状态 ===");
            Console.WriteLine($"总代理数: {totalProxies}");
            Console.WriteLine($"可用代理: {availableProxies}");
            Console.WriteLine($"不可用代理: {totalProxies - availableProxies}");
            Console.WriteLine();

            foreach (var stat in statistics.OrderByDescending(s => s.IsAvailable).ThenBy(s => s.AverageResponseTime))
            {
                var status = stat.IsAvailable ? "✓" : "✗";
                Console.WriteLine($"{status} {stat.ProxyAddress}");
                Console.WriteLine($"   请求: {stat.TotalRequests}, 成功率: {stat.SuccessRate:P0}, 响应: {stat.AverageResponseTime:F0}ms");
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}
