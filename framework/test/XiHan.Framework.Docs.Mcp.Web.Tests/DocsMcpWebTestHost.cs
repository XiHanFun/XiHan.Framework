// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Docs.Mcp.Sources;
using XiHan.Framework.Docs.Mcp.Web.Extensions;
using XiHan.Framework.Docs.Mcp.Web.Extensions.DependencyInjection;
using XiHan.Framework.Docs.Mcp.Web.Options;

namespace XiHan.Framework.Docs.Mcp.Web.Tests;

/// <summary>
/// 用真实 Kestrel 起一个文档 MCP Server，供用例发真实 HTTP 请求
/// </summary>
/// <remarks>
/// 绑 127.0.0.1:0（临时端口）而不是固定端口：CI 上并发跑测试时固定端口会互相抢占，
/// 而且本机也常有别的进程占着。用例之间不共享宿主，避免一个用例的配置影响另一个。
/// <para>
/// 装配走的是产品代码的两个扩展方法 <c>AddXiHanDocsMcpWeb</c> 与 <c>MapXiHanDocsMcp</c>，
/// 和 <c>Program.cs</c> 调的是同一对——fail-closed 判定就写在这两个方法里，
/// 所以这里的断言按得住的正是真实的那份逻辑。
/// </para>
/// </remarks>
internal sealed class DocsMcpWebTestHost : IAsyncDisposable
{
    /// <summary>
    /// initialize 请求体，MCP 握手的第一条消息；协议版本取 SDK 2.2.0 支持的其中一个
    /// </summary>
    private const string InitializePayload =
        """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"docs-mcp-web-tests","version":"1.0.0"}}}
        """;

    private static readonly Lazy<string> LazyRepositoryRoot = new(() =>
        DocSourceLocator.ResolveRepositoryRoot(
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("XIHAN_DOCS_ROOT")));

    private readonly WebApplication _app;

    private DocsMcpWebTestHost(WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
        Client = new HttpClient { BaseAddress = baseAddress };
    }

    /// <summary>
    /// 仓库根，索引与用例断言中的文档路径都以它为基准
    /// </summary>
    public static string RepositoryRoot => LazyRepositoryRoot.Value;

    /// <summary>
    /// 指向本宿主的 HttpClient
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// 本宿主实际监听的地址，端口由系统分配
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// 按给定配置启动一个宿主
    /// </summary>
    /// <param name="settings">写进内存配置源的键值对，键用 <c>XiHan:Docs:Mcp:*</c> 形式</param>
    /// <returns>已启动的宿主</returns>
    public static async Task<DocsMcpWebTestHost> StartAsync(params KeyValuePair<string, string?>[] settings)
    {
        var builder = WebApplication.CreateBuilder();

        // 清掉默认配置源：本机 appsettings.json 与 XiHan__Docs__Mcp__* 环境变量都不该左右用例判定
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(settings);

        builder.Logging.ClearProviders();
        builder.Services.AddXiHanDocsMcpWeb(builder.Configuration, RepositoryRoot);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        var options = app.Services.GetRequiredService<IOptions<XiHanDocsMcpWebOptions>>().Value;
        _ = app.MapXiHanDocsMcp(options);

        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .First();

        return new DocsMcpWebTestHost(app, new Uri(address));
    }

    /// <summary>
    /// 构造一条 MCP initialize 请求，未附带任何鉴权头
    /// </summary>
    /// <param name="path">端点路径</param>
    /// <returns>可直接发送的请求</returns>
    public static HttpRequestMessage CreateInitializeRequest(string path = "/mcp")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(InitializePayload, Encoding.UTF8, "application/json")
        };

        // 流式 HTTP 传输要求两种媒体类型都被接受，缺一会被判 406 而不是进到处理器
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        return request;
    }

    /// <summary>
    /// 发送一条 MCP initialize 请求，只读响应头不读流，避免 SSE 长连接把用例挂住
    /// </summary>
    /// <param name="configure">在发送前追加请求头，可为空</param>
    /// <returns>响应，调用方负责释放</returns>
    public async Task<HttpResponseMessage> SendInitializeAsync(Action<HttpRequestMessage>? configure = null)
    {
        var request = CreateInitializeRequest();
        configure?.Invoke(request);

        return await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 手写 HTTP/1.1 报文直发套接字，发一条 MCP initialize 请求，返回状态码
    /// </summary>
    /// <remarks>
    /// 为什么不用 <see cref="HttpClient"/>：同名请求头添两次时，<see cref="HttpClient"/> 会在发出前
    /// 折成一行逗号分隔的值，服务端拿到的 <c>StringValues</c> 只有一个元素——那样就测不到
    /// 「服务端拿到两个值该怎么办」。手写报文才能把两行同名头真的送上线。
    /// </remarks>
    /// <param name="extraHeaderLines">追加的请求头行，形如 <c>X-Api-Key: value</c></param>
    /// <returns>响应状态码</returns>
    public async Task<int> SendRawInitializeAsync(params string[] extraHeaderLines)
    {
        var token = TestContext.Current.CancellationToken;
        var payload = Encoding.UTF8.GetBytes(InitializePayload);

        var head = new StringBuilder()
            .Append("POST /mcp HTTP/1.1\r\n")
            .Append($"Host: {BaseAddress.Host}:{BaseAddress.Port}\r\n")
            .Append("Content-Type: application/json\r\n")
            .Append("Accept: application/json, text/event-stream\r\n");

        foreach (var line in extraHeaderLines)
        {
            head.Append(line).Append("\r\n");
        }

        head.Append($"Content-Length: {payload.Length}\r\n")
            .Append("Connection: close\r\n\r\n");

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(IPAddress.Loopback, BaseAddress.Port, token);

        var stream = tcp.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()), token);
        await stream.WriteAsync(payload, token);
        await stream.FlushAsync(token);

        // 只读状态行；200 时后面是 SSE 长流，读全了会把用例挂住
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var statusLine = await reader.ReadLineAsync(token);

        Assert.NotNull(statusLine);
        return int.Parse(statusLine.Split(' ')[1], CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 停止宿主并释放资源
    /// </summary>
    /// <returns>异步任务</returns>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
