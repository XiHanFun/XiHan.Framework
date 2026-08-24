// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Security.Claims;
using XiHan.Framework.Authentication.OAuth;
using XiHan.Framework.Authentication.OAuth.Handlers;
using XiHan.Framework.Authentication.Tests.OAuth.Infrastructure;

namespace XiHan.Framework.Authentication.Tests.OAuth;

/// <summary>
/// 各提供商回调流程的测试
/// </summary>
/// <remarks>
/// 从挑战一路走到回调，用桩件顶掉出网请求，断言请求形态与最终落到登录态里的声明。
/// 重点覆盖各家偏离 OAuth2 通用约定的地方：字段命名、错误表达、额外的接口跳数。
/// </remarks>
public class LoginFlowTests
{
    /// <summary>
    /// 钉钉应把小驼峰的令牌字段改写成标准名，并用私有请求头拉用户信息
    /// </summary>
    [Fact]
    public async Task DingTalk_SignsInWithUnionIdAndPrivateHeader()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/v1.0/oauth2/userAccessToken", """{"accessToken":"user-token","refreshToken":"rt","expireIn":7200}""")
            .Respond("/v1.0/contact/users/me", """
                {"nick":"张三","avatarUrl":"https://cdn.demo.com/a.png","openId":"open-1","unionId":"union-1","email":"zhang@demo.com","mobile":"13800000000"}
                """);

        var claims = await RunAsync<DingTalkAuthenticationOptions>(
            new OAuthProviderConfig { Name = "dingtalk", ClientId = "app-key", ClientSecret = "app-secret" },
            handler);

        Assert.Equal("union-1", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Equal("张三", OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
        Assert.Equal("open-1", OAuthTestHost.ReadClaim(claims, OAuthClaimTypes.DingTalk.OpenId));
        Assert.Equal("13800000000", OAuthTestHost.ReadClaim(claims, OAuthClaimTypes.DingTalk.Mobile));
        Assert.Equal("https://cdn.demo.com/a.png", OAuthTestHost.ReadClaim(claims, OAuthOptions.AvatarClaimType));

        var tokenRequest = handler.RequestFor("/v1.0/oauth2/userAccessToken");
        Assert.Equal("POST", tokenRequest.Method);
        Assert.Contains("\"grantType\":\"authorization_code\"", tokenRequest.Body!, StringComparison.Ordinal);

        var userRequest = handler.RequestFor("/v1.0/contact/users/me");
        Assert.Equal("user-token", userRequest.Headers.GetValues(OAuthProviderEndpoints.DingTalk.AccessTokenHeaderName).Single());
        Assert.Null(userRequest.Headers.Authorization);
    }

    /// <summary>
    /// 微信应以 appid/secret 换令牌，并以 unionid 作为登录标识
    /// </summary>
    [Fact]
    public async Task Weixin_SignsInWithUnionIdAsIdentifier()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/sns/oauth2/access_token", """
                {"access_token":"at","expires_in":7200,"openid":"open-1","scope":"snsapi_login","unionid":"union-1"}
                """)
            .Respond("/sns/userinfo", """
                {"openid":"open-1","nickname":"李四","headimgurl":"https://cdn.demo.com/b.png","unionid":"union-1","city":"杭州"}
                """);

        var claims = await RunAsync<WeixinAuthenticationOptions>(
            new OAuthProviderConfig
            {
                Name = "wechat",
                Provider = OAuthProviderNames.WeChat,
                ClientId = "wx-open",
                ClientSecret = "wx-open-secret"
            },
            handler);

        Assert.Equal("union-1", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Equal("李四", OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
        Assert.Equal("杭州", OAuthTestHost.ReadClaim(claims, OAuthClaimTypes.Weixin.City));
        Assert.Equal("https://cdn.demo.com/b.png", OAuthTestHost.ReadClaim(claims, OAuthOptions.AvatarClaimType));

        var tokenRequest = handler.RequestFor("/sns/oauth2/access_token");
        Assert.Contains("appid=wx-open", tokenRequest.Url, StringComparison.Ordinal);
        Assert.Contains("secret=wx-open-secret", tokenRequest.Url, StringComparison.Ordinal);
    }

    /// <summary>
    /// 微信授权范围只有 snsapi_base 时不应再调用用户资料接口
    /// </summary>
    [Fact]
    public async Task Weixin_BaseScope_SkipsUserInfoCall()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/sns/oauth2/access_token", """
                {"access_token":"at","expires_in":7200,"openid":"open-2","scope":"snsapi_base"}
                """);

        var claims = await RunAsync<WeixinAuthenticationOptions>(
            new OAuthProviderConfig
            {
                Name = "wechat",
                Provider = OAuthProviderNames.WeChat,
                ClientId = "wx-open",
                ClientSecret = "wx-open-secret",
                Scopes = ["snsapi_base"]
            },
            handler);

        Assert.Equal("open-2", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.DoesNotContain(handler.Requests, request => request.Url.Contains("/sns/userinfo", StringComparison.Ordinal));
    }

    /// <summary>
    /// 微信令牌接口用 errcode 表达失败，应中断回调
    /// </summary>
    [Fact]
    public async Task Weixin_ErrorCode_Aborts()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/sns/oauth2/access_token", """{"errcode":40029,"errmsg":"invalid code"}""");

        await Assert.ThrowsAsync<AuthenticationFailureException>(() => RunAsync<WeixinAuthenticationOptions>(
            new OAuthProviderConfig
            {
                Name = "wechat",
                Provider = OAuthProviderNames.WeChat,
                ClientId = "wx-open",
                ClientSecret = "wx-open-secret"
            },
            handler));
    }

    /// <summary>
    /// 企业微信应换企业凭证、再换成员身份，并凭 user_ticket 补齐敏感信息
    /// </summary>
    [Fact]
    public async Task WorkWeixin_MergesIdentityAndDetail()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/cgi-bin/gettoken", """{"errcode":0,"access_token":"corp-token","expires_in":7200}""")
            .Respond("/cgi-bin/auth/getuserinfo", """{"errcode":0,"userid":"zhangsan","user_ticket":"ticket-1"}""")
            .Respond("/cgi-bin/auth/getuserdetail", """
                {"errcode":0,"userid":"zhangsan","avatar":"https://cdn.demo.com/c.png","mobile":"13700000000","email":"zhang@demo.com"}
                """);

        var claims = await RunAsync<WorkWeixinAuthenticationOptions>(CreateWorkWeixin(), handler);

        Assert.Equal("zhangsan", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Equal("13700000000", OAuthTestHost.ReadClaim(claims, OAuthClaimTypes.WorkWeixin.Mobile));
        Assert.Equal("zhang@demo.com", OAuthTestHost.ReadClaim(claims, ClaimTypes.Email));
        Assert.Equal("https://cdn.demo.com/c.png", OAuthTestHost.ReadClaim(claims, OAuthOptions.AvatarClaimType));
    }

    /// <summary>
    /// 企业微信开启成员资料读取时，通讯录接口失败不应中断登录
    /// </summary>
    [Fact]
    public async Task WorkWeixin_MemberProfileDenied_KeepsLoginSucceeding()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/cgi-bin/gettoken", """{"errcode":0,"access_token":"corp-token","expires_in":7200}""")
            .Respond("/cgi-bin/auth/getuserinfo", """{"errcode":0,"userid":"zhangsan"}""")
            .Respond("/cgi-bin/user/get", """{"errcode":60011,"errmsg":"no privilege"}""");

        var provider = CreateWorkWeixin();
        provider.LoadMemberProfile = true;

        var claims = await RunAsync<WorkWeixinAuthenticationOptions>(provider, handler);

        Assert.Equal("zhangsan", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Null(OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
    }

    /// <summary>
    /// 企业微信开启成员资料读取且通讯录可读时，应补上姓名
    /// </summary>
    [Fact]
    public async Task WorkWeixin_MemberProfileAllowed_FillsName()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/cgi-bin/gettoken", """{"errcode":0,"access_token":"corp-token","expires_in":7200}""")
            .Respond("/cgi-bin/auth/getuserinfo", """{"errcode":0,"userid":"zhangsan"}""")
            .Respond("/cgi-bin/user/get", """{"errcode":0,"userid":"zhangsan","name":"张三","mobile":"13700000000"}""");

        var provider = CreateWorkWeixin();
        provider.LoadMemberProfile = true;

        var claims = await RunAsync<WorkWeixinAuthenticationOptions>(provider, handler);

        Assert.Equal("张三", OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
    }

    /// <summary>
    /// QQ 应先换 openid 再取资料，两次请求都要求返回 JSON
    /// </summary>
    [Fact]
    public async Task QQ_ResolvesOpenIdBeforeUserInfo()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/oauth2.0/token", """{"access_token":"at","expires_in":7776000,"refresh_token":"rt"}""")
            .Respond("/oauth2.0/me", """{"client_id":"qq-client","openid":"open-1"}""")
            .Respond("/user/get_user_info", """
                {"ret":0,"msg":"","nickname":"王五","figureurl_qq_2":"https://cdn.demo.com/d.png","gender":"男"}
                """);

        var claims = await RunAsync<QQAuthenticationOptions>(
            new OAuthProviderConfig { Name = "qq", ClientId = "qq-client", ClientSecret = "qq-secret" },
            handler);

        Assert.Equal("open-1", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Equal("王五", OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
        Assert.Equal("https://cdn.demo.com/d.png", OAuthTestHost.ReadClaim(claims, OAuthOptions.AvatarClaimType));

        Assert.Contains("fmt=json", handler.RequestFor("/oauth2.0/token").Url, StringComparison.Ordinal);
        Assert.Contains("fmt=json", handler.RequestFor("/oauth2.0/me").Url, StringComparison.Ordinal);
        Assert.Contains("oauth_consumer_key=qq-client", handler.RequestFor("/user/get_user_info").Url, StringComparison.Ordinal);
    }

    /// <summary>
    /// QQ 用户信息接口用 ret 表达失败，应中断回调
    /// </summary>
    [Fact]
    public async Task QQ_NonZeroReturnCode_Aborts()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/oauth2.0/token", """{"access_token":"at"}""")
            .Respond("/oauth2.0/me", """{"openid":"open-1"}""")
            .Respond("/user/get_user_info", """{"ret":1002,"msg":"请先登录"}""");

        await Assert.ThrowsAsync<AuthenticationFailureException>(() => RunAsync<QQAuthenticationOptions>(
            new OAuthProviderConfig { Name = "qq", ClientId = "qq-client", ClientSecret = "qq-secret" },
            handler));
    }

    /// <summary>
    /// 飞书扫码登录应以表单换令牌，并读平铺的用户信息
    /// </summary>
    [Fact]
    public async Task Feishu_QrCode_UsesFormTokenAndFlatUserInfo()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/suite/passport/oauth/token", """{"access_token":"user-token","token_type":"Bearer","expires_in":7200}""")
            .Respond("/suite/passport/oauth/userinfo", """
                {"sub":"sub-1","name":"赵六","avatar_url":"https://cdn.demo.com/e.png","open_id":"open-1","union_id":"union-1","user_id":"user-1"}
                """);

        var claims = await RunAsync<FeishuAuthenticationOptions>(
            new OAuthProviderConfig { Name = "feishu", ClientId = "cli_app", ClientSecret = "app-secret" },
            handler);

        Assert.Equal("union-1", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Equal("赵六", OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
        Assert.Equal("user-1", OAuthTestHost.ReadClaim(claims, OAuthClaimTypes.Feishu.UserId));

        var tokenRequest = handler.RequestFor("/suite/passport/oauth/token");
        Assert.Contains("grant_type=authorization_code", tokenRequest.Body!, StringComparison.Ordinal);
        Assert.Contains("client_secret=app-secret", tokenRequest.Body!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 飞书账号授权应以 JSON 换令牌，并从 data 节点读用户信息
    /// </summary>
    [Fact]
    public async Task Feishu_Account_UsesJsonTokenAndDataNode()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/open-apis/authen/v2/oauth/token", """{"code":0,"access_token":"user-token","expires_in":7200}""")
            .Respond("/open-apis/authen/v1/user_info", """
                {"code":0,"data":{"name":"钱七","avatar_url":"https://cdn.demo.com/f.png","open_id":"open-2","union_id":"union-2","email":"qian@demo.com"}}
                """);

        var claims = await RunAsync<FeishuAuthenticationOptions>(
            new OAuthProviderConfig
            {
                Name = "feishu",
                Mode = OAuthLoginMode.Account,
                ClientId = "cli_app",
                ClientSecret = "app-secret"
            },
            handler);

        Assert.Equal("union-2", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Equal("钱七", OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
        Assert.Equal("qian@demo.com", OAuthTestHost.ReadClaim(claims, ClaimTypes.Email));

        Assert.Contains("\"grant_type\":\"authorization_code\"", handler.RequestFor("/open-apis/authen/v2/oauth/token").Body!, StringComparison.Ordinal);
    }

    /// <summary>
    /// 飞书用响应体里的 code 表达失败，应中断回调
    /// </summary>
    [Fact]
    public async Task Feishu_NonZeroCode_Aborts()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/suite/passport/oauth/token", """{"code":20037,"msg":"code 已失效"}""");

        await Assert.ThrowsAsync<AuthenticationFailureException>(() => RunAsync<FeishuAuthenticationOptions>(
            new OAuthProviderConfig { Name = "feishu", ClientId = "cli_app", ClientSecret = "app-secret" },
            handler));
    }

    /// <summary>
    /// GitHub 在用户信息没给出邮箱且申请过邮箱权限时应补取主邮箱
    /// </summary>
    [Fact]
    public async Task GitHub_PrivateEmail_FallsBackToEmailsEndpoint()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/login/oauth/access_token", """{"access_token":"at","token_type":"bearer"}""")
            .Respond("/user/emails", """[{"email":"secondary@demo.com","primary":false},{"email":"primary@demo.com","primary":true}]""")
            .Respond("/user", """{"id":42,"login":"octocat","name":"The Octocat","avatar_url":"https://cdn.demo.com/g.png"}""");

        var claims = await RunAsync<GitHubAuthenticationOptions>(
            new OAuthProviderConfig
            {
                Name = "github",
                ClientId = "github-client",
                ClientSecret = "github-secret",
                Scopes = ["user:email"]
            },
            handler);

        Assert.Equal("42", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Equal("octocat", OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
        Assert.Equal("primary@demo.com", OAuthTestHost.ReadClaim(claims, ClaimTypes.Email));
    }

    /// <summary>
    /// GitHub 未申请邮箱权限时不应调用邮箱列表接口
    /// </summary>
    [Fact]
    public async Task GitHub_WithoutEmailScope_SkipsEmailsEndpoint()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/login/oauth/access_token", """{"access_token":"at","token_type":"bearer"}""")
            .Respond("/user", """{"id":42,"login":"octocat"}""");

        await RunAsync<GitHubAuthenticationOptions>(
            new OAuthProviderConfig { Name = "github", ClientId = "github-client", ClientSecret = "github-secret" },
            handler);

        Assert.DoesNotContain(handler.Requests, request => request.Url.Contains("/user/emails", StringComparison.Ordinal));
    }

    /// <summary>
    /// Google 应用 Bearer 令牌拉取用户信息
    /// </summary>
    [Fact]
    public async Task Google_UsesBearerUserInfo()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/token", """{"access_token":"at","token_type":"Bearer","expires_in":3600}""")
            .Respond("/oauth2/v3/userinfo", """
                {"sub":"google-1","name":"Ada","given_name":"Ada","family_name":"Lovelace","email":"ada@demo.com","picture":"https://cdn.demo.com/h.png"}
                """);

        var claims = await RunAsync<GoogleAuthenticationOptions>(
            new OAuthProviderConfig { Name = "google", ClientId = "google-client", ClientSecret = "google-secret" },
            handler);

        Assert.Equal("google-1", OAuthTestHost.ReadClaim(claims, ClaimTypes.NameIdentifier));
        Assert.Equal("Ada", OAuthTestHost.ReadClaim(claims, ClaimTypes.Name));
        Assert.Equal("ada@demo.com", OAuthTestHost.ReadClaim(claims, ClaimTypes.Email));
        Assert.Equal("https://cdn.demo.com/h.png", OAuthTestHost.ReadClaim(claims, OAuthOptions.AvatarClaimType));
        Assert.Equal("at", handler.RequestFor("/oauth2/v3/userinfo").Headers.Authorization!.Parameter);
    }

    /// <summary>
    /// 用户信息接口返回非 JSON 时应中断回调
    /// </summary>
    [Fact]
    public async Task NonJsonResponse_Aborts()
    {
        var handler = new StubHttpMessageHandler()
            .Respond("/token", """{"access_token":"at","token_type":"Bearer"}""")
            .Respond("/oauth2/v3/userinfo", "<html>service unavailable</html>");

        await Assert.ThrowsAsync<AuthenticationFailureException>(() => RunAsync<GoogleAuthenticationOptions>(
            new OAuthProviderConfig { Name = "google", ClientId = "google-client", ClientSecret = "google-secret" },
            handler));
    }

    private static OAuthProviderConfig CreateWorkWeixin()
    {
        return new OAuthProviderConfig
        {
            Name = "wecom",
            Provider = OAuthProviderNames.WeCom,
            ClientId = "corp-1",
            ClientSecret = "app-secret",
            AgentId = "1000002"
        };
    }

    private static async Task<string> RunAsync<TOptions>(OAuthProviderConfig provider, StubHttpMessageHandler handler)
        where TOptions : XiHanOAuthProviderOptions
    {
        await using var host = await OAuthTestHost.StartAsync(
            OAuthConfigurationBuilder.Build(provider),
            services => services.PostConfigure<TOptions>(
                provider.Name,
                options => options.Backchannel = new HttpClient(handler)));

        var (location, challenge) = await host.ChallengeAsync(provider.Name);
        var query = OAuthTestHost.ParseQuery(location);

        // 微信系账号授权把状态串挪进了回调地址，回调要按它约定的参数名带回
        var usesShortState = query.TryGetValue("state", out var state) && state == "_oauthstate";
        var callback = await host.CallbackAsync(
            $"/signin-{provider.Name}",
            "code-1",
            usesShortState ? ExtractShortState(query["redirect_uri"]) : state!,
            challenge,
            usesShortState ? "_oauthstate" : "state");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/done", callback.Headers.Location!.ToString());

        return await host.GetClaimsAsync(callback);
    }

    private static string ExtractShortState(string redirectUri)
    {
        return OAuthTestHost.ParseQuery(new Uri(redirectUri))["_oauthstate"];
    }
}
