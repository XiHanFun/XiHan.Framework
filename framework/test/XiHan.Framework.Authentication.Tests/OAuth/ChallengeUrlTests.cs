// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Authentication.OAuth;
using XiHan.Framework.Authentication.Tests.OAuth.Infrastructure;

namespace XiHan.Framework.Authentication.Tests.OAuth;

/// <summary>
/// 各提供商授权地址的测试
/// </summary>
/// <remarks>
/// 每个用例都真正走一次挑战，断言的是最终发给浏览器的重定向地址，
/// 覆盖八家的授权端点与权限范围、登录方式对微信系与钉钉的影响，以及提供商类型别名与配置覆盖。
/// </remarks>
public class ChallengeUrlTests
{
    /// <summary>
    /// Google 应指向 Google 授权页并启用 PKCE
    /// </summary>
    [Fact]
    public async Task Google_UsesAuthorizationEndpointWithPkce()
    {
        var (location, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "google",
            ClientId = "google-client",
            ClientSecret = "google-secret"
        });

        Assert.Equal("https://accounts.google.com/o/oauth2/v2/auth", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("google-client", query["client_id"]);
        Assert.Equal("openid profile email", query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.NotEmpty(query["code_challenge"]);
    }

    /// <summary>
    /// GitHub 应指向 GitHub 授权页
    /// </summary>
    [Fact]
    public async Task GitHub_UsesAuthorizationEndpoint()
    {
        var (location, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "github",
            ClientId = "github-client",
            ClientSecret = "github-secret",
            Scopes = ["user:email"]
        });

        Assert.Equal("https://github.com/login/oauth/authorize", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("github-client", query["client_id"]);
        Assert.Equal("user:email", query["scope"]);
    }

    /// <summary>
    /// Gitee 应指向 Gitee 授权页并带上默认权限范围
    /// </summary>
    [Fact]
    public async Task Gitee_UsesAuthorizationEndpoint()
    {
        var (location, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "gitee",
            ClientId = "gitee-client",
            ClientSecret = "gitee-secret"
        });

        Assert.Equal("https://gitee.com/oauth/authorize", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("user_info emails", query["scope"]);
    }

    /// <summary>
    /// QQ 应指向 QQ 授权页
    /// </summary>
    [Fact]
    public async Task QQ_UsesAuthorizationEndpoint()
    {
        var (location, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "qq",
            ClientId = "qq-client",
            ClientSecret = "qq-secret"
        });

        Assert.Equal("https://graph.qq.com/oauth2.0/authorize", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("get_user_info", query["scope"]);
    }

    /// <summary>
    /// 微信扫码登录应指向开放平台二维码页并带微信跳转锚点
    /// </summary>
    [Fact]
    public async Task Weixin_QrCode_UsesQrConnectEndpoint()
    {
        var (location, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "wechat-qr",
            Provider = OAuthProviderNames.WeChat,
            Mode = OAuthLoginMode.QrCode,
            ClientId = "wx-open",
            ClientSecret = "wx-open-secret"
        });

        Assert.Equal("https://open.weixin.qq.com/connect/qrconnect", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("wx-open", query["appid"]);
        Assert.Equal("snsapi_login", query["scope"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("wechat_redirect", location.Fragment.TrimStart('#'));
        Assert.EndsWith("/signin-wechat-qr", query["redirect_uri"], StringComparison.Ordinal);

        // 扫码页不限制 state 长度，状态串直接放在 state 上
        Assert.DoesNotContain("_oauthstate", query["redirect_uri"], StringComparison.Ordinal);
    }

    /// <summary>
    /// 微信账号授权应改指公众号网页授权页，并把状态串挪进回调地址
    /// </summary>
    [Fact]
    public async Task Weixin_Account_UsesOfficialAccountEndpointWithShortState()
    {
        var (location, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "wechat-mp",
            Provider = OAuthProviderNames.WeChat,
            Mode = OAuthLoginMode.Account,
            ClientId = "wx-mp",
            ClientSecret = "wx-mp-secret"
        });

        Assert.Equal("https://open.weixin.qq.com/connect/oauth2/authorize", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("wx-mp", query["appid"]);
        Assert.Equal("snsapi_userinfo", query["scope"]);
        Assert.Equal("wechat_redirect", location.Fragment.TrimStart('#'));
        Assert.Equal("_oauthstate", query["state"]);
        Assert.Contains("_oauthstate=", query["redirect_uri"], StringComparison.Ordinal);
    }

    /// <summary>
    /// 企业微信扫码登录应指向企业微信登录站点并带上 AgentId
    /// </summary>
    [Fact]
    public async Task WorkWeixin_QrCode_UsesWorkLoginSite()
    {
        var (location, query) = await ChallengeAsync(CreateWorkWeixin("wecom-qr", OAuthLoginMode.QrCode));

        Assert.Equal("https://login.work.weixin.qq.com/wwlogin/sso/login", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("CorpApp", query["login_type"]);
        Assert.Equal("corp-1", query["appid"]);
        Assert.Equal("1000002", query["agentid"]);
        Assert.Empty(location.Fragment);
    }

    /// <summary>
    /// 企业微信账号授权应指向应用内网页授权页，并补齐扫码页没有的参数
    /// </summary>
    [Fact]
    public async Task WorkWeixin_Account_UsesAgentEndpointWithScopeAndFragment()
    {
        var (location, query) = await ChallengeAsync(CreateWorkWeixin("wecom-app", OAuthLoginMode.Account));

        Assert.Equal("https://open.weixin.qq.com/connect/oauth2/authorize", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("corp-1", query["appid"]);
        Assert.Equal("1000002", query["agentid"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("snsapi_privateinfo", query["scope"]);
        Assert.Equal("wechat_redirect", location.Fragment.TrimStart('#'));
        Assert.Equal("_oauthstate", query["state"]);
    }

    /// <summary>
    /// 飞书两种登录方式应指向各自的授权页
    /// </summary>
    /// <param name="mode">登录方式</param>
    /// <param name="expectedEndpoint">期望的授权端点</param>
    [Theory]
    [InlineData(OAuthLoginMode.QrCode, "https://passport.feishu.cn/suite/passport/oauth/authorize")]
    [InlineData(OAuthLoginMode.Account, "https://accounts.feishu.cn/open-apis/authen/v1/authorize")]
    public async Task Feishu_UsesEndpointPerMode(OAuthLoginMode mode, string expectedEndpoint)
    {
        var (location, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "feishu",
            Mode = mode,
            ClientId = "cli_app",
            ClientSecret = "app-secret"
        });

        Assert.Equal(expectedEndpoint, location.GetLeftPart(UriPartial.Path));
        Assert.Equal("cli_app", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.NotEmpty(query["state"]);
    }

    /// <summary>
    /// 钉钉两种登录方式应指向各自的授权页
    /// </summary>
    /// <param name="mode">登录方式</param>
    /// <param name="expectedEndpoint">期望的授权端点</param>
    [Theory]
    [InlineData(OAuthLoginMode.QrCode, "https://login.dingtalk.com/oauth2/challenge.htm")]
    [InlineData(OAuthLoginMode.Account, "https://login.dingtalk.com/oauth2/auth")]
    public async Task DingTalk_UsesEndpointPerMode(OAuthLoginMode mode, string expectedEndpoint)
    {
        var (location, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "dingtalk",
            Mode = mode,
            ClientId = "app-key",
            ClientSecret = "app-secret",
            CorpId = "corp-1"
        });

        Assert.Equal(expectedEndpoint, location.GetLeftPart(UriPartial.Path));
        Assert.Equal("app-key", query["client_id"]);
        Assert.Equal("openid", query["scope"]);
        Assert.Equal("consent", query["prompt"]);
        Assert.Equal("corp-1", query["corpId"]);
    }

    /// <summary>
    /// 未配置 CorpId 时钉钉授权地址不应带该参数
    /// </summary>
    [Fact]
    public async Task DingTalk_WithoutCorpId_OmitsCorpIdParameter()
    {
        var (_, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "dingtalk",
            ClientId = "app-key",
            ClientSecret = "app-secret"
        });

        Assert.DoesNotContain("corpId", query.Keys, StringComparer.Ordinal);
    }

    /// <summary>
    /// 提供商类型别名应等价于正名
    /// </summary>
    /// <param name="providerType">提供商类型</param>
    /// <param name="expectedEndpoint">期望的授权端点</param>
    [Theory]
    [InlineData(OAuthProviderNames.Weixin, "https://open.weixin.qq.com/connect/qrconnect")]
    [InlineData(OAuthProviderNames.WeChat, "https://open.weixin.qq.com/connect/qrconnect")]
    [InlineData(OAuthProviderNames.WorkWeixin, "https://login.work.weixin.qq.com/wwlogin/sso/login")]
    [InlineData(OAuthProviderNames.WeCom, "https://login.work.weixin.qq.com/wwlogin/sso/login")]
    [InlineData(OAuthProviderNames.Feishu, "https://passport.feishu.cn/suite/passport/oauth/authorize")]
    [InlineData(OAuthProviderNames.Lark, "https://passport.feishu.cn/suite/passport/oauth/authorize")]
    public async Task ProviderAliases_ResolveToSameProvider(string providerType, string expectedEndpoint)
    {
        var (location, _) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "provider",
            Provider = providerType,
            ClientId = "client",
            ClientSecret = "secret",
            AgentId = "1000002"
        });

        Assert.Equal(expectedEndpoint, location.GetLeftPart(UriPartial.Path));
    }

    /// <summary>
    /// 显式配置的权限范围应整体覆盖提供商默认值
    /// </summary>
    [Fact]
    public async Task ExplicitScopes_ReplaceProviderDefaults()
    {
        var (_, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "gitee",
            ClientId = "gitee-client",
            ClientSecret = "gitee-secret",
            Scopes = ["user_info"]
        });

        Assert.Equal("user_info", query["scope"]);
    }

    /// <summary>
    /// 显式配置的授权页地址应覆盖按登录方式推导的默认值
    /// </summary>
    [Fact]
    public async Task AuthorizationEndpointOverride_TakesPrecedence()
    {
        var (location, _) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "wecom-qr",
            Provider = OAuthProviderNames.WeCom,
            Mode = OAuthLoginMode.QrCode,
            ClientId = "corp-1",
            ClientSecret = "app-secret",
            AgentId = "1000002",
            AuthorizationEndpoint = "https://open.work.weixin.qq.com/wwopen/sso/qrConnect"
        });

        Assert.Equal("https://open.work.weixin.qq.com/wwopen/sso/qrConnect", location.GetLeftPart(UriPartial.Path));
    }

