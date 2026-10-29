// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Authentication;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.InMemory;

namespace XiHan.Framework.Security.Jwt.Tests;

/// <summary>
/// 认证方案 <c>XiHan</c>（ADR-0051）：绕过内核管道的端点经 ASP.NET Core 认证，凭据交给内核已装配的处理器（JWT 与 ApiKey 同一条路），
/// 查询串令牌只在登记的路径前缀下接受，挂点可以拒绝，主体转成带名称与角色的 <see cref="ClaimsPrincipal"/>；在端点里建的调用上下文带着同一凭据，经管道时照常重新认证。
/// </summary>
public sealed class BypassEndpointAuthenticationTests
{
    /// <summary>
    /// 端点的授权：只经 <c>XiHan</c> 方案认证。
    /// </summary>
    private static readonly AuthorizeAttribute _xihan = new() { AuthenticationSchemes = InvocationAuthenticationDefaults.AuthenticationScheme };

    [Fact]
    public async Task HeaderAndQueryTokens_AuthenticateThroughTheKernelHandlers()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        // Authorization 头：JWT 经内核的 Bearer 处理器，名称是主体 ID、角色按 RoleClaim、声明保留原始类型名
        using (var header = await SendAsync(client, "/hubs/me", bearer: JwtTokens.Create(claims: ("role", "admin"))))
        {
            Assert.Equal(HttpStatusCode.OK, header.StatusCode);
            Assert.Equal("alice|XiHan|admin=True|sub=alice|credential=Bearer", await header.Content.ReadAsStringAsync());
        }

