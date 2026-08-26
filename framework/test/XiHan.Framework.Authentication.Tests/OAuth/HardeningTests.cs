// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Claims;
using XiHan.Framework.Authentication.OAuth;
using XiHan.Framework.Authentication.OAuth.Handlers;
using XiHan.Framework.Authentication.Tests.OAuth.Infrastructure;

namespace XiHan.Framework.Authentication.Tests.OAuth;

/// <summary>
/// 自研提供商的加固行为测试
/// </summary>
/// <remarks>
/// 覆盖对抗审查提出的几处：凭据不得进日志、状态串搬运与还原必须对称、
/// 权限范围回显缺失时不静默留空、重写令牌请求不得吞掉 PKCE 校验串。
/// </remarks>
public class HardeningTests
{
    /// <summary>
    /// 响应体进日志前必须抹掉令牌取值
    /// </summary>
    /// <remarks>
    /// 令牌接口的响应体本身就是凭据；出错日志常被汇聚到访问控制远弱于凭据本身的平台。
    /// </remarks>
    [Fact]
    public async Task TokenResponseBody_IsRedactedBeforeLogging()
    {
        var logs = new CapturingLoggerProvider();
        var handler = new StubHttpMessageHandler()
            .Respond("/sns/oauth2/access_token", """<html>{"access_token":"super-secret-token","openid":"open-1"}</html>""");

        var provider = new OAuthProviderConfig
        {
            Name = "wechat",
            Provider = OAuthProviderNames.WeChat,
            ClientId = "wx-open",
            ClientSecret = "wx-open-secret"
        };

        await using var host = await OAuthTestHost.StartAsync(
            OAuthConfigurationBuilder.Build(provider),
            services =>
            {
                services.AddLogging(builder => builder.AddProvider(logs));
                services.PostConfigure<WeixinAuthenticationOptions>(
                    provider.Name,
                    options => options.Backchannel = new HttpClient(handler));
            });

        var (location, challenge) = await host.ChallengeAsync(provider.Name);

        await Assert.ThrowsAsync<AuthenticationFailureException>(() => host.CallbackAsync(
            "/signin-wechat",
            "code-1",
            OAuthTestHost.ParseQuery(location)["state"],
            challenge));

        Assert.False(logs.Contains("super-secret-token"), "令牌明文不应出现在日志里");
        Assert.True(logs.Contains("\"access_token\":\"***\""), "令牌取值应被抹掉");
    }