    /// <summary>
    /// 额外授权参数应原样出现在授权地址上
    /// </summary>
    [Fact]
    public async Task AuthorizationParameters_AppearInUrl()
    {
        var (_, query) = await ChallengeAsync(new OAuthProviderConfig
        {
            Name = "google",
            ClientId = "google-client",
            ClientSecret = "google-secret",
            AuthorizationParameters = new Dictionary<string, string> { ["access_type"] = "offline" }
        });

        Assert.Equal("offline", query["access_type"]);
    }

    /// <summary>
    /// 同一提供商可以用两条配置注册成两个方案，各自走自己的登录方式
    /// </summary>
    [Fact]
    public async Task SameProvider_TwoSchemes_RegisterIndependently()
    {
        await using var host = await OAuthTestHost.StartAsync(OAuthConfigurationBuilder.Build(
            new OAuthProviderConfig
            {
                Name = "wechat-qr",
                Provider = OAuthProviderNames.Weixin,
                Mode = OAuthLoginMode.QrCode,
                ClientId = "wx-open",
                ClientSecret = "wx-open-secret"
            },
            new OAuthProviderConfig
            {
                Name = "wechat-mp",
                Provider = OAuthProviderNames.Weixin,
                Mode = OAuthLoginMode.Account,
                ClientId = "wx-mp",
                ClientSecret = "wx-mp-secret"
            }));

        var (qrCode, _) = await host.ChallengeAsync("wechat-qr");
        var (account, _) = await host.ChallengeAsync("wechat-mp");

        Assert.Equal("https://open.weixin.qq.com/connect/qrconnect", qrCode.GetLeftPart(UriPartial.Path));
        Assert.Equal("wx-open", OAuthTestHost.ParseQuery(qrCode)["appid"]);
        Assert.Equal("https://open.weixin.qq.com/connect/oauth2/authorize", account.GetLeftPart(UriPartial.Path));
        Assert.Equal("wx-mp", OAuthTestHost.ParseQuery(account)["appid"]);
    }

