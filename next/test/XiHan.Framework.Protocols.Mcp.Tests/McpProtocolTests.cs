// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Protocols.Mcp.Server;
using XiHan.Framework.Protocols.Mcp.Tools;
using XiHan.Framework.Protocols.Mcp.Transports;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Protocols.Mcp.Tests;

/// <summary>
/// MCP 协议适配器：tool 名推导与覆盖、生命周期与传输规则、tools/list 的 Schema 与本地化描述、tools/call 经管道执行与失败映射、按契约授权。
/// </summary>
public sealed class McpProtocolTests
{
    /// <summary>
    /// 两个主体：alice 是 editor（可读写订单），bob 是 viewer（只读）。
    /// </summary>
    private static readonly (string Key, string Value)[] _security =
    [
        ("XiHan:Security:InMemory:Principals:alice:ApiKey", "dev-alice-key"),
        ("XiHan:Security:InMemory:Principals:alice:Roles", "editor"),
        ("XiHan:Security:InMemory:Principals:bob:ApiKey", "dev-bob-key"),
        ("XiHan:Security:InMemory:Principals:bob:Roles", "viewer"),
        ("XiHan:Security:InMemory:Grants:role:editor", "orders.read,orders.write"),
        ("XiHan:Security:InMemory:Grants:role:viewer", "orders.read"),
    ];

