// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Http.Tests.Fixtures;
using XiHan.Framework.Security.Abstractions.Authentication;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// OpenAPI 生成器的四项能力：标题 / 版本 / <c>servers</c> 可由应用指定；装配 Bearer 认证时发布方案与全局 <c>security</c>；操作标题与说明按 <c>Accept-Language</c> 取消息目录文案并分区域缓存；
/// 普通端点按元数据收录（二进制 / 表单 / 文本按内容类型，JSON 只能引用已登记操作的根 Schema，引用不到即失败）。
/// </summary>
public sealed class OpenApiExtensionsTests
{
    /// <summary>
    /// 操作标题与说明的消息目录：只有英文与简体中文，只给 <c>orders.get</c> 与 <c>order-filters.list</c>。
    /// </summary>
    private static readonly MessageCatalog _descriptions = new("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        // 资源段带连字符的操作：键的首段是连字符换成下划线的形态；order-filters.list 只有标题
        ["en-US"] = new Dictionary<string, string> { ["orders.get.title"] = "Get an order", ["orders.get.description"] = "Get one order by its ID.", ["order_filters.list.title"] = "List order filters" },
        ["zh-CN"] = new Dictionary<string, string> { ["orders.get.title"] = "读取订单", ["orders.get.description"] = "按 ID 读取一个订单。" },
    });

    /// <summary>
    /// 启动装配了 HTTP 协议与订单特性的测试宿主；可加本地化、Bearer 认证桩、文档选项与额外端点。
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> StartAsync(bool localization = false, bool bearer = false, OpenApiOptions? options = null, Action<WebApplication>? map = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (bearer)
        {
            builder.Services.AddSingleton<IAuthenticationHandler, BearerHandler>();
        }

        builder.Services.AddMessageCatalog(_descriptions);
        builder.AddXiHan(xihan =>
        {
            xihan.UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>();
            if (localization)
            {
                xihan.UseFeature<InMemoryLocalizationFeature>();
            }
        });

        var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanOperations();
        map?.Invoke(app);
        app.MapXiHanOpenApi(options);
        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    /// <summary>
    /// 取文档；可带 <c>Accept-Language</c>。
    /// </summary>
    private static async Task<(JsonDocument Document, HttpResponseMessage Response)> DocumentOf(HttpClient client, string? acceptLanguage = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi.json");
        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (JsonDocument.Parse(await response.Content.ReadAsStringAsync()), response);
    }

    /// <summary>
    /// 以订单请求描述一个契约。
    /// </summary>
    private static OperationDescriptor<OrdersOperation, OrderRequest, OrderReply> Describe(OperationContract contract)
        => new(contract, "xihan.test.orders", OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply, OrdersFeature.OrderSchema);

    /// <summary>
    /// 手工构造一个带元数据的路由端点。
    /// </summary>
    private static RouteEndpoint Endpoint(string method, string template, params object[] metadata)
        => new(static _ => Task.CompletedTask, RoutePatternFactory.Parse(template), 0, new EndpointMetadataCollection([new HttpMethodMetadata([method]), .. metadata]), $"{method} {template}");

    [Fact]
    public async Task MapXiHanOpenApi_WithOptions_WritesTitleVersionAndServers()
    {
        var options = new OpenApiOptions
        {
            Title = "Orders API",
            Version = "2.1.0",
            Servers = [new OpenApiServer("https://api.example.com", "Production"), new OpenApiServer("/api")],
        };
        var (app, client) = await StartAsync(options: options);
        await using var host = app;

        var (document, _) = await DocumentOf(client);
        using var owned = document;
        var root = document.RootElement;
        Assert.Equal("Orders API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("2.1.0", root.GetProperty("info").GetProperty("version").GetString());
        var servers = root.GetProperty("servers").EnumerateArray().ToList();
        Assert.Equal(["https://api.example.com", "/api"], servers.Select(static s => s.GetProperty("url").GetString()));
        Assert.Equal("Production", servers[0].GetProperty("description").GetString());
        Assert.False(servers[1].TryGetProperty("description", out _));
        // 没有装配本地化：没有 Content-Language，也没有标题与说明
        Assert.False(root.GetProperty("paths").GetProperty("/orders/{id}").GetProperty("get").TryGetProperty("summary", out _));
        Assert.False(root.GetProperty("paths").GetProperty("/orders/{id}").GetProperty("get").TryGetProperty("description", out _));
    }

    [Fact]
    public async Task OpenApi_PublishesBearerScheme_AndMarksUnprotectedOperations()
    {
        var (app, client) = await StartAsync(bearer: true);
        await using var host = app;

        var (document, _) = await DocumentOf(client);
        using var owned = document;
        var root = document.RootElement;
        var bearer = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());
        // 全局要求 Bearer；订单特性的操作都没有授权要求，各自标 security: []
        var security = Assert.Single(root.GetProperty("security").EnumerateArray());
        Assert.Equal(0, security.GetProperty("bearer").GetArrayLength());
        Assert.Equal(0, root.GetProperty("paths").GetProperty("/orders/{id}").GetProperty("get").GetProperty("security").GetArrayLength());
    }

    [Fact]
    public void Build_ProtectedOperationsInheritGlobalSecurity_AndNonBearerSchemesPublishNothing()
    {
        var registry = new OperationRegistry();
        registry.Add(Describe(new OperationContract("orders.create", expose: Expose.Http, permission: "orders.write"))).ThrowIfFailure();
        registry.Add(Describe(new OperationContract("orders.list", expose: Expose.Http, policy: "authenticated"))).ThrowIfFailure();
        registry.Add(Describe(new OperationContract("orders.get", expose: Expose.Http))).ThrowIfFailure();

        using var bearer = JsonDocument.Parse(OpenApiDocumentBuilder.Build(new OpenApiDocumentSource(registry, new HttpProtocolOptions(), null) { AuthenticationSchemes = ["bearer"] }));
        var paths = bearer.RootElement.GetProperty("paths");
        // 有权限或策略要求的操作不写 security（沿用全局），无要求的写空数组；权限码与策略名仍只走 x-xihan-*
        Assert.False(paths.GetProperty("/orders").GetProperty("post").TryGetProperty("security", out _));
        Assert.False(paths.GetProperty("/orders").GetProperty("get").TryGetProperty("security", out _));
        Assert.Equal(0, paths.GetProperty("/orders/{id}").GetProperty("get").GetProperty("security").GetArrayLength());
        Assert.Equal("orders.write", paths.GetProperty("/orders").GetProperty("post").GetProperty("x-xihan-permission").GetString());

        using var apiKey = JsonDocument.Parse(OpenApiDocumentBuilder.Build(new OpenApiDocumentSource(registry, new HttpProtocolOptions(), null) { AuthenticationSchemes = ["ApiKey"] }));
        Assert.False(apiKey.RootElement.GetProperty("components").TryGetProperty("securitySchemes", out _));
        Assert.False(apiKey.RootElement.TryGetProperty("security", out _));
        Assert.False(apiKey.RootElement.GetProperty("paths").GetProperty("/orders/{id}").GetProperty("get").TryGetProperty("security", out _));
    }

    [Fact]
    public async Task OpenApi_LocalizesOperationTitlesAndDescriptions_PerAcceptLanguage_AndCachesPerLocale()
    {
        var (app, client) = await StartAsync(localization: true);
        await using var host = app;

        var (chinese, chineseResponse) = await DocumentOf(client, "zh-CN");
        using var ownedChinese = chinese;
        Assert.Equal(["zh-CN"], chineseResponse.Content.Headers.ContentLanguage);
        var get = chinese.RootElement.GetProperty("paths").GetProperty("/orders/{id}").GetProperty("get");
        Assert.Equal(("读取订单", "按 ID 读取一个订单。"), (get.GetProperty("summary").GetString(), get.GetProperty("description").GetString()));
        // 目录没有的操作没有标题与说明
        Assert.False(chinese.RootElement.GetProperty("paths").GetProperty("/orders").GetProperty("post").TryGetProperty("summary", out _));
        // 资源段带连字符的操作（order-filters.list）按 order_filters.list.title 取标题；只有英文时回退到 en-US；没有说明就不写，标题不顶替
        var filters = chinese.RootElement.GetProperty("paths").GetProperty("/order-filters").GetProperty("get");
        Assert.Equal("List order filters", filters.GetProperty("summary").GetString());
        Assert.False(filters.TryGetProperty("description", out _));

        // 没有 Accept-Language 走默认区域 en-US；日文是启用的区域（Content-Language 标 ja-JP），目录没有日文时文案回退到 en-US；各区域分别缓存，再次请求各自不变
        foreach (var acceptLanguage in new[] { null, "ja-JP", "zh-CN" })
        {
            var (document, response) = await DocumentOf(client, acceptLanguage);
            using var owned = document;
            var expected = acceptLanguage == "zh-CN" ? "读取订单" : "Get an order";
            Assert.Equal(expected, document.RootElement.GetProperty("paths").GetProperty("/orders/{id}").GetProperty("get").GetProperty("summary").GetString());
            Assert.Equal([acceptLanguage ?? "en-US"], response.Content.Headers.ContentLanguage);
        }
    }

    [Fact]
    public async Task DocumentSource_FromServices_ProducesTheSameDocument_AsTheMappedEndpoint()
    {
        var options = new OpenApiOptions { Title = "Orders API", Version = "2.0.0", Servers = [new OpenApiServer("https://api.example.com", "prod")] };
        var (app, client) = await StartAsync(localization: true, bearer: true, options: options);
        await using var host = app;
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(static source => source.Endpoints).ToList();

        // 导出工具用同一工厂组装输入：与 /openapi.json 在同一区域下逐字节相同（标题 / 版本 / servers、Bearer 方案、普通端点、文案都在内）
        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi.json");
        request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN");
        var served = await (await client.SendAsync(request)).Content.ReadAsByteArrayAsync();
        var built = OpenApiDocumentBuilder.Build(OpenApiDocumentSource.FromServices(app.Services, options, endpoints, "zh-CN"));
        Assert.Equal(served, built);
        using var chinese = JsonDocument.Parse(built);
        Assert.Equal("读取订单", chinese.RootElement.GetProperty("paths").GetProperty("/orders/{id}").GetProperty("get").GetProperty("summary").GetString());
        Assert.Equal("Orders API", chinese.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("https://api.example.com", chinese.RootElement.GetProperty("servers")[0].GetProperty("url").GetString());

        // 不给区域时按本地化的默认区域取文案；不给选项时标题取应用名、版本取框架版本
        using var english = JsonDocument.Parse(OpenApiDocumentBuilder.Build(OpenApiDocumentSource.FromServices(app.Services, null, endpoints)));
        Assert.Equal("Get an order", english.RootElement.GetProperty("paths").GetProperty("/orders/{id}").GetProperty("get").GetProperty("summary").GetString());
        Assert.Equal(FrameworkInfo.Version, english.RootElement.GetProperty("info").GetProperty("version").GetString());
        Assert.NotEqual("Orders API", english.RootElement.GetProperty("info").GetProperty("title").GetString());
        Assert.True(english.RootElement.GetProperty("components").TryGetProperty("securitySchemes", out _));
    }

    [Fact]
    public async Task OpenApi_IncludesPlainEndpoints_ByMetadata_WithBinaryFormTextAndSchemaReferences()
    {
        var (app, client) = await StartAsync(map: static web =>
        {
            // 表单上传：multipart 请求体、端点级权限、端点名 / 摘要 / 标签
            web.MapPost("/attachments/{orderId}", static (string orderId, IFormFile file) => Results.Created())
                .Accepts<IFormFile>("multipart/form-data")
                .Produces(201)
                .WithName("attachments.upload")
                .WithSummary("Upload an attachment")
                .WithTags("attachments")
                .RequireXiHanPermission("orders.write");
            // 二进制下载：没有类型但有内容类型
            web.MapGet("/attachments/{id}/content", static (string id) => Results.Bytes([]))
                .Produces(200, contentType: "application/octet-stream")
                .WithName("attachments.download");
            // JSON 响应按类型自动匹配到已登记操作的响应 Schema；JSON 请求体显式声明同 orders.create 的请求
            web.MapGet("/legacy/orders/{id}", static (string id) => new OrderReply("legacy", id, null, 0, [], null)).WithName("legacy.orders.get");
            web.MapPost("/legacy/orders", static () => Results.Created())
                .Accepts(typeof(LegacyOrder), "application/json")
                .WithXiHanOperationSchema(request: "orders.create")
                .WithName("legacy.orders.create");
            // 文本响应进文档；重定向、探针与排除的端点没有描述元数据或被排除，不进
            web.MapGet("/ping", static () => "pong").WithName("ping");
            web.MapGet("/redirect", static () => Results.Redirect("/ping"));
            web.MapGet("/hidden", static () => "x").ExcludeFromDescription();
            web.MapXiHanHealth();
        });
        await using var host = app;

        var (document, _) = await DocumentOf(client);
        using var owned = document;
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        var upload = paths.GetProperty("/attachments/{orderId}").GetProperty("post");
        Assert.Equal("attachments.upload", upload.GetProperty("operationId").GetString());
        Assert.Equal("Upload an attachment", upload.GetProperty("summary").GetString());
        Assert.Equal(["attachments"], upload.GetProperty("tags").EnumerateArray().Select(static t => t.GetString()));
        Assert.Equal("orders.write", upload.GetProperty("x-xihan-permission").GetString());
        Assert.Equal("orderId", upload.GetProperty("parameters")[0].GetProperty("name").GetString());
        var form = upload.GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
        Assert.Equal("binary", form.GetProperty("properties").GetProperty("file").GetProperty("format").GetString());
        Assert.True(upload.GetProperty("responses").TryGetProperty("201", out _));
        Assert.Equal("#/components/schemas/ProblemDetails", upload.GetProperty("responses").GetProperty("default").GetProperty("content").GetProperty("application/problem+json").GetProperty("schema").GetProperty("$ref").GetString());
        // 端点引用的权限码并入权限定义的收录
        Assert.True(root.GetProperty("components").GetProperty("x-xihan-permissions").TryGetProperty("orders.write", out _));

        var download = paths.GetProperty("/attachments/{id}/content").GetProperty("get");
        Assert.Equal("binary", download.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/octet-stream").GetProperty("schema").GetProperty("format").GetString());

        // JSON：自动匹配与显式声明都引用提升到组件的根；操作自己也改为引用同一份
        var legacyGet = paths.GetProperty("/legacy/orders/{id}").GetProperty("get");
        Assert.Equal("#/components/schemas/OrderReply", legacyGet.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        var legacyCreate = paths.GetProperty("/legacy/orders").GetProperty("post");
        Assert.Equal("#/components/schemas/OrderRequest", legacyCreate.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Equal("#/components/schemas/Line", schemas.GetProperty("OrderRequest").GetProperty("properties").GetProperty("line").GetProperty("$ref").GetString());
        Assert.False(schemas.GetProperty("OrderRequest").TryGetProperty("$defs", out _));
        Assert.Equal("operation", schemas.GetProperty("OrderReply").GetProperty("required")[0].GetString());
        var create = paths.GetProperty("/orders").GetProperty("post");
        Assert.Equal("#/components/schemas/OrderRequest", create.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        Assert.Equal("#/components/schemas/OrderReply", create.GetProperty("responses").GetProperty("201").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
        // 被提升的根仍给查询参数用：GET /orders/{id} 的 page 还是查询参数
        Assert.Contains(paths.GetProperty("/orders/{id}").GetProperty("get").GetProperty("parameters").EnumerateArray(), static p => p.GetProperty("name").GetString() == "page" && p.GetProperty("in").GetString() == "query");

        Assert.Equal("string", paths.GetProperty("/ping").GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("text/plain").GetProperty("schema").GetProperty("type").GetString());
        Assert.False(paths.TryGetProperty("/redirect", out _));
        Assert.False(paths.TryGetProperty("/hidden", out _));
        Assert.False(paths.TryGetProperty("/health/ready", out _));
        Assert.False(paths.TryGetProperty("/openapi.json", out _));
    }

    [Fact]
    public void Build_RejectsPlainEndpointJsonTypes_ThatReferenceNoOperation_AndRoutesSharedWithOperations()
    {
        var registry = new OperationRegistry();
        registry.Add(Describe(new OperationContract("orders.get", expose: Expose.Http))).ThrowIfFailure();
        var options = new HttpProtocolOptions();

        // 类型不是任何已登记操作的响应类型
        var unknown = Endpoint("GET", "/legacy/unknown", new ProducesResponseTypeMetadata(200, typeof(LegacyOrder), ["application/json"]));
        var unresolved = Assert.Throws<FrameworkException>(() => OpenApiDocumentBuilder.Build(new OpenApiDocumentSource(registry, options, null) { Endpoints = [unknown] }));
        Assert.Equal(HttpErrors.OpenApiSchemaUnresolved, unresolved.Code);
        Assert.Contains("LegacyOrder", unresolved.Message, StringComparison.Ordinal);

        // 显式声明的操作没登记
        var declared = Endpoint("GET", "/legacy/declared", new ProducesResponseTypeMetadata(200, typeof(LegacyOrder), ["application/json"]), new OperationSchemaReference(ResponseOperationId: "orders.missing"));
        Assert.Equal(HttpErrors.OpenApiSchemaUnresolved, Assert.Throws<FrameworkException>(() => OpenApiDocumentBuilder.Build(new OpenApiDocumentSource(registry, options, null) { Endpoints = [declared] })).Code);

        // JSON 响应没有类型也没有声明
        var untyped = Endpoint("GET", "/legacy/untyped", new ProducesResponseTypeMetadata(200, null, ["application/json"]));
        Assert.Equal(HttpErrors.OpenApiSchemaUnresolved, Assert.Throws<FrameworkException>(() => OpenApiDocumentBuilder.Build(new OpenApiDocumentSource(registry, options, null) { Endpoints = [untyped] })).Code);

        // 与操作共用一条路由
        var shared = Endpoint("GET", "/orders/{id}", new ProducesResponseTypeMetadata(200, typeof(string), ["text/plain"]));
        Assert.Equal(HttpErrors.DuplicateRoute, Assert.Throws<FrameworkException>(() => OpenApiDocumentBuilder.Build(new OpenApiDocumentSource(registry, options, null) { Endpoints = [shared] })).Code);

        // 没有方法元数据、没有描述元数据、带操作描述的端点都不进文档
        var ignored = new RouteEndpoint(static _ => Task.CompletedTask, RoutePatternFactory.Parse("/plain"), 0, new EndpointMetadataCollection(new HttpMethodMetadata(["GET"])), "GET /plain");
        using var document = JsonDocument.Parse(OpenApiDocumentBuilder.Build(new OpenApiDocumentSource(registry, options, null) { Endpoints = [ignored] }));
        Assert.Equal(["/orders/{id}"], document.RootElement.GetProperty("paths").EnumerateObject().Select(static p => p.Name));
    }
}
