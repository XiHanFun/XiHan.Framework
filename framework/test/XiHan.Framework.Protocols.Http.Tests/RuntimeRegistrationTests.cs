// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Http.Tests.Fixtures;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 映射之后登记与撤回的操作
/// </summary>
/// <remarks>
/// ADR-0251：路由与 OpenAPI 文档跟随登记表，路由冲突在登记时整组被拒，运行期的分组来源增减分组文档
/// </remarks>
public sealed class RuntimeRegistrationTests
{
    /// <summary>
    /// 插件的清单 ID
    /// </summary>
    private const string Plugin = "xihan.test.plugin";

    /// <summary>
    /// 文档选项：订单特性一个静态分组
    /// </summary>
    private static readonly OpenApiOptions _openApi = new() { Groups = [new OpenApiGroup("orders", 10, ["xihan.test.orders"])] };

    /// <summary>
    /// 启动测试宿主
    /// </summary>
    /// <remarks>
    /// 订单特性 + HTTP 适配器 + OpenAPI；操作端点的返回值上挂一个标签约定，用来看运行期出现的端点也带上它
    /// </remarks>
    private static async Task<(WebApplication App, HttpClient Client, OperationRegistry Registry)> StartAsync(IOpenApiGroupSource? groups = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (groups is not null)
        {
            builder.Services.AddSingleton(groups);
        }

        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>());
        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanOperations().WithMetadata(new TagsAttribute("runtime"));
        app.MapXiHanOpenApi(_openApi);
        await app.StartAsync();
        return (app, app.GetTestClient(), app.Services.GetRequiredService<OperationRegistry>());
    }

    /// <summary>
    /// 插件的一个带 id 的操作
    /// </summary>
    private static OperationDescriptor<OrdersOperation, OrderRequest, OrderReply> Describe(string id)
        => new(new OperationContract(id) { Expose = Expose.Http }, Plugin, OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply);

    /// <summary>
    /// 一个不带 id 的操作：<c>get</c> 与 <c>list</c> 都推导为 <c>GET /{resource}</c>
    /// </summary>
    private static OperationDescriptor<OrderReportOperation, OrderReportRequest, OrderReply> DescribeReport(string id, string owner = Plugin)
        => new(new OperationContract(id) { Expose = Expose.Http }, owner, OrdersJsonContext.Default.OrderReportRequest, OrdersJsonContext.Default.OrderReply);

    /// <summary>
    /// 某个操作此刻的端点
    /// </summary>
    private static Endpoint? EndpointOf(WebApplication app, string id)
        => ((IEndpointRouteBuilder)app).DataSources.SelectMany(static source => source.Endpoints).SingleOrDefault(endpoint => endpoint.Metadata.GetMetadata<OperationDescriptor>()?.Id == id);

    /// <summary>
    /// 文档的 <c>paths</c> 里有没有这条路径
    /// </summary>
    private static async Task<bool> HasPathAsync(HttpClient client, string document, string path)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync(new Uri(document, UriKind.Relative)));
        return json.RootElement.GetProperty("paths").TryGetProperty(path, out _);
    }

    [Fact]
    public async Task OperationsRegisteredAfterMapping_GetARoute_AndLoseItWhenWithdrawn()
    {
        var (app, client, registry) = await StartAsync();
        await using var _ = app;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri("/widgets/7", UriKind.Relative))).StatusCode);

        Assert.True(registry.AddRange(Plugin, [Describe("widgets.get")], []).IsSuccess);
        var response = await client.GetAsync(new Uri("/widgets/7", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reply = JsonSerializer.Deserialize(await response.Content.ReadAsByteArrayAsync(), OrdersJsonContext.Default.OrderReply)!;
        Assert.Equal("widgets.get", reply.Operation);
        Assert.Equal("7", reply.Id);

        // 返回值上的约定作用于此后出现的端点，名字照旧是操作 ID
        var endpoint = Assert.IsType<RouteEndpoint>(EndpointOf(app, "widgets.get"));
        Assert.Contains("runtime", endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
        Assert.Equal("widgets.get", endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName);

        // 撤回之后没有路由，与从没有过这个操作一样；配置阶段登记的照常
        Assert.Equal(1, registry.RemoveOwnedBy(Plugin));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri("/widgets/7", UriKind.Relative))).StatusCode);
        Assert.Null(EndpointOf(app, "widgets.get"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/orders/7?page=1", UriKind.Relative))).StatusCode);

        // 同一归属可以再登记
        Assert.True(registry.AddRange(Plugin, [Describe("widgets.get")], []).IsSuccess);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/widgets/8", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task RouteConflicts_AfterMapping_RejectTheWholeGroup()
    {
        var (app, client, registry) = await StartAsync();
        await using var _ = app;

        // 组内两项推导出同一条路由 GET /gadgets：整组不登记
        var inGroup = registry.AddRange(Plugin, [DescribeReport("gadgets.get"), DescribeReport("gadgets.list")], []);
        Assert.Equal(HttpErrors.DuplicateRoute, inGroup.Error?.Code);
        Assert.Equal(HttpMessages.RouteDuplicate, inGroup.Error?.MessageKey);
        Assert.False(registry.Find("gadgets.get").HasValue);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri("/gadgets", UriKind.Relative))).StatusCode);

        // 与已登记的冲突：被拒的不登记，已有的路由不变
        Assert.True(registry.AddRange(Plugin, [DescribeReport("gadgets.get")], []).IsSuccess);
        var withExisting = registry.AddRange("xihan.test.other", [DescribeReport("gadgets.list", "xihan.test.other")], []);
        Assert.Equal(HttpErrors.DuplicateRoute, withExisting.Error?.Code);
        Assert.False(registry.Find("gadgets.list").HasValue);
        var response = await client.GetAsync(new Uri("/gadgets", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("gadgets.get", JsonSerializer.Deserialize(await response.Content.ReadAsByteArrayAsync(), OrdersJsonContext.Default.OrderReply)!.Operation);
    }

    [Fact]
    public async Task OpenApiDocument_FollowsTheRegistry()
    {
        var (app, client, registry) = await StartAsync();
        await using var _ = app;
        Assert.True(await HasPathAsync(client, "/openapi.json", "/orders/{id}"));
        Assert.False(await HasPathAsync(client, "/openapi.json", "/widgets/{id}"));

        Assert.True(registry.AddRange(Plugin, [Describe("widgets.get")], []).IsSuccess);
        Assert.True(await HasPathAsync(client, "/openapi.json", "/widgets/{id}"));

        Assert.Equal(1, registry.RemoveOwnedBy(Plugin));
        Assert.False(await HasPathAsync(client, "/openapi.json", "/widgets/{id}"));
    }

    [Fact]
    public async Task GroupSources_AddAndRemoveGroupDocuments()
    {
        using var groups = new TestGroupSource();
        var (app, client, registry) = await StartAsync(groups);
        await using var _ = app;

        // 静态分组照常经同一条带参数的路由答复，不是当时的组名为 404
        Assert.True(await HasPathAsync(client, "/openapi/orders.json", "/orders/{id}"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri("/openapi/plugin.json", UriKind.Relative))).StatusCode);

        Assert.True(registry.AddRange(Plugin, [Describe("widgets.get")], []).IsSuccess);
        groups.Set([new OpenApiGroup("plugin", 1000, [Plugin])]);
        Assert.True(await HasPathAsync(client, "/openapi/plugin.json", "/widgets/{id}"));
        Assert.False(await HasPathAsync(client, "/openapi/plugin.json", "/orders/{id}"));
        Assert.False(await HasPathAsync(client, "/openapi/orders.json", "/widgets/{id}"));
        using (var full = JsonDocument.Parse(await client.GetStringAsync(new Uri("/openapi.json", UriKind.Relative))))
        {
            var tagGroups = full.RootElement.GetProperty("x-tagGroups").EnumerateArray().ToList();
            Assert.Contains(tagGroups, static group => group.GetProperty("name").GetString() == "plugin" && group.GetProperty("tags").EnumerateArray().Any(static tag => tag.GetString() == "widgets"));
        }

        // 文档列表按调用时的分组
        Assert.Contains(app.GetXiHanOpenApiDocuments(_openApi), static link => link.Name == "plugin");

        groups.Set([]);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri("/openapi/plugin.json", UriKind.Relative))).StatusCode);
        Assert.DoesNotContain(app.GetXiHanOpenApiDocuments(_openApi), static link => link.Name == "plugin");

        // 来源的分组与选项的合在一起核对：组名重复是应用的编程错误
        groups.Set([new OpenApiGroup("orders", 1000, [Plugin])]);
        Assert.Throws<ArgumentException>(() => OpenApiDocumentSource.FromServices(app.Services, _openApi, []));
    }
}