    /// <summary>
    /// 启动测试宿主：Minimal 组合（本地化）+ 内存安全 + MCP 适配器 + 订单特性，并注册 tool 描述的消息目录。
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(params (string Key, string Value)[] configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(_security.Concat(configuration).Select(static c => new KeyValuePair<string, string?>(c.Key, c.Value)));
        builder.AddXiHan(static xihan => xihan.UseMinimalProfile().UseFeature<InMemorySecurityFeature>().UseFeature<McpProtocolFeature>().UseFeature<OrdersFeature>());
        builder.Services.AddMessageCatalog(MessageCatalog.FromEmbeddedResources("orders", typeof(OrdersFeature)));

        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanMcp();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    /// <summary>
    /// 一条 JSON-RPC 请求。
    /// </summary>
    private static string Rpc(string method, string? parameters = null, int id = 1)
        => parameters is null
            ? $$"""{"jsonrpc":"2.0","id":{{id}},"method":"{{method}}"}"""
            : $$"""{"jsonrpc":"2.0","id":{{id}},"method":"{{method}}","params":{{parameters}}}""";

    /// <summary>
    /// 发一条消息到 /mcp，返回状态码与正文。
    /// </summary>
    private static async Task<(HttpStatusCode Status, string Text)> PostAsync(HttpClient client, string json, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// 解析为独立的 JSON 元素。
    /// </summary>
    private static JsonElement Root(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 发请求并取成功响应的 <c>result</c>。
    /// </summary>
    private static async Task<JsonElement> ResultOf(HttpClient client, string json, params (string Name, string Value)[] headers)
    {
        var (status, text) = await PostAsync(client, json, headers);
        Assert.Equal(HttpStatusCode.OK, status);
        var root = Root(text);
        Assert.False(root.TryGetProperty("error", out _), text);
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        return root.GetProperty("result");
    }

    /// <summary>
    /// 断言一条消息得到 JSON-RPC 错误：HTTP 状态、错误码与 <c>data.code</c> 里的框架错误码。
    /// </summary>
    private static async Task<JsonElement> AssertErrorAsync(HttpClient client, string json, HttpStatusCode status, int code, string errorCode)
    {
        var (actual, text) = await PostAsync(client, json);
        Assert.Equal(status, actual);
        var error = Root(text).GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetInt32());
        Assert.Equal(errorCode, error.GetProperty("data").GetProperty("code").GetString());
        return Root(text);
    }

    [Fact]
    public void ToolNames_FollowConvention_AndOptionsOverride()
    {
        Assert.Equal("orders_create", McpToolNameConvention.Derive(new OperationContract("orders.create")));
        Assert.Equal("order-lines_list", McpToolNameConvention.Derive(new OperationContract("order-lines.list")));
        Assert.True(McpToolName.IsValid("orders_create"));
        Assert.False(McpToolName.IsValid("orders.create"));
        Assert.False(McpToolName.IsValid(new string('a', McpToolName.MaxLength + 1)));
        Assert.Throws<ArgumentException>(static () => new McpProtocolOptions { Tools = new Dictionary<string, string> { ["orders.create"] = "bad name" } });
        Assert.Throws<ArgumentException>(static () => new McpProtocolOptions { Tools = new Dictionary<string, string> { ["Bad"] = "fine" } });

        var options = new McpProtocolOptions { Tools = new Dictionary<string, string> { ["orders.create"] = "place_order" } };
        Assert.Equal("place_order", options.ToolNameFor(new OperationContract("orders.create")));
        Assert.Equal("orders_get", options.ToolNameFor(new OperationContract("orders.get")));

        Assert.Equal(new McpToolAnnotations(ReadOnly: true, Destructive: false, Idempotent: true), McpToolAnnotations.For(new OperationContract("orders.get")));
        Assert.Equal(new McpToolAnnotations(ReadOnly: false, Destructive: true, Idempotent: true), McpToolAnnotations.For(new OperationContract("orders.delete")));
        Assert.Equal(new McpToolAnnotations(ReadOnly: false, Destructive: false, Idempotent: false), McpToolAnnotations.For(new OperationContract("orders.create")));
        Assert.Equal(new McpToolAnnotations(ReadOnly: false, Destructive: true, Idempotent: false), McpToolAnnotations.For(new OperationContract("orders.export")));
        // 声明只读的非标准动词（树形查询、查询变体）与 get / list 同样只读且幂等
        Assert.Equal(new McpToolAnnotations(ReadOnly: true, Destructive: false, Idempotent: true), McpToolAnnotations.For(new OperationContract("departments.tree", readOnly: true)));
        Assert.Equal(new McpToolAnnotations(ReadOnly: true, Destructive: false, Idempotent: true), McpToolAnnotations.For(new OperationContract("users.list-online", readOnly: true)));
        Assert.Equal(new McpToolAnnotations(ReadOnly: false, Destructive: true, Idempotent: false), McpToolAnnotations.For(new OperationContract("departments.tree")));
    }

    [Fact]
    public async Task DuplicateToolNames_FailAtStartup()
    {
        var exception = await Assert.ThrowsAsync<FrameworkException>(static () => StartAsync(("XiHan:Protocols:Mcp:Tools:orders.cancel", "orders_get")));
        Assert.Equal(McpErrors.DuplicateToolName, exception.Code);
    }

    [Fact]
    public async Task Lifecycle_InitializePingNotifications_AndTransportRules()
    {
        var (app, client) = await StartAsync(("XiHan:Protocols:Mcp:ServerName", "Orders"), ("XiHan:Protocols:Mcp:Instructions", "Use the orders tools."));
        await using var host = app;

        var initialized = await ResultOf(client, Rpc("initialize", """{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1"}}"""));
        Assert.Equal(McpServer.ProtocolVersion, initialized.GetProperty("protocolVersion").GetString());
        Assert.Equal("Orders", initialized.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.Equal(FrameworkInfo.Version, initialized.GetProperty("serverInfo").GetProperty("version").GetString());
        Assert.False(initialized.GetProperty("capabilities").GetProperty("tools").GetProperty("listChanged").GetBoolean());
        Assert.Equal("Use the orders tools.", initialized.GetProperty("instructions").GetString());

        // 通知与客户端的响应不答复
        var (accepted, empty) = await PostAsync(client, """{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        Assert.Equal(HttpStatusCode.Accepted, accepted);
        Assert.Equal(string.Empty, empty);
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(client, """{"jsonrpc":"2.0","id":9,"result":{}}""")).Status);

        var pong = await ResultOf(client, Rpc("ping", id: 7));
        Assert.Equal(JsonValueKind.Object, pong.ValueKind);
        Assert.Empty(pong.EnumerateObject());

        // 协议版本头：当前版本放行，其他版本 400
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, Rpc("ping"), (McpServer.ProtocolVersionHeader, McpServer.ProtocolVersion))).Status);
        var (rejected, text) = await PostAsync(client, Rpc("ping"), (McpServer.ProtocolVersionHeader, "2024-11-05"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected);
        Assert.Equal(McpErrors.UnsupportedVersion, Root(text).GetProperty("error").GetProperty("data").GetProperty("code").GetString());

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/mcp")).StatusCode);

        // 不合法的消息 400，其余 JSON-RPC 错误 200；id 原样回写，无 id 可用时为 null
        var parse = await AssertErrorAsync(client, "not json", HttpStatusCode.BadRequest, -32700, McpErrors.InvalidMessage);
        Assert.Equal(JsonValueKind.Null, parse.GetProperty("id").ValueKind);
        await AssertErrorAsync(client, """{"jsonrpc":"1.0","id":1,"method":"ping"}""", HttpStatusCode.BadRequest, -32600, McpErrors.InvalidMessage);
        await AssertErrorAsync(client, """{"jsonrpc":"2.0","id":null,"method":"ping"}""", HttpStatusCode.BadRequest, -32600, McpErrors.InvalidMessage);
        await AssertErrorAsync(client, """{"jsonrpc":"2.0","id":1,"method":"ping","params":[]}""", HttpStatusCode.BadRequest, -32600, McpErrors.InvalidMessage);
        var unknown = await AssertErrorAsync(client, Rpc("resources/list", id: 3), HttpStatusCode.OK, -32601, McpErrors.UnknownMethod);
        Assert.Equal(3, unknown.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task ToolsList_PublishesExposedOperations_WithSchemasAndLocalizedText()
    {
        var (app, client) = await StartAsync(("XiHan:Protocols:Mcp:Tools:orders.cancel", "cancel_order"));
        await using var host = app;

        var result = await ResultOf(client, Rpc("tools/list"), ("Accept-Language", "zh-CN"));
        var tools = result.GetProperty("tools").EnumerateArray().ToList();
        Assert.Equal(["cancel_order", "order-notes_list", "orders_create", "orders_delete", "orders_get", "orders_list", "orders_update"], tools.Select(static t => t.GetProperty("name").GetString()));
        var byName = tools.ToDictionary(static t => t.GetProperty("name").GetString()!, static t => t);

        // 带 Schema 的操作：inputSchema 原样（含 $defs），响应为对象时给 outputSchema；描述按 Accept-Language
        var create = byName["orders_create"];
        Assert.Equal("OrderRequest", create.GetProperty("inputSchema").GetProperty("title").GetString());
        Assert.True(create.GetProperty("inputSchema").GetProperty("$defs").TryGetProperty("Line", out _));
        Assert.Equal("OrderReply", create.GetProperty("outputSchema").GetProperty("title").GetString());
        Assert.Equal("按名称下单。", create.GetProperty("description").GetString());
        Assert.False(create.TryGetProperty("title", out _));
        var createHints = create.GetProperty("annotations");
        Assert.False(createHints.GetProperty("readOnlyHint").GetBoolean());
        Assert.False(createHints.GetProperty("destructiveHint").GetBoolean());
        Assert.False(createHints.GetProperty("idempotentHint").GetBoolean());

        var get = byName["orders_get"];
        Assert.Equal("读取订单", get.GetProperty("title").GetString());
        Assert.Equal("按标识读取一笔订单。", get.GetProperty("description").GetString());
        Assert.True(get.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.True(get.GetProperty("annotations").GetProperty("idempotentHint").GetBoolean());

        // 没有 Schema 的操作：输入为空对象 Schema，没有输出 Schema 与描述
        var list = byName["orders_list"];
        Assert.Equal("object", list.GetProperty("inputSchema").GetProperty("type").GetString());
        Assert.False(list.TryGetProperty("outputSchema", out _));
        Assert.False(list.TryGetProperty("description", out _));
        // 资源段带连字符的操作（order-notes.list）：tool 名保留连字符（order-notes_list），描述按 order_notes.list.description 取——键的首段允许下划线
        Assert.Equal("列出订单附带的备注。", byName["order-notes_list"].GetProperty("description").GetString());
        Assert.True(byName["orders_delete"].GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());

        // 默认区域回退到英文
        var english = await ResultOf(client, Rpc("tools/list"));
        Assert.Equal("Place an order by name.", english.GetProperty("tools").EnumerateArray().Single(static t => t.GetProperty("name").GetString() == "orders_create").GetProperty("description").GetString());
    }

    [Fact]
    public async Task ToolsCall_ExecutesThroughThePipeline_AndMapsFailures()
    {
        var (app, client) = await StartAsync();
        await using var host = app;

        var got = await ResultOf(client, Rpc("tools/call", """{"name":"orders_get","arguments":{"id":"42","page":2,"tags":["a","b"]}}"""));
        Assert.False(got.TryGetProperty("isError", out _));
        var content = got.GetProperty("content")[0];
        Assert.Equal("text", content.GetProperty("type").GetString());
        var structured = got.GetProperty("structuredContent");
        Assert.Equal("orders.get", structured.GetProperty("operation").GetString());
        Assert.Equal("42", structured.GetProperty("id").GetString());
        Assert.Equal(2, structured.GetProperty("page").GetInt32());
        Assert.Equal(["a", "b"], structured.GetProperty("tags").EnumerateArray().Select(static t => t.GetString()));
        // 文本内容就是响应 JSON
        Assert.Equal("orders.get", Root(content.GetProperty("text").GetString()!).GetProperty("operation").GetString());

        // 缺省参数等价于空对象
        var listed = await ResultOf(client, Rpc("tools/call", """{"name":"orders_list"}"""));
        Assert.Equal("orders.list", listed.GetProperty("structuredContent").GetProperty("operation").GetString());

        // 操作失败是 tool 结果而不是协议错误
        var failed = await ResultOf(client, Rpc("tools/call", """{"name":"orders_get","arguments":{"name":"fail","page":1}}"""));
        Assert.True(failed.GetProperty("isError").GetBoolean());
        Assert.StartsWith("ORDERS_CONFLICT_001: ", failed.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.False(failed.TryGetProperty("structuredContent", out _));

        // 参数不符合 tool 公布的 Schema（page 要整数）由内核 Validate 阶段判定，同样是 tool 结果
        var invalid = await ResultOf(client, Rpc("tools/call", """{"name":"orders_get","arguments":{"page":"many"}}"""));
        Assert.True(invalid.GetProperty("isError").GetBoolean());
        Assert.StartsWith("KERNEL_VALIDATION_003: ", invalid.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);

        // 协议错误：未登记的 tool（含只给 HTTP 的与内部的）、非对象参数、缺 name
        await AssertErrorAsync(client, Rpc("tools/call", """{"name":"orders_secret"}"""), HttpStatusCode.OK, -32602, McpErrors.UnknownTool);
        await AssertErrorAsync(client, Rpc("tools/call", """{"name":"orders_audit"}"""), HttpStatusCode.OK, -32602, McpErrors.UnknownTool);
        await AssertErrorAsync(client, Rpc("tools/call", """{"name":"orders_get","arguments":[1]}"""), HttpStatusCode.OK, -32602, McpErrors.InvalidArguments);
        await AssertErrorAsync(client, Rpc("tools/call", """{"arguments":{}}"""), HttpStatusCode.OK, -32602, McpErrors.InvalidMessage);
    }

    [Fact]
    public async Task ToolsCall_AuthorizesByContract()
    {
        var (app, client) = await StartAsync();
        await using var host = app;
        var create = Rpc("tools/call", """{"name":"orders_create","arguments":{"name":"widget","page":1}}""");

        var anonymous = await ResultOf(client, create);
        Assert.True(anonymous.GetProperty("isError").GetBoolean());
        Assert.StartsWith("SECURITY_DENIED_", anonymous.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);

        var alice = await ResultOf(client, create, ("Authorization", "ApiKey dev-alice-key"));
        Assert.Equal("orders.create", alice.GetProperty("structuredContent").GetProperty("operation").GetString());

        var bob = await ResultOf(client, create, ("Authorization", "ApiKey dev-bob-key"));
        Assert.True(bob.GetProperty("isError").GetBoolean());
        Assert.StartsWith("SECURITY_DENIED_004: ", bob.GetProperty("content")[0].GetProperty("text").GetString(), StringComparison.Ordinal);

        // 凭据无效：外层管道在解析 JSON-RPC 之前就拒绝整个请求，认证失败是 401 并质询被拒的方案
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(Rpc("ping"), Encoding.UTF8, "application/json") };
        invalid.Headers.TryAddWithoutValidation("Authorization", "ApiKey nope");
        using var rejected = await client.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal(["ApiKey"], rejected.Headers.WwwAuthenticate.Select(static h => h.ToString()));
        Assert.Equal("SECURITY_DENIED_003", Root(await rejected.Content.ReadAsStringAsync()).GetProperty("code").GetString());
    }
}