    /// <summary>
    /// 未知提供商类型应被跳过而不是注册出一个坏方案
    /// </summary>
    [Fact]
    public async Task UnknownProviderType_IsSkipped()
    {
        await using var host = await OAuthTestHost.StartAsync(OAuthConfigurationBuilder.Build(
            new OAuthProviderConfig
            {
                Name = "somewhere",
                ClientId = "id",
                ClientSecret = "secret"
            }));

        var schemes = await host.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();

        Assert.DoesNotContain(schemes, scheme => scheme.Name == "somewhere");
    }

    private static async Task<(Uri Location, Dictionary<string, string> Query)> ChallengeAsync(OAuthProviderConfig provider)
    {
        await using var host = await OAuthTestHost.StartAsync(OAuthConfigurationBuilder.Build(provider));
        var (location, _) = await host.ChallengeAsync(provider.Name);
        return (location, OAuthTestHost.ParseQuery(location));
    }

    private static OAuthProviderConfig CreateWorkWeixin(string name, OAuthLoginMode mode)
    {
        return new OAuthProviderConfig
        {
            Name = name,
            Provider = OAuthProviderNames.WeCom,
            Mode = mode,
            ClientId = "corp-1",
            ClientSecret = "app-secret",
            AgentId = "1000002"
        };
    }
}
