// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Http.Tests.Fixtures;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// OpenAPI 文档的文案、标签、分组与字段说明（ADR-0052）。
/// </summary>
/// <remarks>
/// 操作的标题与说明分开取，授权要求按区域附在说明末尾；顶层标签带显示名；分组出各自的文档与全量文档的 <c>x-tagGroups</c>；
/// Schema 里 XML 注释折进的说明进参数、请求体与响应，可按区域替换。
/// </remarks>
public sealed class OpenApiDocumentationTests
{
    /// <summary>
    /// 带说明的请求与响应 Schema（源生成器从 XML 注释折进的形态）。
    /// </summary>
    private static readonly OperationSchema _schema = new(
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"OrderRequest","description":"订单查询。","type":"object","properties":{"id":{"description":"订单 ID。","type":["string","null"]},"name":{"description":"名称。","type":["string","null"]},"page":{"type":"integer","format":"int32"},"line":{"description":"行。","$ref":"#/$defs/OrderLine"}},"required":["page"],"$defs":{"OrderLine":{"description":"订单行。","type":"object","properties":{"skuCode":{"description":"货号。","type":"string"}},"required":["skuCode"]}}}""",
        """{"$schema":"https://json-schema.org/draft/2020-12/schema","title":"OrderReply","description":"订单结果。","type":"object","properties":{"operation":{"type":"string"}},"required":["operation"]}""");

    /// <summary>
    /// 文档区域（简体中文）的目录：框架的统一文案、操作、标签、分组与 Schema 覆盖。
    /// </summary>
    private static readonly Dictionary<string, string> _catalog = new(StringComparer.Ordinal)
    {
        ["orders.get.title"] = "读取订单",
        ["orders.get.description"] = "按 ID 读取订单。",
        [HttpMessages.OpenApiPermissionRequired] = "需要权限 {permission}（{name}）。",
        [HttpMessages.OpenApiPermissionCodeRequired] = "需要权限 {permission}。",
        [HttpMessages.OpenApiPolicyRequired] = "需要策略 {policy}。",
        [HttpMessages.OpenApiAuthenticationRequired] = "需要登录。",
        [HttpMessages.OpenApiSuccess] = "成功",
        [HttpMessages.OpenApiError] = "错误",
        [HttpMessages.OpenApiUngrouped] = "其他",
        ["openapi.tags.orders.name"] = "订单",
        ["openapi.tags.orders.description"] = "订单的增查。",
        ["openapi.groups.orders.name"] = "订单服务",
        ["openapi.groups.billing.name"] = "账单服务",
        ["openapi.groups.billing.description"] = "开票与导出。",
        ["openapi.schemas.order_request.properties.id"] = "订单编号。",
        ["openapi.schemas.order_line.properties.sku_code"] = "商品货号。",
        ["openapi.schemas.order_reply.description"] = "订单处理结果。",
    };

    /// <summary>
    /// 分组：账单在前（排序 5，额外带 exports 标签让普通端点入组），订单在后；notes 不属于任何组。
    /// </summary>
    private static readonly OpenApiGroup[] _groups = [new("orders", 10, ["xihan.test.orders"]), new("billing", 5, ["xihan.test.billing"], ["exports"])];

    [Fact]
    public void Operations_SplitTitleAndDescription_AndAppendAuthorizationPerLocale()
    {
        using var document = Build();
        var paths = document.RootElement.GetProperty("paths");

        // 标题与说明分开；权限要求带显示名附在说明末尾（空一行）
        var get = paths.GetProperty("/orders/{id}").GetProperty("get");
        Assert.Equal(("读取订单", "按 ID 读取订单。\n\n需要权限 orders.read（读取订单）。"), (get.GetProperty("summary").GetString(), get.GetProperty("description").GetString()));

        // 没有说明时授权要求这一行就是说明；没登记的权限只有码；只要求登录与命名策略各有一句；没有要求就没有说明
        Assert.Equal("需要权限 orders.write。", paths.GetProperty("/orders").GetProperty("post").GetProperty("description").GetString());
        Assert.False(paths.GetProperty("/orders").GetProperty("post").TryGetProperty("summary", out _));
        Assert.Equal("需要登录。", paths.GetProperty("/orders").GetProperty("get").GetProperty("description").GetString());
        Assert.Equal("需要策略 billing.admins。", paths.GetProperty("/invoices").GetProperty("get").GetProperty("description").GetString());
        Assert.False(paths.GetProperty("/notes").GetProperty("get").TryGetProperty("description", out _));
        // 扩展字段照旧
        Assert.Equal("orders.read", get.GetProperty("x-xihan-permission").GetString());

        // 没有本地化：没有任何目录文案，响应说明是英文的 Success / Error
        using var plain = JsonDocument.Parse(OpenApiDocumentBuilder.Build(Source() with { Localize = null }));
        var bare = plain.RootElement.GetProperty("paths").GetProperty("/orders/{id}").GetProperty("get");
        Assert.False(bare.TryGetProperty("summary", out _));
        Assert.False(bare.TryGetProperty("description", out _));
        Assert.Equal("Error", bare.GetProperty("responses").GetProperty("default").GetProperty("description").GetString());
    }

    [Fact]
    public void Tags_CarryDisplayNames_AndTheFullDocumentGroupsThem()
    {
        using var document = Build();
        var root = document.RootElement;

        // 顶层 tags：用到的每个标签按名字排序，name 是资源码，显示名与说明有才写
        var tags = root.GetProperty("tags").EnumerateArray().ToList();
        Assert.Equal(["exports", "invoices", "notes", "orders"], tags.Select(static t => t.GetProperty("name").GetString()));
        var orders = tags[3];
        Assert.Equal(("订单", "订单的增查。"), (orders.GetProperty("x-displayName").GetString(), orders.GetProperty("description").GetString()));
        Assert.False(tags[1].TryGetProperty("x-displayName", out _));

        // x-tagGroups：按排序（账单 5 在订单 10 前），组名是显示名；没进任何组的标签归入最后的「其他」
        var groups = root.GetProperty("x-tagGroups").EnumerateArray().Select(static g => (g.GetProperty("name").GetString(), string.Join(",", g.GetProperty("tags").EnumerateArray().Select(static t => t.GetString())))).ToList();
        Assert.Equal([("账单服务", "exports,invoices"), ("订单服务", "orders"), ("其他", "notes")], groups);

        // 全量文档收录全部登记的权限
        Assert.True(root.GetProperty("components").GetProperty("x-xihan-permissions").TryGetProperty("billing.view", out _));
    }

    [Fact]
    public void GroupDocuments_HoldOnlyTheirOperationsEndpointsAndPermissions()
    {
        using var orders = Build("orders");
        var root = orders.RootElement;
        Assert.Equal("订单服务", root.GetProperty("info").GetProperty("title").GetString());
        Assert.False(root.GetProperty("info").TryGetProperty("description", out _));
        Assert.Equal(["/orders", "/orders/{id}"], root.GetProperty("paths").EnumerateObject().Select(static p => p.Name));
        Assert.Equal(["orders"], root.GetProperty("tags").EnumerateArray().Select(static t => t.GetProperty("name").GetString()));
        Assert.False(root.TryGetProperty("x-tagGroups", out _));
        // 分组文档只收本组引用的权限码（显示名照旧）
        var permissions = root.GetProperty("components").GetProperty("x-xihan-permissions");
        Assert.Equal(["orders.read", "orders.write"], permissions.EnumerateObject().Select(static p => p.Name));
        Assert.Equal("读取订单", permissions.GetProperty("orders.read").GetProperty("displayName").GetString());

        // 账单组：自己的操作加按额外标签入组的普通端点，组说明进 info
        using var billing = Build("billing");
        Assert.Equal(["/exports/{id}", "/invoices"], billing.RootElement.GetProperty("paths").EnumerateObject().Select(static p => p.Name));
        Assert.Equal(("账单服务", "开票与导出。"), (billing.RootElement.GetProperty("info").GetProperty("title").GetString(), billing.RootElement.GetProperty("info").GetProperty("description").GetString()));
        Assert.Empty(billing.RootElement.GetProperty("components").GetProperty("x-xihan-permissions").EnumerateObject());

        Assert.Throws<ArgumentException>(() => OpenApiDocumentBuilder.Build(Source() with { Group = "missing" }));
    }

    [Fact]
    public void FieldDescriptions_ReachParametersBodiesAndResponses_AndArePerLocale()
    {
        using var document = Build();
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        // 参数：覆盖优先（id），否则属性 Schema 的说明（name），没有说明不写（page）；参数 Schema 里的说明随之替换
        var parameters = paths.GetProperty("/orders/{id}").GetProperty("get").GetProperty("parameters").EnumerateArray().ToDictionary(static p => p.GetProperty("name").GetString()!);
        Assert.Equal("订单编号。", parameters["id"].GetProperty("description").GetString());
        Assert.Equal("订单编号。", parameters["id"].GetProperty("schema").GetProperty("description").GetString());
        Assert.Equal("名称。", parameters["name"].GetProperty("description").GetString());
        Assert.False(parameters["page"].TryGetProperty("description", out _));

        // 请求体：根说明；内联 Schema 的属性说明按区域替换
        var post = paths.GetProperty("/orders").GetProperty("post");
        Assert.Equal("订单查询。", post.GetProperty("requestBody").GetProperty("description").GetString());
        Assert.Equal("订单编号。", post.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("properties").GetProperty("id").GetProperty("description").GetString());

        // 响应：成功取响应根说明（有覆盖用覆盖），失败是按区域的「错误」
        var responses = post.GetProperty("responses");
        Assert.Equal("订单处理结果。", responses.GetProperty("201").GetProperty("description").GetString());
        Assert.Equal("错误", responses.GetProperty("default").GetProperty("description").GetString());

        // 组件：$defs 提升出的组件属性说明按区域替换（键按 snake_case）
        Assert.Equal("商品货号。", root.GetProperty("components").GetProperty("schemas").GetProperty("OrderLine").GetProperty("properties").GetProperty("skuCode").GetProperty("description").GetString());
        Assert.Equal("订单行。", root.GetProperty("components").GetProperty("schemas").GetProperty("OrderLine").GetProperty("description").GetString());
    }

    [Fact]
    public async Task MappedGroupDocuments_AreServed_AndListedForTheDocumentationUi()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<OrdersFeature>());
        await using var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanOperations();
        var options = new OpenApiOptions { Title = "Orders API", Groups = [new OpenApiGroup("orders", 1, ["xihan.test.orders"])] };
        app.MapXiHanOpenApi(options);
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/openapi/orders.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var group = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("orders", group.RootElement.GetProperty("info").GetProperty("title").GetString());
        using var full = JsonDocument.Parse(await client.GetStringAsync("/openapi.json"));
        Assert.Equal("orders", full.RootElement.GetProperty("x-tagGroups")[0].GetProperty("name").GetString());

        // 列给文档界面：全量在前，组随后；没有本地化时组标题是组名
        Assert.Equal(
            [new OpenApiDocumentLink("all", "Orders API", "/openapi.json"), new OpenApiDocumentLink("orders", "orders", "/openapi/orders.json")],
            ((IEndpointRouteBuilder)app).GetXiHanOpenApiDocuments(options));
        Assert.Equal("/docs/api/orders.json", OpenApiEndpointRouteBuilderExtensions.GroupPattern("/docs/api.json", "orders"));
        await app.StopAsync();
    }

    [Fact]
    public void Groups_AreValidated_AndKeysUseSnakeCase()
    {
        Assert.Throws<ArgumentException>(static () => new OpenApiGroup("Orders", 1, []));
        Assert.Throws<ArgumentException>(static () => new OpenApiOptions { Groups = [new OpenApiGroup("all", 1, [])] });
        Assert.Throws<ArgumentException>(static () => new OpenApiOptions { Groups = [new OpenApiGroup("a", 1, []), new OpenApiGroup("a", 2, [])] });
        Assert.Throws<ArgumentException>(static () => new OpenApiOptions { Groups = [new OpenApiGroup("a", 1, ["x.f"]), new OpenApiGroup("b", 2, ["x.f"])] });

        Assert.Equal("constraint_rule_dto", OpenApiDocumentBuilder.SnakeCase("ConstraintRuleDto"));
        Assert.Equal("page_result_of_user_response", OpenApiDocumentBuilder.SnakeCase("PageResultOfUserResponse"));
        Assert.Equal("ip_address", OpenApiDocumentBuilder.SnakeCase("IPAddress"));
        Assert.Equal("rule_type", OpenApiDocumentBuilder.SnakeCase("ruleType"));
        Assert.Equal("v2_name", OpenApiDocumentBuilder.SnakeCase("v2Name"));
    }

    /// <summary>
    /// 生成文档（全量或某一组）。
    /// </summary>
    private static JsonDocument Build(string? group = null) => JsonDocument.Parse(OpenApiDocumentBuilder.Build(Source() with { Group = group }));

    /// <summary>
    /// 文档输入：五个操作分属三个特性、一个带 exports 标签的普通端点、权限注册表与简体中文目录。
    /// </summary>
    private static OpenApiDocumentSource Source()
    {
        var registry = new OperationRegistry();
        registry.Add(Describe(new OperationContract("orders.get", expose: Expose.Http, permission: "orders.read"), "xihan.test.orders")).ThrowIfFailure();
        registry.Add(Describe(new OperationContract("orders.create", expose: Expose.Http, permission: "orders.write"), "xihan.test.orders")).ThrowIfFailure();
        registry.Add(Describe(new OperationContract("orders.list", expose: Expose.Http, policy: "authenticated"), "xihan.test.orders")).ThrowIfFailure();
        registry.Add(Describe(new OperationContract("invoices.list", expose: Expose.Http, policy: "billing.admins"), "xihan.test.billing")).ThrowIfFailure();
        registry.Add(Describe(new OperationContract("notes.list", expose: Expose.Http), "xihan.test.misc")).ThrowIfFailure();
        var permissions = new PermissionRegistry();
        permissions.Add(new PermissionDefinition("orders.read", "读取订单")).ThrowIfFailure();
        permissions.Add(new PermissionDefinition("billing.view", "查看账单")).ThrowIfFailure();
        var export = new RouteEndpoint(
            static _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/exports/{id}"),
            0,
            new EndpointMetadataCollection(new HttpMethodMetadata(["GET"]), new TagsAttribute("exports"), new ProducesResponseTypeMetadata(200, null, ["text/csv"])),
            "GET /exports/{id}");
        return new OpenApiDocumentSource(registry, new HttpProtocolOptions(), permissions)
        {
            Endpoints = [export],
            Groups = _groups,
            Localize = static (key, arguments) => _catalog.TryGetValue(key, out var template) ? MessageTemplate.Format(template, arguments, CultureInfo.InvariantCulture) : null,
        };
    }

    /// <summary>
    /// 以带说明的 Schema 描述一个契约。
    /// </summary>
    private static OperationDescriptor<OrdersOperation, OrderRequest, OrderReply> Describe(OperationContract contract, string owner)
        => new(contract, owner, OrdersJsonContext.Default.OrderRequest, OrdersJsonContext.Default.OrderReply, _schema);
}