        // 登记过的路径前缀下，查询串令牌等同 Authorization 头（按路径段、不区分大小写）
        using (var query = await client.GetAsync("/HUBS/me?access_token=" + JwtTokens.Create()))
        {
            Assert.Equal(HttpStatusCode.OK, query.StatusCode);
            Assert.Equal("alice|XiHan|admin=False|sub=alice|credential=Bearer", await query.Content.ReadAsStringAsync());
        }

    }

    [Fact]
    public async Task OtherKernelSchemes_TakeTheSameRoute()
    {
        // 只装内存安全：ApiKey 处理器就是内核的认证处理器，方案 XiHan 照样交给它
        await using var app = await StartAsync(jwt: false);
        using var client = app.GetTestClient();
        using (var apiKey = await SendAsync(client, "/hubs/me", scheme: "ApiKey", value: "svc-api-key"))
        {
            Assert.Equal(HttpStatusCode.OK, apiKey.StatusCode);
            Assert.Equal("svc|XiHan|admin=True|sub=|credential=ApiKey", await apiKey.Content.ReadAsStringAsync());
        }

        using var wrongKey = await SendAsync(client, "/hubs/me", scheme: "ApiKey", value: "wrong-api-key");
        Assert.Equal((HttpStatusCode.Unauthorized, "ApiKey"), (wrongKey.StatusCode, wrongKey.Headers.WwwAuthenticate.Single().ToString()));
    }

    [Fact]
    public async Task QueryTokens_OutsideRegisteredPaths_AreIgnored()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var token = JwtTokens.Create();

        // 没登记的路径：查询串令牌一律不认，请求按匿名处理，受保护的端点 401
        foreach (var path in new[] { "/other/me", "/hubsx/me" })
        {
            using var response = await client.GetAsync(path + "?access_token=" + token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(["Bearer"], response.Headers.WwwAuthenticate.Select(static h => h.ToString()));
        }

        // 同一路径带头就放行：拒绝的只是查询串
        using var withHeader = await SendAsync(client, "/other/me", bearer: token);
        Assert.Equal(HttpStatusCode.OK, withHeader.StatusCode);

        // 查询串令牌多于一个值也不认
        using var twice = await client.GetAsync($"/hubs/me?access_token={token}&access_token={token}");
        Assert.Equal(HttpStatusCode.Unauthorized, twice.StatusCode);
    }

    [Fact]
    public async Task Failures_ChallengeLikeThePipeline_AndTheFilterCanReject()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        // 凭据无效：401 + invalid_token，与管道的质询一致
        using (var expired = await SendAsync(client, "/hubs/me", bearer: JwtTokens.Create(lifetime: TimeSpan.FromMinutes(-1))))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
            Assert.Equal(["Bearer error=\"invalid_token\""], expired.Headers.WwwAuthenticate.Select(static h => h.ToString()));
        }

        // 方案不支持：401，按已装配的方案质询
        using (var unsupported = await SendAsync(client, "/hubs/me", scheme: "Basic", value: "YTpi"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unsupported.StatusCode);
            Assert.Equal(["Bearer"], unsupported.Headers.WwwAuthenticate.Select(static h => h.ToString()));
        }

        // 挂点拒绝（会话已失效）：认证失败
        using (var revoked = await SendAsync(client, "/hubs/me", bearer: JwtTokens.Create(claims: ("sid", RevokedSessions.Revoked))))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
            Assert.Equal(["Bearer error=\"invalid_token\""], revoked.Headers.WwwAuthenticate.Select(static h => h.ToString()));
        }
    }

    [Fact]
    public async Task ContextsBuiltInTheEndpoint_ReauthenticateThroughThePipeline()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();

        // 端点里以请求建调用上下文并经管道：凭据（查询串令牌）随之带上，Authenticate 阶段重新认证
        Assert.Equal("alice", await client.GetStringAsync("/hubs/invoke?access_token=" + JwtTokens.Create()));

        // 普通端点仍由管道认证；XiHan 方案只认绕过管道的端点，不对普通请求再认证一遍（挂点不被调用）
        var filter = app.Services.GetRequiredService<RevokedSessions>();
        var before = filter.Calls;
        using (var pipeline = await SendAsync(client, "/api/me", bearer: JwtTokens.Create()))
        {
            Assert.Equal(HttpStatusCode.OK, pipeline.StatusCode);
            Assert.Equal("alice", await pipeline.Content.ReadAsStringAsync());
        }

        Assert.Equal(before, filter.Calls);
    }

    [Fact]
    public void Options_RejectRootPrefixes_AndMatchBySegment()
    {
        var options = new InvocationAuthenticationOptions { QueryTokenPaths = { "/hubs" } };
        options.Validate();
        Assert.True(options.AcceptsQueryToken("/hubs"));
        Assert.True(options.AcceptsQueryToken("/Hubs/notification/negotiate"));
        Assert.False(options.AcceptsQueryToken("/hubsx"));
        Assert.False(options.AcceptsQueryToken("/api/hubs"));
        Assert.Throws<ArgumentException>(static () => new InvocationAuthenticationOptions { QueryTokenPaths = { "/" } }.Validate());
        Assert.Throws<ArgumentException>(static () => new InvocationAuthenticationOptions { QueryTokenPaths = { PathString.Empty } }.Validate());
    }

    /// <summary>
    /// 启动宿主：内存安全（ApiKey）+ JWT（<paramref name="jwt"/> 为假时只装内存安全），方案 <c>XiHan</c> 分两次登记（第二次只加路径），挂点拒绝已失效的会话；
    /// <c>/hubs/*</c> 与 <c>/other/me</c> 绕过管道，<c>/api/me</c> 是普通端点。
    /// </summary>
    private static async Task<WebApplication> StartAsync(bool jwt = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(
        [
            new("XiHan:Security:Jwt:Secret", JwtTokens.Secret),
            new("XiHan:Security:Jwt:Issuer", JwtTokens.Issuer),
            new("XiHan:Security:Jwt:Audience", JwtTokens.Audience),
            new("XiHan:Security:InMemory:Principals:svc:ApiKey", "svc-api-key"),
            new("XiHan:Security:InMemory:Principals:svc:Roles", "admin"),
        ]);
        builder.AddXiHan(jwt ? static xihan => xihan.UseFeature<InMemorySecurityFeature>().UseFeature<JwtSecurityFeature>() : static xihan => xihan.UseFeature<InMemorySecurityFeature>());
        builder.Services.AddAuthentication().AddXiHan().AddXiHan(static options => options.QueryTokenPaths.Add("/hubs"));
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<RevokedSessions>();
        builder.Services.AddSingleton<IInvocationAuthenticationFilter>(static sp => sp.GetRequiredService<RevokedSessions>());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseXiHan();
        PipelineBypassMetadata.BypassXiHanPipeline(app.MapGet("/hubs/me", Describe).RequireAuthorization(_xihan));
        PipelineBypassMetadata.BypassXiHanPipeline(app.MapGet("/hubsx/me", Describe).RequireAuthorization(_xihan));
        PipelineBypassMetadata.BypassXiHanPipeline(app.MapGet("/other/me", Describe).RequireAuthorization(_xihan));
        PipelineBypassMetadata.BypassXiHanPipeline(app.MapGet("/hubs/invoke", (Func<HttpContext, Task<string>>)InvokeAsync).RequireAuthorization(_xihan));
        app.MapGet("/api/me", static (HttpContext http) => http.GetXiHanContext().ValueOrThrow().GetSecurity().Principal.Id).RequireXiHanAuthentication();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 端点里看到的主体：名称、认证类型、是否在 admin 角色、原始 sub 声明，以及以请求建的调用上下文带着的凭据方案。
    /// </summary>
    private static string Describe(HttpContext http)
    {
        var user = http.User;
        var credential = HttpInvocation.CreateContext(http, http.RequestServices).GetCredential();
        return $"{user.Identity?.Name}|{user.Identity?.AuthenticationType}|admin={user.IsInRole("admin")}|sub={user.FindFirst("sub")?.Value}|credential={(credential.TryGetValue(out var c) ? c.Scheme : "-")}";
    }

    /// <summary>
    /// 在绕过管道的端点里发起一次内核调用，返回管道认证出的主体 ID。
    /// </summary>
    private static async Task<string> InvokeAsync(HttpContext http)
    {
        var principal = string.Empty;
        var context = HttpInvocation.CreateContext(http, http.RequestServices);
        var outcome = await http.RequestServices.GetRequiredService<App>().InvokeAsync(context, ctx =>
        {
            principal = ctx.GetSecurity().Principal.Id;
            return ValueTask.CompletedTask;
        });
        return outcome.IsSuccess ? principal : outcome.Error!.Code;
    }

    /// <summary>
    /// 带凭据请求。
    /// </summary>
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string? bearer = null, string? scheme = null, string? value = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        else if (scheme is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, value);
        }

        return client.SendAsync(request);
    }

    /// <summary>
    /// 挂点：令牌的 <c>sid</c> 是已失效的会话即拒绝；记录被调用的次数。
    /// </summary>
    private sealed class RevokedSessions : IInvocationAuthenticationFilter
    {
        /// <summary>
        /// 已失效的会话。
        /// </summary>
        public const string Revoked = "revoked-session";

        /// <summary>
        /// 被调用的次数。
        /// </summary>
        private int _calls;

        /// <summary>
        /// 被调用的次数。
        /// </summary>
        public int Calls => Volatile.Read(ref _calls);

        /// <inheritdoc />
        public ValueTask<Result> AuthenticatedAsync(Principal principal, InvocationContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return ValueTask.FromResult(principal.HasClaim("sid", Revoked)
                ? Result.Failure(Error.Denied(SecurityErrors.InvalidCredential, SecurityMessages.InvalidToken))
                : Result.Success());
        }
    }
}
