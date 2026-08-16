// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using XiHan.Framework.AI.Abstractions.Skills;
using XiHan.Framework.AI.Skills;
using XiHan.Framework.Web.Mcp.Extensions;
using XiHan.Framework.Web.Mcp.Extensions.DependencyInjection;
using XiHan.Framework.Web.Mcp.Options;

namespace XiHan.Framework.Web.Mcp.Tests;

/// <summary>
/// 进程内测试宿主：按给定配置装配 <c>AddXiHanWebMcp</c> + <c>MapXiHanMcp</c>，在回环临时端口上跑真实 HTTP
/// </summary>
/// <remarks>
/// 为什么必须起真实服务端而不是只调扩展方法：被测的两个性质都只在 HTTP 层才有意义——
/// 「端点不存在」要由 404 来证，「密钥不对」要由端点过滤器在真实请求管道里跑出 401 来证。
/// 只断言服务集合的话，一个映射了无鉴权端点的实现照样能全绿。
/// <para>
/// 端口固定写 0 交给操作系统分配，CI 上并行跑多个测试项目也不会撞；只绑 127.0.0.1，不出网。
/// </para>
/// </remarks>
internal sealed class McpTestHost : IAsyncDisposable
{
    /// <summary>
    /// 单次请求的等待上限，够宽以容忍 CI 冷启动，又不至于让挂起的测试拖死整个套件
    /// </summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 一条最小可用的 initialize 请求；握手是所有 MCP 会话的第一步，用它探端点最贴近真实客户端
    /// </summary>
    private const string InitializeRequestJson =
        """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"xihan-web-mcp-tests","version":"1.0"}}}
        """;

