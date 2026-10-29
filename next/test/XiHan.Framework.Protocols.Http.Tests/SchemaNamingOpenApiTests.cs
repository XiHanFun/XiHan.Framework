// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Protocols.Http.OpenApi;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Http.Tests.Fixtures;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 生成器按实参给封闭泛型起名、把字符串枚举放进 <c>$defs</c> 之后。
/// </summary>
/// <remarks>
/// OpenAPI 的组件提升不再串型。两个 <c>PageResult&lt;T&gt;</c> 各是一份组件，枚举是独立组件并被属性与查询参数以 <c>$ref</c> 引用。
/// </remarks>
public sealed class SchemaNamingOpenApiTests
{
    [Fact]
    public async Task OpenApi_KeepsClosedGenericSchemasApart_AndPublishesStringEnumsAsComponents()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<PagedFeature>());

        await using var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanOperations();
        // 两个普通端点分别引用两个操作的响应根：根按 title 提升到组件，同一泛型的两个封闭类型各是一份组件
        app.MapGet("/export/users", static () => Results.Ok()).Produces<OrderReply>(200, "application/json").WithXiHanOperationSchema(response: "users.list");
        app.MapGet("/export/roles", static () => Results.Ok()).Produces<OrderReply>(200, "application/json").WithXiHanOperationSchema(response: "roles.list");
        app.MapXiHanOpenApi();
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi.json"));
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");

        // 没有裸的 PageResult；两份提升的根各自引用自己的实参类型，普通端点与操作都指向它们
        Assert.False(schemas.TryGetProperty("PageResult", out _));
        Assert.Equal("#/components/schemas/UserResponse", schemas.GetProperty("PageResultOfUserResponse").GetProperty("properties").GetProperty("items").GetProperty("items").GetProperty("$ref").GetString());
        Assert.Equal("#/components/schemas/RoleResponse", schemas.GetProperty("PageResultOfRoleResponse").GetProperty("properties").GetProperty("items").GetProperty("items").GetProperty("$ref").GetString());
        Assert.Equal("#/components/schemas/PageResultOfUserResponse", SuccessSchema(root.GetProperty("paths").GetProperty("/export/users").GetProperty("get")).GetProperty("$ref").GetString());
        Assert.Equal("#/components/schemas/PageResultOfRoleResponse", SuccessSchema(root.GetProperty("paths").GetProperty("/export/roles").GetProperty("get")).GetProperty("$ref").GetString());

        // 枚举是独立组件，属性以 $ref 引用，可空为 anyOf 带 null
        Assert.Equal(["Active", "Disabled"], schemas.GetProperty("UserStatus").GetProperty("enum").EnumerateArray().Select(static e => e.GetString()));
        Assert.Equal("string", schemas.GetProperty("UserStatus").GetProperty("type").GetString());
        var user = schemas.GetProperty("UserResponse").GetProperty("properties");
        Assert.Equal("#/components/schemas/UserStatus", user.GetProperty("status").GetProperty("$ref").GetString());
        Assert.Equal("#/components/schemas/UserStatus", user.GetProperty("previousStatus").GetProperty("anyOf")[0].GetProperty("$ref").GetString());
        Assert.Equal("null", user.GetProperty("previousStatus").GetProperty("anyOf")[1].GetProperty("type").GetString());

        // GET /users 与 GET /roles：响应引用提升后的根，查询参数 status 引用枚举组件
        var users = root.GetProperty("paths").GetProperty("/users").GetProperty("get");
        var roles = root.GetProperty("paths").GetProperty("/roles").GetProperty("get");
        Assert.Equal("#/components/schemas/PageResultOfUserResponse", SuccessSchema(users).GetProperty("$ref").GetString());
        Assert.Equal("#/components/schemas/PageResultOfRoleResponse", SuccessSchema(roles).GetProperty("$ref").GetString());
        var status = Assert.Single(users.GetProperty("parameters").EnumerateArray(), static p => p.GetProperty("name").GetString() == "status");
        Assert.Equal("query", status.GetProperty("in").GetString());
        Assert.Equal("#/components/schemas/UserStatus", status.GetProperty("schema").GetProperty("anyOf")[0].GetProperty("$ref").GetString());
        Assert.False(status.GetProperty("required").GetBoolean());

        await app.StopAsync();
    }

    /// <summary>
    /// 操作或端点成功响应的 JSON Schema。
    /// </summary>
    private static JsonElement SuccessSchema(JsonElement operation)
    {
        var success = Assert.Single(operation.GetProperty("responses").EnumerateObject(), static r => r.Name != "default").Value;
        return success.GetProperty("content").GetProperty("application/json").GetProperty("schema");
    }
}
