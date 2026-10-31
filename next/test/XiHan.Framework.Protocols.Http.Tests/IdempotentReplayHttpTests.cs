// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Http.Tests.Fixtures;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// HTTP 端点的幂等回放（RFC-0006 幂等第 4 条，ADR-0064）。
/// </summary>
/// <remarks>
/// 回放时端点不执行，<c>UseXiHan()</c> 用端点元数据上的写出方式写出首次调用的状态码与正文；记录不能回放给本次调用时是 Problem Details。
/// </remarks>
public sealed class IdempotentReplayHttpTests
{
    [Fact]
    public async Task Replay_WritesTheFirstResponse_WithTheSameStatusCode()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var first = await client.SendAsync(Post("/orders", """{"page":1,"name":"first"}""", "order-1"));
        // 同一个键、不同的请求体：端点不执行，回放首次调用的响应
        using var second = await client.SendAsync(Post("/orders", """{"page":2,"name":"second"}""", "order-1"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal("application/json; charset=utf-8", second.Content.Headers.ContentType?.ToString());
        var replayed = await second.Content.ReadAsByteArrayAsync();
        Assert.Equal(await first.Content.ReadAsByteArrayAsync(), replayed);
        var reply = JsonSerializer.Deserialize(replayed, OrdersJsonContext.Default.OrderReply)!;
        Assert.Equal("orders.create", reply.Operation);
        Assert.Equal("first", reply.Name);
        Assert.Equal(1, reply.Page);
    }

    [Fact]
    public async Task KeyReused_ByAnotherOperation_IsAConflictProblem()
    {
        var (app, client) = await StartAsync();
        await using var _ = app;

        using var created = await client.SendAsync(Post("/orders", """{"page":1,"name":"first"}""", "order-2"));
        using var reused = await client.SendAsync(Post("/orders:report", """{"name":"monthly"}""", "order-2"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal("application/problem+json", reused.Content.Headers.ContentType?.MediaType);
        Assert.Contains("CACHING_CONFLICT_003", await reused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 启动装配了 HTTP 协议、内存缓存（含幂等中间件）与订单操作的测试宿主。
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<InMemoryCachingFeature>().UseFeature<OrdersFeature>());
        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanOperations();
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    /// <summary>
    /// 带幂等键 Baggage 的 POST 请求。
    /// </summary>
    private static HttpRequestMessage Post(string path, string json, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add("baggage", $"xihan.idempotency-key={idempotencyKey}");
        return request;
    }
}
