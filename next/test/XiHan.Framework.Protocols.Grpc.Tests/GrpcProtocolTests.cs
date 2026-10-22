// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Profiles.Minimal;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Protocols.Grpc.Tests;

/// <summary>
/// gRPC 协议适配器端到端：真实的 gRPC 客户端经 TestServer 调用一元方法，请求按线格式手工拼出，响应按 Schema 解码；失败映射、尾部元数据、本地化、按契约授权、<c>.proto</c> 端点、启动校验。
/// </summary>
public sealed class GrpcProtocolTests
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
    /// 字节数组直通的编组器：测试自己拼线格式。
    /// </summary>
    private static readonly Marshaller<byte[]> _bytes = Marshallers.Create<byte[]>(static b => b, static b => b);

    /// <summary>
    /// 启动测试宿主：Minimal 组合（本地化）+ 内存安全 + gRPC 适配器 + 订单特性。
    /// </summary>
    private static async Task<(WebApplication App, GrpcChannel Channel, HttpClient Client)> StartAsync(Action<AppBuilder>? extra = null, params (string Key, string Value)[] configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(_security.Concat(configuration).Select(static c => new KeyValuePair<string, string?>(c.Key, c.Value)));
        builder.AddXiHan(xihan =>
        {
            xihan.UseMinimalProfile().UseFeature<InMemorySecurityFeature>().UseFeature<GrpcProtocolFeature>().UseFeature<OrdersFeature>();
            extra?.Invoke(xihan);
        });

        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanGrpc();
        app.MapXiHanProto();
        await app.StartAsync();
        var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions { HttpHandler = app.GetTestServer().CreateHandler() });
        return (app, channel, app.GetTestClient());
    }

    /// <summary>
    /// 发起一元调用。
    /// </summary>
    private static AsyncUnaryCall<byte[]> Call(GrpcChannel channel, string service, string method, byte[] request, params (string Name, string Value)[] headers)
    {
        var metadata = new Metadata();
        foreach (var (name, value) in headers)
        {
            metadata.Add(name, value);
        }

        return channel.CreateCallInvoker().AsyncUnaryCall(new Method<byte[], byte[]>(MethodType.Unary, service, method, _bytes, _bytes), null, new CallOptions(metadata), request);
    }

    /// <summary>
    /// 按响应 Schema 解码为 JSON 元素。
    /// </summary>
    private static JsonElement Reply(byte[] bytes)
    {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(output))
        {
            ProtoDecoder.Decode(OrdersFeature.ReplyMessage, bytes, writer);
        }

        using var document = JsonDocument.Parse(output.WrittenMemory);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task UnaryCall_DecodesRequest_RunsThePipeline_AndEncodesResponse()
    {
        var (app, channel, _) = await StartAsync();
        await using var host = app;
        using var scope = channel;

        // 手工拼出的请求：id = "42"，page = 2，tags = ["a", "b"]
        var request = ProtoBytes.Concat(ProtoBytes.StringField(1, "42"), ProtoBytes.VarintField(3, 2), ProtoBytes.StringField(4, "a"), ProtoBytes.StringField(4, "b"));
        using var call = Call(channel, "Orders", "Get", request);
        var response = await call.ResponseAsync;

        // 响应线格式：operation = 1 是第一个字段
        Assert.True(response.AsSpan().StartsWith(ProtoBytes.StringField(1, "orders.get")));
        var reply = Reply(response);
        Assert.Equal("orders.get", reply.GetProperty("operation").GetString());
        Assert.Equal("42", reply.GetProperty("id").GetString());
        Assert.Equal(2, reply.GetProperty("page").GetInt32());
        Assert.Equal(["a", "b"], reply.GetProperty("tags").EnumerateArray().Select(static t => t.GetString()));
        Assert.False(reply.TryGetProperty("name", out _));
        Assert.Equal(32, call.GetTrailers().GetValue("xihan-trace-id")!.Length);

        // 空请求：proto3 默认值；连字符资源成 PascalCase 服务
        using var empty = Call(channel, "OrderLines", "List", []);
        Assert.Equal("order-lines.list", Reply(await empty.ResponseAsync).GetProperty("operation").GetString());
    }

    [Fact]
    public async Task Failures_BecomeStatuses_WithErrorCodeTrailers_AndLocalizedDetail()
    {
        var (app, channel, _) = await StartAsync();
        await using var host = app;
        using var scope = channel;

        // 操作返回的失败：CONFLICT → Aborted，错误码在尾部元数据
        var conflict = await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Orders", "Get", ProtoBytes.StringField(2, "fail")).ResponseAsync);
        Assert.Equal(StatusCode.Aborted, conflict.StatusCode);
        Assert.Equal("ORDERS_CONFLICT_001", conflict.Trailers.GetValue("xihan-error-code"));
        Assert.NotNull(conflict.Trailers.GetValue("xihan-trace-id"));

        // 不是合法 protobuf：InvalidArgument + 协议错误码
        var malformed = await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Orders", "Get", [0x0A, 0x05, 0x61]).ResponseAsync);
        Assert.Equal(StatusCode.InvalidArgument, malformed.StatusCode);
        Assert.Equal(GrpcErrors.RequestMalformed, malformed.Trailers.GetValue("xihan-error-code"));
        Assert.Contains("orders.get", malformed.Status.Detail, StringComparison.Ordinal);

        // 未登记的方法：只给 HTTP 的、内部的、不存在的服务
        Assert.Equal(StatusCode.Unimplemented, (await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Orders", "Secret", []).ResponseAsync)).StatusCode);
        Assert.Equal(StatusCode.Unimplemented, (await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Orders", "Audit", []).ResponseAsync)).StatusCode);
        Assert.Equal(StatusCode.Unimplemented, (await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Nope", "Get", []).ResponseAsync)).StatusCode);

        // 文案按 accept-language 翻译
        var denied = await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Orders", "Create", [], ("authorization", "ApiKey dev-bob-key"), ("accept-language", "ja-JP")).ResponseAsync);
        Assert.Equal(StatusCode.PermissionDenied, denied.StatusCode);
        Assert.Equal("権限 'orders.write' がありません。", denied.Status.Detail);
    }

    [Fact]
    public async Task Authorization_FollowsTheContract()
    {
        var (app, channel, _) = await StartAsync();
        await using var host = app;
        using var scope = channel;
        var request = ProtoBytes.StringField(2, "widget");

        var anonymous = await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Orders", "Create", request).ResponseAsync);
        Assert.Equal(StatusCode.Unauthenticated, anonymous.StatusCode);
        Assert.Equal("SECURITY_DENIED_001", anonymous.Trailers.GetValue("xihan-error-code"));

        var bob = await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Orders", "Create", request, ("authorization", "ApiKey dev-bob-key")).ResponseAsync);
        Assert.Equal(StatusCode.PermissionDenied, bob.StatusCode);
        Assert.Equal("SECURITY_DENIED_004", bob.Trailers.GetValue("xihan-error-code"));

        var invalid = await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Orders", "Create", request, ("authorization", "ApiKey nope")).ResponseAsync);
        Assert.Equal(StatusCode.PermissionDenied, invalid.StatusCode);
        Assert.Equal("SECURITY_DENIED_003", invalid.Trailers.GetValue("xihan-error-code"));

        using var alice = Call(channel, "Orders", "Create", request, ("authorization", "ApiKey dev-alice-key"));
        Assert.Equal(("orders.create", "widget"), Reply(await alice.ResponseAsync).Let(static r => (r.GetProperty("operation").GetString(), r.GetProperty("name").GetString())));
    }

    [Fact]
    public async Task ProtoEndpoint_ServesTheGeneratedFile_WithPackageAndOverrides()
    {
        var (app, channel, client) = await StartAsync(null, ("XiHan:Protocols:Grpc:Package", "acme.orders"), ("XiHan:Protocols:Grpc:Methods:orders.list", "Orders/ListAll"));
        await using var host = app;
        using var scope = channel;

        var proto = await client.GetStringAsync("/operations.proto");
        Assert.Contains("package acme.orders;", proto, StringComparison.Ordinal);
        Assert.Contains("message OrderRequest {", proto, StringComparison.Ordinal);
        Assert.Contains("message OrderReply {", proto, StringComparison.Ordinal);
        Assert.Contains("service OrderLines {\n  // order-lines.list\n  rpc List (OrderRequest) returns (OrderReply);\n}", proto, StringComparison.Ordinal);
        Assert.Contains("  rpc ListAll (OrderRequest) returns (OrderReply);", proto, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", proto, StringComparison.Ordinal);
        Assert.DoesNotContain("Audit", proto, StringComparison.Ordinal);

        // 有包名时方法路径带包前缀
        using var call = Call(channel, "acme.orders.Orders", "ListAll", []);
        Assert.Equal("orders.list", Reply(await call.ResponseAsync).GetProperty("operation").GetString());
        Assert.Equal(StatusCode.Unimplemented, (await Assert.ThrowsAsync<RpcException>(async () => await Call(channel, "Orders", "ListAll", []).ResponseAsync)).StatusCode);
    }

    [Fact]
    public async Task SchemalessOrDuplicateOperations_FailAtStartup()
    {
        var schemaless = await Assert.ThrowsAsync<FrameworkException>(static () => StartAsync(static xihan => xihan.UseFeature<SchemalessFeature>()));
        Assert.Equal(GrpcErrors.SchemaUnsupported, schemaless.Code);

        var duplicate = await Assert.ThrowsAsync<FrameworkException>(static () => StartAsync(null, ("XiHan:Protocols:Grpc:Methods:orders.get", "Orders/List")));
        Assert.Equal(GrpcErrors.DuplicateMethod, duplicate.Code);
    }
}
