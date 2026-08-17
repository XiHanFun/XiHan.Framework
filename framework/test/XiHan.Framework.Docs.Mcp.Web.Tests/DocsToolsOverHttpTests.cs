// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace XiHan.Framework.Docs.Mcp.Web.Tests;

/// <summary>
/// 用官方 MCP 客户端走完整协议握手，验证三个文档工具经 HTTP 真的可用
/// </summary>
/// <remarks>
/// 这一组不自己拼 JSON-RPC，而是让 <see cref="McpClient"/> 去谈协议版本、开会话、调工具：
/// 手拼的报文只能证明服务端没报错，谈不拢协议这类问题它看不出来。
/// </remarks>
public class DocsToolsOverHttpTests : IAsyncLifetime
{
    /// <summary>
    /// 本组共用的密钥，长度须满足 <c>XiHanDocsMcpWebOptionsValidator</c> 的十六字符下限，否则宿主根本起不来
    /// </summary>
    private const string ApiKey = "docs-http-transport-key";

    private DocsMcpWebTestHost _host = null!;
    private McpClient _client = null!;

    /// <summary>
    /// 起宿主并完成 MCP 握手
    /// </summary>
    /// <returns>异步任务</returns>
    public async ValueTask InitializeAsync()
    {
        _host = await DocsMcpWebTestHost.StartAsync(
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:Enabled", "true"),
            new KeyValuePair<string, string?>("XiHan:Docs:Mcp:ApiKey", ApiKey));

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(_host.BaseAddress, "/mcp"),
            // 不用 AutoDetect：探测失败会退回 SSE 再报一个与本用例无关的错，掩盖真正的失败原因
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["X-Api-Key"] = ApiKey }
        });

        _client = await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 断开客户端并停掉宿主
    /// </summary>
    /// <returns>异步任务</returns>
    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task 三个文档工具都列得出来()
    {
        var tools = await _client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            ["list_docs", "read_doc", "search_docs"],
            tools.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task 检索返回真实出处()
    {
        var text = await CallAsync("search_docs", new Dictionary<string, object?>
        {
            ["query"] = "分布式事件什么时候发出去"
        });

        // 与 stdio 服务端的黄金查询集同一条：这个问题应命中事件总线指南
        Assert.Contains("docs/guide/event-bus.md", text, StringComparison.Ordinal);
        Assert.Contains("出处", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 可按出处读回整篇文档()
    {
        var text = await CallAsync("read_doc", new Dictionary<string, object?>
        {
            ["path"] = "docs/guide/event-bus.md"
        });

        Assert.Contains("docs/guide/event-bus.md", text, StringComparison.Ordinal);
        Assert.DoesNotContain("未找到文档", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 可列出全部被索引的文档()
    {
        var text = await CallAsync("list_docs", new Dictionary<string, object?>());

        Assert.Contains("篇文档", text, StringComparison.Ordinal);
        Assert.Contains("docs/guide/event-bus.md", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 问的不是框架文档时明确否认()
    {
        var text = await CallAsync("search_docs", new Dictionary<string, object?>
        {
            ["query"] = "红烧肉怎么做才不腻"
        });

        Assert.Contains("请不要基于猜测作答", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 调一个工具并把返回的文本块拼起来
    /// </summary>
    private async Task<string> CallAsync(string name, Dictionary<string, object?> arguments)
    {
        var result = await _client.CallToolAsync(
            name,
            arguments!,
            cancellationToken: TestContext.Current.CancellationToken);

        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
    }
}
