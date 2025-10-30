#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SocksProxyExample
// Guid:j5o7q9l1-km2n-6o8p-lq4r-3m5n6o7p8q9k
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Http.Configuration;
using XiHan.Framework.Http.Enums;
using XiHan.Framework.Http.Options;
using XiHan.Framework.Http.Services;

namespace XiHan.Framework.Http.Tests.Examples;

/// <summary>
/// SOCKS 代理使用示例
/// </summary>
/// <remarks>
/// 从 .NET 6 开始，SocketsHttpHandler 和 HttpClientHandler 原生支持 SOCKS4/SOCKS5 代理
/// 参考: https://github.com/MihaZupan/HttpToSocks5Proxy
/// </remarks>
public class SocksProxyExample
{
    private readonly IAdvancedHttpService _httpService;

    public SocksProxyExample(IAdvancedHttpService httpService)
    {
        _httpService = httpService;
    }

    /// <summary>
    /// 示例1: 使用 SOCKS5 代理（如 Tor）
    /// </summary>
    public async Task Example1_UseSocks5ProxyWithTorAsync()
    {
        // Tor 默认使用 SOCKS5 代理，端口 9050
        var torProxy = new ProxyConfiguration
        {
            Name = "Tor Network",
            Host = "127.0.0.1",
            Port = 9050,
            Type = ProxyType.Socks5
        };

        var options = new XiHanHttpRequestOptions()
            .SetProxy(torProxy);

        // 检查是否通过 Tor 连接
        var result = await _httpService.GetStringAsync("https://check.torproject.org/", options);

        if (result.IsSuccess)
        {
            Console.WriteLine("通过 Tor 访问成功！");
            Console.WriteLine(result.Data);
        }
        else
        {
            Console.WriteLine($"访问失败: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// 示例2: 使用需要认证的 SOCKS5 代理
    /// </summary>
    public async Task Example2_UseSocks5ProxyWithAuthAsync()
    {
        var socks5Proxy = new ProxyConfiguration
        {
            Name = "Authenticated SOCKS5",
            Host = "proxy.example.com",
            Port = 1080,
            Type = ProxyType.Socks5,
            Username = "myusername",
            Password = "mypassword"
        };

        var options = new XiHanHttpRequestOptions()
            .SetProxy(socks5Proxy);

        var result = await _httpService.GetAsync<string>("https://api.ipify.org?format=json", options);

        if (result.IsSuccess)
        {
            Console.WriteLine($"你的IP地址: {result.Data}");
        }
    }

    /// <summary>
    /// 示例3: 使用 SOCKS4 代理
    /// </summary>
    public async Task Example3_UseSocks4ProxyAsync()
    {
        var socks4Proxy = new ProxyConfiguration
        {
            Host = "127.0.0.1",
            Port = 1080,
            Type = ProxyType.Socks4
        };

        var options = new XiHanHttpRequestOptions()
            .SetProxy(socks4Proxy);

        var result = await _httpService.GetStringAsync("https://example.com", options);

        Console.WriteLine($"请求状态: {result.StatusCode}");
        Console.WriteLine($"响应时间: {result.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// 示例4: 对比不同代理类型的性能
    /// </summary>
    public async Task Example4_CompareProxyTypesAsync()
    {
        var proxies = new[]
        {
            new ProxyConfiguration { Name = "HTTP", Host = "127.0.0.1", Port = 7890, Type = ProxyType.Http },
            new ProxyConfiguration { Name = "SOCKS4", Host = "127.0.0.1", Port = 1080, Type = ProxyType.Socks4 },
            new ProxyConfiguration { Name = "SOCKS5", Host = "127.0.0.1", Port = 1080, Type = ProxyType.Socks5 }
        };

        var testUrl = "https://www.google.com";

        foreach (var proxy in proxies)
        {
            var options = new XiHanHttpRequestOptions().SetProxy(proxy);
            var result = await _httpService.GetStringAsync(testUrl, options);

            Console.WriteLine($"{proxy.Name} 代理:");
            Console.WriteLine($"  成功: {result.IsSuccess}");
            Console.WriteLine($"  响应时间: {result.ElapsedMilliseconds}ms");
            Console.WriteLine($"  状态码: {result.StatusCode}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 示例5: 配置 SOCKS5 代理池
    /// </summary>
    public async Task Example5_Socks5ProxyPoolAsync()
    {
        // 在 appsettings.json 中配置多个 SOCKS5 代理:
        /*
        {
          "XiHan": {
            "Http:ProxyPool": {
              "Enabled": true,
              "SelectionStrategy": "FastestResponse",
              "Proxies": [
                {
                  "Name": "SOCKS5-1",
                  "Host": "proxy1.example.com",
                  "Port": 1080,
                  "Type": "Socks5",
                  "Enabled": true
                },
                {
                  "Name": "SOCKS5-2",
                  "Host": "proxy2.example.com",
                  "Port": 1080,
                  "Type": "Socks5",
                  "Enabled": true
                }
              ]
            }
          }
        }
        */

        // 使用代理池（自动选择最快的 SOCKS5 代理）
        var options = new XiHanHttpRequestOptions().EnableProxyPool();
        var result = await _httpService.GetStringAsync("https://api.example.com/data", options);

        Console.WriteLine($"通过代理池访问: {result.IsSuccess}");
    }

    /// <summary>
    /// 示例6: 原生 HttpClient 使用 SOCKS5 代理（底层实现）
    /// </summary>
    public static async Task Example6_NativeSocks5SupportAsync()
    {
        // 这是 .NET 6+ 原生支持的方式
        var client = new HttpClient(new SocketsHttpHandler
        {
            Proxy = new System.Net.WebProxy("socks5://127.0.0.1:9050")
        });

        var content = await client.GetStringAsync("https://check.torproject.org/");
        Console.WriteLine(content);
    }

    /// <summary>
    /// 示例7: 通过 Tor 进行匿名爬虫
    /// </summary>
    public async Task Example7_AnonymousScrapingWithTorAsync()
    {
        var torProxy = new ProxyConfiguration
        {
            Host = "127.0.0.1",
            Port = 9050,
            Type = ProxyType.Socks5
        };

        var urls = new[]
        {
            "https://example.com/page1",
            "https://example.com/page2",
            "https://example.com/page3"
        };

        foreach (var url in urls)
        {
            var options = new XiHanHttpRequestOptions()
                .SetProxy(torProxy)
                .SetTimeout(TimeSpan.FromSeconds(30))
                .AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var result = await _httpService.GetStringAsync(url, options);

            if (result.IsSuccess)
            {
                Console.WriteLine($"成功抓取: {url}");
                // 处理数据...
            }

            // 每次请求之间延迟，避免被检测
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    /// <summary>
    /// 示例8: SOCKS5 代理故障转移
    /// </summary>
    public async Task Example8_Socks5FailoverAsync()
    {
        var socks5Proxies = new[]
        {
            new ProxyConfiguration { Host = "proxy1.example.com", Port = 1080, Type = ProxyType.Socks5 },
            new ProxyConfiguration { Host = "proxy2.example.com", Port = 1080, Type = ProxyType.Socks5 },
            new ProxyConfiguration { Host = "proxy3.example.com", Port = 1080, Type = ProxyType.Socks5 }
        };

        var url = "https://api.example.com/data";

        foreach (var proxy in socks5Proxies)
        {
            var options = new XiHanHttpRequestOptions()
                .SetProxy(proxy)
                .SetTimeout(TimeSpan.FromSeconds(10));

            var result = await _httpService.GetStringAsync(url, options);

            if (result.IsSuccess)
            {
                Console.WriteLine($"成功通过代理: {proxy.Host}:{proxy.Port}");
                break;
            }

            Console.WriteLine($"代理失败: {proxy.Host}:{proxy.Port}, 尝试下一个...");
        }
    }

    /// <summary>
    /// 示例9: 测试 Tor 连接
    /// </summary>
    public async Task Example9_TestTorConnectionAsync()
    {
        var torProxy = new ProxyConfiguration
        {
            Host = "127.0.0.1",
            Port = 9050,
            Type = ProxyType.Socks5
        };

        var options = new XiHanHttpRequestOptions().SetProxy(torProxy);

        // 获取当前IP
        var ipResult = await _httpService.GetStringAsync("https://api.ipify.org?format=text", options);

        if (ipResult.IsSuccess)
        {
            Console.WriteLine($"当前出口IP: {ipResult.Data}");
        }

        // 检查是否通过 Tor
        var torCheckResult = await _httpService.GetStringAsync("https://check.torproject.org/api/ip", options);

        if (torCheckResult.IsSuccess)
        {
            Console.WriteLine($"Tor 检查结果: {torCheckResult.Data}");
        }
    }

    /// <summary>
    /// 示例10: SOCKS5 代理性能监控
    /// </summary>
    public async Task Example10_MonitorSocks5PerformanceAsync()
    {
        var socks5Proxy = new ProxyConfiguration
        {
            Host = "127.0.0.1",
            Port = 1080,
            Type = ProxyType.Socks5
        };

        var testUrl = "https://www.google.com";
        var testCount = 10;
        var responseTimes = new List<long>();

        for (var i = 0; i < testCount; i++)
        {
            var options = new XiHanHttpRequestOptions().SetProxy(socks5Proxy);
            var result = await _httpService.GetStringAsync(testUrl, options);

            if (result.IsSuccess)
            {
                responseTimes.Add(result.ElapsedMilliseconds);
                Console.WriteLine($"测试 {i + 1}/{testCount}: {result.ElapsedMilliseconds}ms");
            }

            await Task.Delay(1000); // 延迟1秒
        }

        if (responseTimes.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"总测试次数: {testCount}");
            Console.WriteLine($"成功次数: {responseTimes.Count}");
            Console.WriteLine($"平均响应时间: {responseTimes.Average():F2}ms");
            Console.WriteLine($"最快响应: {responseTimes.Min()}ms");
            Console.WriteLine($"最慢响应: {responseTimes.Max()}ms");
        }
    }
}
