// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;
using System.Web;
using XiHan.Framework.Authentication.OAuth;

namespace XiHan.Framework.Authentication.Tests.OAuth.Infrastructure;

/// <summary>
/// 承载 OAuth 注册结果的测试宿主
/// </summary>
/// <remarks>
/// 暴露两个端点：<c>/challenge?scheme=</c> 发起指定方案的挑战，<c>/me</c> 读取外部登录中转方案里的声明。
/// </remarks>
public sealed class OAuthTestHost : IAsyncDisposable
{
    private readonly IHost _host;

    private OAuthTestHost(IHost host)
    {
        _host = host;
        Client = host.GetTestClient();
    }

    /// <summary>
    /// 指向测试宿主的客户端，不自动跟随重定向
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// 宿主的服务提供者
    /// </summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// 用内存配置启动测试宿主
    /// </summary>
    /// <param name="settings">配置项</param>
    /// <param name="configureServices">追加的服务注册</param>
    /// <returns>测试宿主</returns>
    public static async Task<OAuthTestHost> StartAsync(
        Dictionary<string, string?> settings,
        Action<IServiceCollection>? configureServices = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddAuthentication(XiHanOAuthServiceCollectionExtensions.ExternalSignInScheme)
                        .AddCookie(XiHanOAuthServiceCollectionExtensions.ExternalSignInScheme);
                    services.AddXiHanOAuth(configuration);
                    configureServices?.Invoke(services);
                });
                webHost.Configure(app =>
                {
                    app.UseAuthentication();
                    app.Run(async context =>
                    {
                        if (context.Request.Path == "/challenge")
                        {
                            await context.ChallengeAsync(
                                context.Request.Query["scheme"].ToString(),
                                new AuthenticationProperties { RedirectUri = "/done" });
                            return;
                        }

                        if (context.Request.Path == "/me")
                        {
                            var result = await context.AuthenticateAsync(XiHanOAuthServiceCollectionExtensions.ExternalSignInScheme);
                            if (!result.Succeeded)
                            {
                                context.Response.StatusCode = 401;
                                return;
                            }

                            var claims = result.Principal!.Claims.Select(c => $"{c.Type}={c.Value}");
                            await context.Response.WriteAsync(string.Join("\n", claims));
                            return;
                        }

                        context.Response.StatusCode = 404;
                    });
                });
            })
            .StartAsync();

        return new OAuthTestHost(host);
    }

    /// <summary>
    /// 发起挑战并取回重定向地址
    /// </summary>
    /// <param name="scheme">认证方案名称</param>
    /// <returns>授权地址与挑战响应</returns>
    public async Task<(Uri Location, HttpResponseMessage Response)> ChallengeAsync(string scheme)
    {
        var response = await Client.GetAsync($"/challenge?scheme={Uri.EscapeDataString(scheme)}");
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        return (response.Headers.Location!, response);
    }

    /// <summary>
    /// 读取授权地址上的查询参数
    /// </summary>
    /// <param name="location">授权地址</param>
    /// <returns>查询参数</returns>
    public static Dictionary<string, string> ParseQuery(Uri location)
    {
        var parsed = HttpUtility.ParseQueryString(location.Query);
        return parsed.AllKeys
            .Where(key => key is not null)
            .ToDictionary(key => key!, key => parsed[key] ?? string.Empty, StringComparer.Ordinal);
    }

    /// <summary>
    /// 用挑战响应中的关联 Cookie 回调认证端点
    /// </summary>
    /// <param name="callbackPath">回调路径</param>
    /// <param name="code">授权码</param>
    /// <param name="state">状态串</param>
    /// <param name="challenge">挑战响应</param>
    /// <returns>回调响应</returns>
    public async Task<HttpResponseMessage> CallbackAsync(
        string callbackPath,
        string code,
        string state,
        HttpResponseMessage challenge,
        string stateParameterName = "state")
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{callbackPath}?code={Uri.EscapeDataString(code)}&{stateParameterName}={Uri.EscapeDataString(state)}");

        foreach (var cookie in challenge.Headers.GetValues("Set-Cookie"))
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        return await Client.SendAsync(request);
    }

    /// <summary>
    /// 用回调响应写入的登录 Cookie 读取声明
    /// </summary>
    /// <param name="callback">回调响应</param>
    /// <returns>声明文本，每行一条</returns>
    public async Task<string> GetClaimsAsync(HttpResponseMessage callback)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        foreach (var cookie in callback.Headers.GetValues("Set-Cookie"))
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// 从声明文本中取出指定类型的声明值
    /// </summary>
    /// <param name="claims">声明文本</param>
    /// <param name="claimType">声明类型</param>
    /// <returns>声明值</returns>
    public static string? ReadClaim(string claims, string claimType)
    {
        return claims.Split('\n')
            .FirstOrDefault(line => line.StartsWith(claimType + "=", StringComparison.Ordinal))
            ?[(claimType.Length + 1)..];
    }

    /// <summary>
    /// 常用声明类型
    /// </summary>
    public static class ClaimTypeNames
    {
        /// <summary>
        /// 登录标识
        /// </summary>
        public const string NameIdentifier = ClaimTypes.NameIdentifier;

        /// <summary>
        /// 姓名
        /// </summary>
        public const string Name = ClaimTypes.Name;
    }

    /// <summary>
    /// 释放宿主
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }
}
