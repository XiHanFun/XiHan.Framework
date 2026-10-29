// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Security.Jwt.Tests;

/// <summary>
/// 经 ASP.NET Core 宿主的 JWT 认证失败。
/// </summary>
/// <remarks>
/// 过期、签名不符的访问令牌是 401 并质询 <c>Bearer error="invalid_token"</c>（RFC 6750 §3.1），没有令牌是 401 并质询 <c>Bearer</c>（RFC 6750 §3，不带错误码）；有效令牌放行。客户端与网关据 401 触发刷新。
/// </remarks>
public sealed class JwtHttpChallengeTests
{
    [Fact]
    public async Task InvalidAndMissingTokens_AreUnauthorized_WithBearerChallenges()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
        [
            new("XiHan:Security:Jwt:Secret", JwtTokens.Secret),
            new("XiHan:Security:Jwt:Issuer", JwtTokens.Issuer),
            new("XiHan:Security:Jwt:Audience", JwtTokens.Audience),
        ]);
        builder.AddXiHan(static xihan => xihan.UseFeature<InMemorySecurityFeature>().UseFeature<JwtSecurityFeature>());

        await using var app = builder.Build();
        app.UseXiHan();
        app.MapGet("/me", static () => "ok").RequireXiHanAuthentication();
        await app.StartAsync();
        using var client = app.GetTestClient();

        // 过期与签名不符：凭据无效，401 + invalid_token，正文仍是 SECURITY_DENIED_003
        foreach (var token in new[] { JwtTokens.Create(lifetime: TimeSpan.FromMinutes(-1)), JwtTokens.Create(secret: "another-secret-key-with-at-least-32-bytes!!") })
        {
            using var response = await SendAsync(client, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(["Bearer error=\"invalid_token\""], response.Headers.WwwAuthenticate.Select(static h => h.ToString()));
            Assert.Equal("SECURITY_DENIED_003", await CodeOf(response));
        }

        // 没有令牌：未认证，401 + 不带错误码的 Bearer 质询
        using (var anonymous = await SendAsync(client, token: null))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.Equal(["Bearer"], anonymous.Headers.WwwAuthenticate.Select(static h => h.ToString()));
            Assert.Equal("SECURITY_DENIED_001", await CodeOf(anonymous));
        }

        // 有效令牌放行，没有质询
        using (var ok = await SendAsync(client, JwtTokens.Create()))
        {
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            Assert.Empty(ok.Headers.WwwAuthenticate);
        }

        await app.StopAsync();
    }

    /// <summary>
    /// 带（或不带）Bearer 令牌请求 <c>/me</c>。
    /// </summary>
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

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
