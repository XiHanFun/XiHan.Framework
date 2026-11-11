// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 端点上的授权要求不会被静默跳过
/// </summary>
/// <remarks>
/// 安全审查 S-06（ADR-0175）：绕过内核管道的端点上的要求原先从不判定，启动核对却照样通过；<c>UseXiHan()</c> 没调或排在路由之前时，端点上的要求同样不判定而直接放行。现在前者启动失败，后者请求时拒绝
/// </remarks>
public sealed class UnevaluatedRequirementTests
{
    /// <summary>
    /// 装配内存安全与一个登记 <c>orders.read</c> 的特性
    /// </summary>
    private static WebApplication Build()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddXiHan(static xihan => xihan.UseFeature<InMemorySecurityFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.orders", requires: ["security.authorization@1"]))
        {
            OnConfigure = static ctx => ctx.AddPermission(new PermissionDefinition("orders.read", "Read orders")),
        }));
        return builder.Build();
    }

    [Fact]
    public async Task RequirementOnABypassEndpoint_FailsTheHostStart()
    {
        await using var app = Build();
        app.UseXiHan();
        PipelineBypassMetadata.BypassXiHanPipeline(app.MapGet("/assets/report", static () => "secret")).RequireXiHanPermission("orders.read");
        app.MapGet("/orders", static () => "ok").RequireXiHanPermission("orders.read");

        var exception = await Assert.ThrowsAsync<FrameworkException>(() => app.StartAsync());

        Assert.Equal(SecurityErrors.RequirementBypassed, exception.Code);
        Assert.Contains("/assets/report", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("'/orders'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutUseXiHan_AnEndpointRequirement_FailsClosed()
    {
        await using var app = Build();
        app.MapGet("/orders", static () => "secret").RequireXiHanPermission("orders.read");
        await app.StartAsync();
        using var client = app.GetTestClient();

        await AssertRejectedAsync(client, "/orders");
        await app.StopAsync();
    }

    [Fact]
    public async Task UseXiHanBeforeRouting_AnEndpointRequirement_FailsClosed()
    {
        await using var app = Build();
        app.UseXiHan();
        app.UseRouting();
        app.MapGet("/orders", static () => "secret").RequireXiHanPermission("orders.read");
        await app.StartAsync();
        using var client = app.GetTestClient();

        await AssertRejectedAsync(client, "/orders");
        await app.StopAsync();
    }

    [Fact]
    public async Task EndpointsWithoutRequirements_AreUnaffected()
    {
        await using var app = Build();
        app.MapGet("/public", static () => "ok");
        await app.StartAsync();
        using var client = app.GetTestClient();

        Assert.Equal("ok", await client.GetStringAsync("/public"));
        await app.StopAsync();
    }

    /// <summary>
    /// 断言请求拿不到端点的结果
    /// </summary>
    /// <remarks>
    /// 要么服务端抛出（测试服务器把异常带给客户端），要么返回 500
    /// </remarks>
    private static async Task AssertRejectedAsync(HttpClient client, string path)
    {
        try
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        catch (InvalidOperationException exception)
        {
            Assert.Contains("UseXiHan", exception.Message, StringComparison.Ordinal);
        }
    }
}
