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
/// 按 <c>samples/SimpleApp/Program.cs</c> 的方式装配宿主（<c>AddXiHan</c> → <c>UseXiHan</c> → <c>MapXiHanOperations</c> → <c>MapXiHanOpenApi</c>），
/// 验证 <c>/openapi.json</c> 把契约的权限码与特性登记的权限定义一起发布：前端据此隐藏入口并生成授权界面。
/// </summary>
public sealed class SimpleAppOpenApiTests
{
    [Fact]
    public async Task OpenApi_PublishesOperationPermissions_AndRegisteredDefinitions()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<SampleOrdersFeature>());

        await using var app = builder.Build();
        app.UseXiHan();
        app.MapXiHanOperations();
        app.MapXiHanOpenApi();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var response = await client.GetAsync("/openapi.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // orders.create → POST /orders，契约的权限码进操作的扩展字段；Experimental 仍带稳定性扩展
        var post = root.GetProperty("paths").GetProperty("/orders").GetProperty("post");
        Assert.Equal("orders.create", post.GetProperty("operationId").GetString());
        Assert.Equal("orders.write", post.GetProperty("x-xihan-permission").GetString());
        Assert.Equal("Experimental", post.GetProperty("x-xihan-stability").GetString());
        Assert.False(post.TryGetProperty("x-xihan-policy", out _));
        Assert.Equal("orders.read", root.GetProperty("paths").GetProperty("/orders").GetProperty("get").GetProperty("x-xihan-permission").GetString());

        // 特性登记的权限定义经容器里的 PermissionRegistry 进 components；示例的定义没有说明，所以没有 description
        var permissions = root.GetProperty("components").GetProperty("x-xihan-permissions");
        var write = permissions.GetProperty("orders.write");
        Assert.Equal("orders.write", write.GetProperty("code").GetString());
        Assert.Equal(SampleOrdersFeature.WriteDisplayName, write.GetProperty("displayName").GetString());
        Assert.False(write.TryGetProperty("description", out _));
        Assert.Equal(SampleOrdersFeature.ReadDisplayName, permissions.GetProperty("orders.read").GetProperty("displayName").GetString());
        Assert.Equal(["orders.read", "orders.write"], permissions.EnumerateObject().Select(static p => p.Name));

        await app.StopAsync();
    }
}
