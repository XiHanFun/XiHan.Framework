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
        Assert.Equal("POST /orders", HttpRouteConvention.Derive(new OperationContract("orders.create")).ToString());
        Assert.Equal("GET /orders/{id}", HttpRouteConvention.Derive(new OperationContract("orders.get")).ToString());
        Assert.Equal("GET /order-lines", HttpRouteConvention.Derive(new OperationContract("order-lines.list")).ToString());
        Assert.Equal("PUT /orders/{id}", HttpRouteConvention.Derive(new OperationContract("orders.update")).ToString());
        Assert.Equal("DELETE /orders/{id}", HttpRouteConvention.Derive(new OperationContract("orders.delete")).ToString());
        Assert.Equal("POST /orders/{id}:cancel", HttpRouteConvention.Derive(new OperationContract("orders.cancel")).ToString());
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
        Assert.Equal("POST /api/orders:export", options.RouteFor(new OperationContract("orders.export")).ToString());
        Assert.Equal("GET /api/orders/{id}", options.RouteFor(new OperationContract("orders.get")).ToString());
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
    public async Task RoutePrefix_AppliesToEveryOperation()
    {
        var (app, client, _) = await StartAsync(("XiHan:Protocols:Http:RoutePrefix", "/api"));
        await using var _ = app;

        Assert.Equal("orders.get", (await ReplyOf(await client.GetAsync("/api/orders/1"))).Operation);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/orders/1")).StatusCode);
    }
}
