// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Http.Tests.Fixtures;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 声明只读的自定义动词经真实宿主走 <c>GET …:{verb}</c>，字段从路由值与查询串绑定；OpenAPI 里同样是 GET 与查询参数（ADR-0041）。
/// </summary>
public sealed class ReadOnlyVerbHttpTests
{
    [Fact]
    public async Task ReadOnlyCustomVerb_IsServedOverGet_AndBindsFromTheQueryString()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<ReadOnlyVerbFeature>());

        await using var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanOperations();
        app.MapXiHanOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();

        // 请求含 id：实例级 GET /orders/{id}:tree，其余字段走查询串
        using var tree = await client.GetAsync("/orders/42:tree?name=root&page=3");
        Assert.Equal(HttpStatusCode.OK, tree.StatusCode);
        using var reply = JsonDocument.Parse(await tree.Content.ReadAsStringAsync());
        Assert.Equal(("orders.tree", "42", "root", 3), (reply.RootElement.GetProperty("operation").GetString(), reply.RootElement.GetProperty("id").GetString(), reply.RootElement.GetProperty("name").GetString(), reply.RootElement.GetProperty("page").GetInt32()));
        // 只读动词没有 POST 路由；没声明只读的自定义动词照旧只有 POST（换方法即路由不到）
        HttpStatusCode[] unrouted = [HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed];
        Assert.Contains((await client.PostAsync("/orders/42:tree", null)).StatusCode, unrouted);
        Assert.Contains((await client.GetAsync("/orders/42:archive")).StatusCode, unrouted);
        using var archive = await client.PostAsync("/orders/42:archive?name=old", null);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        // OpenAPI：只读动词是 GET + 查询参数，没有请求体
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi.json"));
        var treePath = document.RootElement.GetProperty("paths").GetProperty("/orders/{id}:tree");
        Assert.False(treePath.TryGetProperty("post", out _));
        var get = treePath.GetProperty("get");
        Assert.False(get.TryGetProperty("requestBody", out _));
        Assert.Contains(get.GetProperty("parameters").EnumerateArray(), static p => p.GetProperty("name").GetString() == "name" && p.GetProperty("in").GetString() == "query");
        Assert.True(document.RootElement.GetProperty("paths").GetProperty("/orders/{id}:archive").TryGetProperty("post", out _));

        await app.StopAsync();
    }
}