    /// <summary>
    /// 扫码链路上伪造的状态搬运参数不应顶掉真实状态串
    /// </summary>
    /// <remarks>
    /// 搬运只发生在账号授权链路，还原也必须同样门控，否则扫码登录可被一个同名查询参数打断。
    /// </remarks>
    [Fact]
    public async Task QrCodeCallback_WithForgedShortState_StillSignsIn()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/sns/oauth2/access_token", """
                {"access_token":"at","expires_in":7200,"openid":"open-1","scope":"snsapi_login","unionid":"union-1"}
                """)
            .Respond("/sns/userinfo", """{"openid":"open-1","nickname":"李四","unionid":"union-1"}""");

        var provider = new OAuthProviderConfig
        {
            Name = "wechat",
            Provider = OAuthProviderNames.WeChat,
            Mode = OAuthLoginMode.QrCode,
            ClientId = "wx-open",
            ClientSecret = "wx-open-secret"
        };

        await using var host = await OAuthTestHost.StartAsync(
            OAuthConfigurationBuilder.Build(provider),
            services => services.PostConfigure<WeixinAuthenticationOptions>(
                provider.Name,
                options => options.Backchannel = new HttpClient(handler)));

        var (location, challenge) = await host.ChallengeAsync(provider.Name);
        var state = OAuthTestHost.ParseQuery(location)["state"];

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/signin-wechat?code=code-1&state={Uri.EscapeDataString(state)}&_oauthstate=forged");
        foreach (var cookie in challenge.Headers.GetValues("Set-Cookie"))
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }

        var callback = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/done", callback.Headers.Location!.ToString());
    }

    /// <summary>
    /// 令牌响应没回显权限范围时仍应去拉用户资料，而不是静默留空
    /// </summary>
    [Fact]
    public async Task Weixin_TokenWithoutScopeEcho_StillFetchesUserInfo()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/sns/oauth2/access_token", """{"access_token":"at","expires_in":7200,"openid":"open-7","unionid":"union-7"}""")
            .Respond("/sns/userinfo", """{"openid":"open-7","nickname":"王五","headimgurl":"https://cdn.demo.com/x.png","unionid":"union-7"}""");

        var claims = await RunWeixinAsync(handler, OAuthLoginMode.Account);

        Assert.Equal("union-7", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Equal("王五", OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
        Assert.Equal("https://cdn.demo.com/x.png", OAuthTestHost.ReadClaim(claims, OAuthOptions.AvatarClaimType));
        Assert.Contains(handler.Requests, request => request.Url.Contains("/sns/userinfo", StringComparison.Ordinal));
    }

    /// <summary>
    /// 明确回显 snsapi_base 时仍应跳过用户资料接口
    /// </summary>
    [Fact]
    public async Task Weixin_BaseScopeEcho_SkipsUserInfo()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/sns/oauth2/access_token", """{"access_token":"at","expires_in":7200,"openid":"open-8","scope":"snsapi_base"}""");

        var claims = await RunWeixinAsync(handler, OAuthLoginMode.Account);

        Assert.Equal("open-8", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.DoesNotContain(handler.Requests, request => request.Url.Contains("/sns/userinfo", StringComparison.Ordinal));
    }

    /// <summary>
    /// 启用 PKCE 时飞书的令牌请求必须带上校验串
    /// </summary>
    /// <remarks>
    /// 飞书没有重写授权地址构造，基类在启用 PKCE 时会带 code_challenge，令牌请求漏发 code_verifier 会被直接拒绝。
    /// </remarks>
    [Fact]
    public async Task Feishu_WithPkce_SendsCodeVerifier()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/suite/passport/oauth/token", """{"access_token":"user-token","token_type":"Bearer","expires_in":7200}""")
            .Respond("/suite/passport/oauth/userinfo", """{"name":"赵六","open_id":"open-1","union_id":"union-1"}""");

        var provider = new OAuthProviderConfig
        {
            Name = "feishu",
            ClientId = "cli_app",
            ClientSecret = "app-secret"
        };

        await using var host = await OAuthTestHost.StartAsync(
            OAuthConfigurationBuilder.Build(provider),
            services => services.PostConfigure<FeishuAuthenticationOptions>(
                provider.Name,
                options =>
                {
                    options.UsePkce = true;
                    options.Backchannel = new HttpClient(handler);
                }));

        var (location, challenge) = await host.ChallengeAsync(provider.Name);
        var query = OAuthTestHost.ParseQuery(location);

        Assert.Equal("S256", query["code_challenge_method"]);

        var callback = await host.CallbackAsync("/signin-feishu", "code-1", query["state"], challenge);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Contains("code_verifier=", handler.RequestFor("/suite/passport/oauth/token").Body!, StringComparison.Ordinal);
    }

    private static async Task<string> RunWeixinAsync(StubHttpMessageHandler handler, OAuthLoginMode mode)
    {
        var provider = new OAuthProviderConfig
        {
            Name = "wechat",
            Provider = OAuthProviderNames.WeChat,
            Mode = mode,
            ClientId = "wx-mp",
            ClientSecret = "wx-mp-secret"
        };

        await using var host = await OAuthTestHost.StartAsync(
            OAuthConfigurationBuilder.Build(provider),
            services => services.PostConfigure<WeixinAuthenticationOptions>(
                provider.Name,
                options => options.Backchannel = new HttpClient(handler)));

        var (location, challenge) = await host.ChallengeAsync(provider.Name);
        var query = OAuthTestHost.ParseQuery(location);
        var realState = OAuthTestHost.ParseQuery(new Uri(query["redirect_uri"]))["_oauthstate"];

        var callback = await host.CallbackAsync("/signin-wechat", "code-1", realState, challenge, "_oauthstate");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        return await host.GetClaimsAsync(callback);
    }
}
