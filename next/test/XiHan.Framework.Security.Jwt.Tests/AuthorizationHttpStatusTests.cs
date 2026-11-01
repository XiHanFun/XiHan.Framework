// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Jwt.Tests;

/// <summary>
/// 经 ASP.NET Core 宿主的权限判定状态码。
/// </summary>
/// <remarks>
/// 授予存储不可达是 503 + <c>SECURITY_UNAVAILABLE_001</c>，不是 403：读不到授予不能当成没有授予（ADR-0071）；存储可达而缺权限是 403，持有则放行。
/// </remarks>
public sealed class AuthorizationHttpStatusTests
{
    [Fact]
    public async Task GrantStoreFailure_IsServiceUnavailable_NotForbidden()
    {
        var store = new SwitchableGrantStore { Unavailable = true };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
        [
            new("XiHan:Security:Jwt:Secret", JwtTokens.Secret),
            new("XiHan:Security:Jwt:Issuer", JwtTokens.Issuer),
            new("XiHan:Security:Jwt:Audience", JwtTokens.Audience),
        ]);
        // 先于特性登记：内存授权用 TryAdd 登记授予存储，这里的实例胜出
        builder.Services.AddSingleton<IPermissionGrantStore>(store);
        builder.AddXiHan(static xihan => xihan.UseFeature<InMemorySecurityFeature>().UseFeature<JwtSecurityFeature>().UseFeature(new StubFeature(TestManifests.Create("xihan.app.orders", requires: ["security.authorization@1"]))
        {
            OnConfigure = static ctx => ctx.AddPermission(new PermissionDefinition(SwitchableGrantStore.Permission, "Read orders")),
        }));

        await using var app = builder.Build();
        app.UseXiHan();
        app.MapGet("/orders", static () => "ok").RequireXiHanPermission(SwitchableGrantStore.Permission);
        await app.StartAsync();
        using var client = app.GetTestClient();

        // 存储不可达：503，错误码是存储不可达，不是缺权限
        using (var unavailable = await SendAsync(client))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
            Assert.Equal("SECURITY_UNAVAILABLE_001", await CodeOf(unavailable));
        }

        // 存储恢复但没有授予：403 缺权限
        store.Unavailable = false;
        using (var forbidden = await SendAsync(client))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.Equal("SECURITY_DENIED_004", await CodeOf(forbidden));
        }

        // 有授予：放行
        store.Granted.Add("alice");
        using (var ok = await SendAsync(client))
        {
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        await app.StopAsync();
    }

    /// <summary>
    /// 带 alice 的有效令牌请求 <c>/orders</c>。
    /// </summary>
    private static Task<HttpResponseMessage> SendAsync(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtTokens.Create());
        return client.SendAsync(request);
    }

    /// <summary>
    /// Problem Details 的错误码。
    /// </summary>
    private static async Task<string?> CodeOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