    private readonly WebApplication _app;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="app">已启动的宿主</param>
    /// <param name="mcpOptions">宿主实际生效的 MCP 配置</param>
    /// <param name="baseAddress">实际监听地址</param>
    private McpTestHost(WebApplication app, XiHanMcpOptions mcpOptions, Uri baseAddress)
    {
        _app = app;
        McpOptions = mcpOptions;
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// 宿主实际生效的 MCP 配置
    /// </summary>
    public XiHanMcpOptions McpOptions { get; }

    /// <summary>
    /// 实际监听地址（端口由操作系统分配）
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// 按给定配置启动宿主
    /// </summary>
    /// <param name="enabled">是否启用 MCP</param>
    /// <param name="apiKey">访问密钥，null 表示配置里根本没有这个键</param>
    /// <param name="skills">要注册进技能注册表的技能</param>
    /// <returns>已启动的宿主</returns>
    public static async Task<McpTestHost> StartAsync(bool enabled, string? apiKey, params IAiSkill[] skills)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production
        });

        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        // 放在配置源链的最后，压过开发机上可能存在的 XiHan__AI__Mcp__* 环境变量，
        // 否则测试结果会随执行机器的环境而变
        builder.Configuration.AddInMemoryCollection(BuildSettings(enabled, apiKey));

        _ = builder.Services.AddXiHanWebMcp(builder.Configuration);

        // 技能注册表本由 AddXiHanAI 注册，这里只补这一件依赖：
        // SkillMcpToolsConfigurator 要它，而拉起整个 AI 模块会连带一串与本包无关的 provider 配置
        builder.Services.TryAddSingleton<IAiSkillRegistry, DefaultAiSkillRegistry>();
        foreach (var skill in skills)
        {
            _ = builder.Services.AddSingleton<IAiSkill>(skill);
        }

        var app = builder.Build();
        try
        {
            var mcpOptions = app.Services.GetRequiredService<IOptions<XiHanMcpOptions>>().Value;
            _ = app.MapXiHanMcp(mcpOptions);

            await app.StartAsync();

            return new McpTestHost(app, mcpOptions, ResolveBaseAddress(app));
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// 向 MCP 端点发一条 initialize 请求
    /// </summary>
    /// <param name="configureRequest">对请求的补充配置，通常用来加密钥请求头</param>
    /// <returns>状态码与响应体</returns>
    public async Task<McpHttpResponse> PostInitializeAsync(Action<HttpRequestMessage>? configureRequest = null)
    {
        using var timeout = new CancellationTokenSource(RequestTimeout);
        using var client = new HttpClient { Timeout = RequestTimeout };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseAddress, McpOptions.Path))
        {
            Content = new StringContent(InitializeRequestJson, Encoding.UTF8, "application/json")
        };

        // Streamable HTTP 传输要求 Accept 同时列出这两种，缺一会被服务端以 406 拒绝，
        // 那样 401/404 的断言就测不到该测的东西了
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        configureRequest?.Invoke(request);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        var body = await response.Content.ReadAsStringAsync(timeout.Token);

        return new McpHttpResponse(response.StatusCode, body);
    }

    /// <summary>
    /// 以真实 MCP 客户端连上端点并完成握手
    /// </summary>
    /// <param name="headerName">携带密钥的请求头名</param>
    /// <param name="headerValue">请求头值</param>
    /// <returns>已握手的会话</returns>
    public async Task<McpTestSession> ConnectAsync(string headerName, string headerValue)
    {
        using var timeout = new CancellationTokenSource(RequestTimeout);

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Name = "xihan-web-mcp-tests",
            Endpoint = new Uri(BaseAddress, McpOptions.Path),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [headerName] = headerValue
            }
        });

        try
        {
            var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
            return new McpTestSession(transport, client);
        }
        catch
        {
            await transport.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// 停止并释放宿主，断言失败时也不留下占着端口的监听器
    /// </summary>
    /// <returns>释放任务</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await _app.StopAsync();
        }
        finally
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>
    /// 组装内存配置项，宿主与「只看服务集合」的测试共用同一套，两边看到的配置必然一致
    /// </summary>
    /// <param name="enabled">是否启用</param>
    /// <param name="apiKey">访问密钥，null 表示不写这个键</param>
    /// <returns>配置键值对</returns>
    public static Dictionary<string, string?> BuildSettings(bool enabled, string? apiKey)
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [XiHanMcpOptions.SectionName + ":Enabled"] = enabled ? "true" : "false"
        };

        if (apiKey is not null)
        {
            settings[XiHanMcpOptions.SectionName + ":ApiKey"] = apiKey;
        }

        return settings;
    }

    /// <summary>
    /// 取宿主启动后真正绑定的地址
    /// </summary>
    /// <param name="app">已启动的宿主</param>
    /// <returns>形如 http://127.0.0.1:54321 的地址</returns>
    /// <exception cref="InvalidOperationException">宿主没有解析出任何监听地址</exception>
    private static Uri ResolveBaseAddress(WebApplication app)
    {
        var address = app.Urls.FirstOrDefault()
            ?? throw new InvalidOperationException("测试宿主启动后没有解析出监听地址。");

        return new Uri(address, UriKind.Absolute);
    }
}

/// <summary>
/// 一次 HTTP 往返的结果
/// </summary>
/// <param name="StatusCode">状态码</param>
/// <param name="Body">响应体</param>
internal sealed record McpHttpResponse(HttpStatusCode StatusCode, string Body);

/// <summary>
/// 一次已握手的 MCP 会话，连同它底下的传输一并释放
/// </summary>
internal sealed class McpTestSession : IAsyncDisposable
{
    private readonly HttpClientTransport _transport;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="transport">底层传输</param>
    /// <param name="client">已握手的客户端</param>
    public McpTestSession(HttpClientTransport transport, McpClient client)
    {
        _transport = transport;
        Client = client;
    }

    /// <summary>
    /// 已握手的 MCP 客户端
    /// </summary>
    public McpClient Client { get; }

    /// <summary>
    /// 释放会话与传输
    /// </summary>
    /// <returns>释放任务</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Client.DisposeAsync();
        }
        finally
        {
            await _transport.DisposeAsync();
        }
    }
}
