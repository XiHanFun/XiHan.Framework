// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// HTTP 协议适配器：路由推导与覆盖、前缀、路由值/请求体/查询串绑定、成功状态码、失败的 Problem Details、不发布的操作。
/// </summary>
public sealed class HttpProtocolTests
{
    /// <summary>
    /// 启动测试宿主；返回映射的端点数。
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client, int Mapped)> StartAsync(params (string Key, string Value)[] configuration)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration.Select(static c => new KeyValuePair<string, string?>(c.Key, c.Value)));
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>());

        var app = builder.Build();
        app.UseXiHan();
        var mapped = app.MapXiHanOperations();
        app.MapXiHanOpenApi();
        await app.StartAsync();
        return (app, app.GetTestClient(), mapped);
    }

    /// <summary>
    /// 解析响应为订单回复。
    /// </summary>
    private static async Task<OrderReply> ReplyOf(HttpResponseMessage response)
        => JsonSerializer.Deserialize(await response.Content.ReadAsByteArrayAsync(), OrdersJsonContext.Default.OrderReply)!;

    /// <summary>
    /// JSON 请求体。
    /// </summary>
    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>
    /// 以订单请求（含 id）描述一个操作。
    /// </summary>
    private static OperationDescriptor<OrdersOperation, OrderRequest, OrderReply> Describe(string id)
        => new OperationDescriptor<OrdersOperation, OrderRequest, OrderReply>(new OperationContract(id), "xihan.test.orders", OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply);

    /// <summary>
    /// 以报表请求（不含 id）描述一个操作。
    /// </summary>
    private static OperationDescriptor<OrderReportOperation, OrderReportRequest, OrderReply> DescribeReport(string id)
        => new OperationDescriptor<OrderReportOperation, OrderReportRequest, OrderReply>(new OperationContract(id), "xihan.test.orders", OrdersJsonContext.Default.OrderReportRequest, OrdersJsonContext.Default.OrderReply);

    /// <summary>
    /// 读 Problem Details 的错误码。
    /// </summary>
    private static async Task<string> CodeOf(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return problem.RootElement.GetProperty("code").GetString()!;
    }

    [Fact]
    public void Convention_DerivesRoutesFromOperationIds()
    {
        Assert.Equal("POST /orders", HttpRouteConvention.Derive(new OperationContract("orders.create"), hasIdentifier: false).ToString());
        Assert.Equal("GET /orders/{id}", HttpRouteConvention.Derive(new OperationContract("orders.get"), hasIdentifier: true).ToString());
        Assert.Equal("GET /order-lines", HttpRouteConvention.Derive(new OperationContract("order-lines.list"), hasIdentifier: false).ToString());
        Assert.Equal("PUT /orders/{id}", HttpRouteConvention.Derive(new OperationContract("orders.update"), hasIdentifier: true).ToString());
        Assert.Equal("DELETE /orders/{id}", HttpRouteConvention.Derive(new OperationContract("orders.delete"), hasIdentifier: true).ToString());
        // 标准动词的形态与请求形状无关
        Assert.Equal("GET /orders/{id}", HttpRouteConvention.Derive(new OperationContract("orders.get"), hasIdentifier: false).ToString());
        Assert.Equal("POST /orders", HttpRouteConvention.Derive(new OperationContract("orders.create"), hasIdentifier: true).ToString());
        // 自定义动词：请求含 id 走实例级，不含走集合级
        Assert.Equal("POST /orders/{id}:cancel", HttpRouteConvention.Derive(new OperationContract("orders.cancel"), hasIdentifier: true).ToString());
        Assert.Equal("POST /orders:export", HttpRouteConvention.Derive(new OperationContract("orders.export"), hasIdentifier: false).ToString());
        Assert.Equal(201, HttpRouteConvention.SuccessStatusCode(new OperationContract("orders.create")));
        Assert.Equal(200, HttpRouteConvention.SuccessStatusCode(new OperationContract("orders.get")));
    }

    [Fact]
    public void Route_ParsesAndValidates()
    {
        var route = HttpRoute.Parse("post /orders:export");

        Assert.Equal("POST", route.Method);
        Assert.Equal("/orders:export", route.Template);
        Assert.Throws<ArgumentException>(static () => HttpRoute.Parse("/orders"));
        Assert.Throws<ArgumentException>(static () => new HttpRoute("HEAD", "/orders"));
        Assert.Throws<ArgumentException>(static () => new HttpRoute("GET", "orders"));
        Assert.Throws<ArgumentException>(static () => new HttpProtocolOptions { RoutePrefix = "api/" });
        Assert.Throws<ArgumentException>(() => new HttpProtocolOptions { Routes = new Dictionary<string, HttpRoute> { ["Bad"] = route } });

        var options = new HttpProtocolOptions { RoutePrefix = "/api", Routes = new Dictionary<string, HttpRoute> { ["orders.export"] = route } };
        Assert.Equal("POST /api/orders:export", options.RouteFor(Describe("orders.export")).ToString());
        Assert.Equal("GET /api/orders/{id}", options.RouteFor(Describe("orders.get")).ToString());
    }

    [Fact]
    public async Task CollectionVerb_RoutesWithoutIdentifier()
    {
        Assert.True(HttpRouteConvention.HasIdentifier(Describe("orders.cancel")));
        Assert.False(HttpRouteConvention.HasIdentifier(DescribeReport("orders.report")));
        Assert.Equal("POST /orders/{id}:cancel", HttpRouteConvention.Derive(Describe("orders.cancel")).ToString());
        Assert.Equal("POST /orders:report", HttpRouteConvention.Derive(DescribeReport("orders.report")).ToString());
        // 没有类型化元数据的描述（插件代理）看 Schema：声明了 id 走实例级，否则集合级
        Assert.True(HttpRouteConvention.HasIdentifier(new SchemaOnlyDescriptor("orders.cancel", OrdersFeature.OrderSchema)));
        Assert.False(HttpRouteConvention.HasIdentifier(new SchemaOnlyDescriptor("orders.report", new OperationSchema("""{"type":"object","properties":{"name":{"type":"string"}}}""", """{"type":"object"}"""))));
        Assert.False(HttpRouteConvention.HasIdentifier(new SchemaOnlyDescriptor("orders.report", schema: null)));
        Assert.Equal("POST /orders:report", HttpRouteConvention.Derive(new SchemaOnlyDescriptor("orders.report", schema: null)).ToString());

        var (app, client, _) = await StartAsync(("XiHan:Protocols:Http:RoutePrefix", "/api"));
        await using var host = app;

        // 请求不含 id 的自定义动词只有集合级路径；实例级路径不存在
        var reply = await ReplyOf(await client.PostAsync("/api/orders:report?page=3", Json("""{"name":"weekly"}""")));
        Assert.Equal("orders.report", reply.Operation);
        Assert.Equal("weekly", reply.Name);
        Assert.Equal(3, reply.Page);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/orders/1:report", Json("{}"))).StatusCode);

        // OpenAPI 里集合级动词没有路径参数
        using var document = JsonDocument.Parse(await (await client.GetAsync("/openapi.json")).Content.ReadAsStringAsync());
        var report = document.RootElement.GetProperty("paths").GetProperty("/api/orders:report").GetProperty("post");
        Assert.Equal("orders.report", report.GetProperty("operationId").GetString());
        Assert.False(report.TryGetProperty("parameters", out var parameters) && parameters.EnumerateArray().Any(static p => p.GetProperty("in").GetString() == "path"));
    }

    [Fact]
    public async Task Endpoints_BindRouteBodyAndQuery_AndReturnJson()
    {
        var (app, client, mapped) = await StartAsync(("XiHan:Protocols:Http:Routes:orders.export", "POST /orders:export"));
        await using var _ = app;
        Assert.Equal(OrdersFeature.HttpOperations.Length, mapped);

        var created = await client.PostAsync("/orders", Json("""{"name":"widget"}"""));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("application/json", created.Content.Headers.ContentType!.MediaType);
        var reply = await ReplyOf(created);
        Assert.Equal("orders.create", reply.Operation);
        Assert.Equal("widget", reply.Name);

        var got = await client.GetAsync("/orders/42");
        Assert.Equal(HttpStatusCode.OK, got.StatusCode);
        Assert.Equal(("orders.get", "42"), (await ReplyOf(got)).Let(static r => (r.Operation, r.Id)));

        var listed = await ReplyOf(await client.GetAsync("/orders?page=2&tags=a&tags=b"));
        Assert.Equal("orders.list", listed.Operation);
        Assert.Equal(2, listed.Page);
        Assert.Equal(["a", "b"], listed.Tags);

        // 路由值优先于请求体：请求体里的 id 被 URL 里的 7 覆盖
        var cancelled = await ReplyOf(await client.PostAsync("/orders/7:cancel", Json("""{"id":"9","reason":"late"}""")));
        Assert.Equal(("orders.cancel", "7", "late"), (cancelled.Operation, cancelled.Id, cancelled.Reason));

        var updated = await ReplyOf(await client.PutAsync("/orders/3", Json("""{"name":"n"}""")));
        Assert.Equal(("orders.update", "3", "n"), (updated.Operation, updated.Id, updated.Name));
        Assert.Equal("orders.delete", (await ReplyOf(await client.DeleteAsync("/orders/3"))).Operation);

        // 配置覆盖的路由
        var exported = await client.PostAsync("/orders:export", Json("{}"));
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Equal("orders.export", (await ReplyOf(exported)).Operation);
    }

    [Fact]
    public async Task Failures_BecomeProblemDetails_AndUnexposedOperationsAreNotMapped()
    {
        var (app, client, _) = await StartAsync();
        await using var _ = app;

        Assert.Equal("KERNEL_VALIDATION_001", await CodeOf(await client.PostAsync("/orders", Json("not json"))));
        Assert.Equal("KERNEL_VALIDATION_001", await CodeOf(await client.PostAsync("/orders", Json("[1]"))));
        var rejected = await client.PostAsync("/orders", Json("""{"name":"fail"}"""));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("ORDERS_CONFLICT_001", await CodeOf(rejected));

        // 只给 MCP 的与内部的操作没有端点；推导的自定义动词路由在没有覆盖时需要 {id}
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/orders/1:secret", Json("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/orders/1:audit", Json("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/orders:export", Json("{}"))).StatusCode);
    }

    [Fact]
    public async Task OpenApi_DescribesRoutesParametersBodiesAndComponents()
    {
        var (app, client, _) = await StartAsync(("XiHan:Protocols:Http:RoutePrefix", "/api"));
        await using var host = app;

        var response = await client.GetAsync("/openapi.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("3.1.0", root.GetProperty("openapi").GetString());

        // GET /api/orders/{id}：id 是路径参数，其余属性是查询参数，required 来自 Schema
        var get = root.GetProperty("paths").GetProperty("/api/orders/{id}").GetProperty("get");
        Assert.Equal("orders.get", get.GetProperty("operationId").GetString());
        var parameters = get.GetProperty("parameters").EnumerateArray().ToDictionary(static p => p.GetProperty("name").GetString()!, static p => p);
        Assert.Equal("path", parameters["id"].GetProperty("in").GetString());
        Assert.Equal("query", parameters["page"].GetProperty("in").GetString());
        Assert.True(parameters["page"].GetProperty("required").GetBoolean());
        Assert.False(parameters["name"].GetProperty("required").GetBoolean());
        Assert.False(get.TryGetProperty("requestBody", out _));

        // POST /api/orders：请求体是提升了 $defs 的 Schema，201 响应带响应 Schema，错误响应引用 ProblemDetails
        var post = root.GetProperty("paths").GetProperty("/api/orders").GetProperty("post");
        var body = post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("#/components/schemas/Line", body.GetProperty("properties").GetProperty("line").GetProperty("$ref").GetString());
        Assert.False(body.TryGetProperty("$defs", out _));
        Assert.Equal("OrderReply", post.GetProperty("responses").GetProperty("201").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("title").GetString());
        Assert.Equal("#/components/schemas/ProblemDetails", post.GetProperty("responses").GetProperty("default").GetProperty("content").GetProperty("application/problem+json").GetProperty("schema").GetProperty("$ref").GetString());
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("ProblemDetails", out _));
        Assert.Equal("sku", schemas.GetProperty("Line").GetProperty("required")[0].GetString());

        // 没有 Schema 的操作也发布，只是没有请求体与响应 Schema
        var cancel = root.GetProperty("paths").GetProperty("/api/orders/{id}:cancel").GetProperty("post");
        Assert.Equal("orders.cancel", cancel.GetProperty("operationId").GetString());
        Assert.False(cancel.TryGetProperty("requestBody", out _));
        Assert.False(root.GetProperty("paths").TryGetProperty("/api/orders/{id}:audit", out _));
    }

    [Fact]
    public async Task RoutePrefix_AppliesToEveryOperation()
    {
        var (app, client, _) = await StartAsync(("XiHan:Protocols:Http:RoutePrefix", "/api"));
        await using var _ = app;

        Assert.Equal("orders.get", (await ReplyOf(await client.GetAsync("/api/orders/1"))).Operation);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/orders/1")).StatusCode);
    }
}
