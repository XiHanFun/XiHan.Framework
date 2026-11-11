// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Protocols.Http.Tests.Fixtures;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Protocols.Http.Tests;

/// <summary>
/// 端点上的授权约定与操作契约的权限叠加判定
/// </summary>
/// <remarks>
/// 安全审查 S-01（ADR-0174）：在 <c>MapXiHanOperations()</c> 的组上加 <c>RequireXiHanAuthentication()</c> 原先替换了每个操作的权限，没有授予的已登录用户能调用全部操作；现在两者都要满足
/// </remarks>
public sealed class EndpointRequirementStackingTests
{
    /// <summary>
    /// 持有 <c>orders.write</c> 的主体的 API Key
    /// </summary>
    private const string WriterKey = "writer-key-0123456789";

    /// <summary>
    /// 没有任何授予的主体的 API Key
    /// </summary>
    private const string NobodyKey = "nobody-key-0123456789";

    [Fact]
    public async Task GroupConvention_AddsToTheContractPermission_InsteadOfReplacingIt()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
        [
            new("XiHan:Security:InMemory:Principals:writer:ApiKey", WriterKey),
            new("XiHan:Security:InMemory:Principals:nobody:ApiKey", NobodyKey),
            new("XiHan:Security:InMemory:Grants:user:writer", "orders.write"),
        ]);
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<InMemorySecurityFeature>().UseFeature<SampleOrdersFeature>());
        await using var app = builder.Build();
        app.UseXiHan();
        // 宿主本意是「整个 API 都要登录」：这条约定必须叠加在 orders.create 的 orders.write 之上，不能替换它
        app.MapXiHanOperations().RequireXiHanAuthentication();
        await app.StartAsync();
        using var client = app.GetTestClient();

        using (var anonymous = await CreateAsync(client, null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        }

        using (var nobody = await CreateAsync(client, NobodyKey))
        {
            Assert.Equal(HttpStatusCode.Forbidden, nobody.StatusCode);
            Assert.Equal("SECURITY_DENIED_004", await CodeOf(nobody));
        }

        using (var writer = await CreateAsync(client, WriterKey))
        {
            Assert.True(writer.IsSuccessStatusCode, $"Expected success, got {(int)writer.StatusCode}.");
        }

        await app.StopAsync();
    }

    [Fact]
    public async Task SeveralEndpointRequirements_AreAllEvaluated()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
        [
            new("XiHan:Security:InMemory:Principals:writer:ApiKey", WriterKey),
            new("XiHan:Security:InMemory:Grants:user:writer", "orders.write"),
        ]);
        builder.AddXiHan(static xihan => xihan.UseFeature<HttpProtocolFeature>().UseFeature<InMemorySecurityFeature>().UseFeature<SampleOrdersFeature>());
        await using var app = builder.Build();
        app.UseXiHan();
        // 组上要 orders.read，端点上又要登录：后加的那条原先会顶掉前一条
        var group = app.MapGroup("/api").RequireXiHanPermission("orders.read");
        group.MapGet("/ping", static () => "pong").RequireXiHanAuthentication();
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/ping");
        request.Headers.Authorization = new AuthenticationHeaderValue(ApiKeyAuthenticationHandler.SchemeName, WriterKey);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("SECURITY_DENIED_004", await CodeOf(response));
        await app.StopAsync();
    }

    /// <summary>
    /// 以给定 API Key（空为匿名）调用 <c>orders.create</c>
    /// </summary>
    private static async Task<HttpResponseMessage> CreateAsync(HttpClient client, string? apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders")
        {
            Content = new StringContent("""{"name":"x"}""", Encoding.UTF8, "application/json"),
        };
        if (apiKey is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(ApiKeyAuthenticationHandler.SchemeName, apiKey);
        }

        return await client.SendAsync(request);
    }

    /// <summary>
    /// Problem Details 里的错误码
    /// </summary>
    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
