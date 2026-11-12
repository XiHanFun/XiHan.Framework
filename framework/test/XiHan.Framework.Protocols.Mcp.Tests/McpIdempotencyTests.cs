// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.Mcp.Transports;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Protocols.Mcp.Tests;

/// <summary>
/// HTTP 传输上的幂等键交给 <c>tools/call</c>（ADR-0205）
/// </summary>
/// <remarks>
/// 安全审查 S-24：原先外层的 <c>/mcp</c> 请求先按幂等键写下「处理中」，同一个键的内层 <c>tools/call</c> 随即撞上它，工具一次也执行不了
/// </remarks>
public sealed class McpIdempotencyTests
{
    [Fact]
    public async Task ToolCall_WithAnIdempotencyKey_RunsOnce_AndIsReplayed()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        const string Call = """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"orders_get","arguments":{"id":"42","page":1}}}""";

        var first = await PostAsync(client, Call);
        var second = await PostAsync(client, Call);

        // 修之前第一次就得到 isError：内层调用撞上外层写下的「处理中」（CACHING_CONFLICT_002）
        Assert.False(first.GetProperty("result").TryGetProperty("isError", out var failed) && failed.GetBoolean(), first.ToString());
        Assert.Equal(first.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString(), second.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Single(app.Services.GetRequiredService<CallLog>().Calls);
    }

    /// <summary>
    /// 启动带内存缓存（幂等阶段）的 MCP 宿主
    /// </summary>
    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseMinimalProfile().UseFeature<InMemorySecurityFeature>().UseFeature<InMemoryCachingFeature>().UseFeature<McpProtocolFeature>().UseFeature<OrdersFeature>());
        builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("orders", typeof(OrdersFeature)));
        builder.Services.AddSingleton<OriginLog>();
        builder.Services.AddSingleton<CallLog>();

        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanMcp();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 带同一个幂等键发一条消息，返回答复
    /// </summary>
    private static async Task<JsonElement> PostAsync(HttpClient client, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("baggage", "xihan.idempotency-key=mcp-call-1");
        using var response = await client.SendAsync(request);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
