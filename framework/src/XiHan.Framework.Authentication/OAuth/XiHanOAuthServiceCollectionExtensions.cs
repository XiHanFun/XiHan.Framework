// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Authentication.OAuth.Handlers;
using AspNetOAuthOptions = Microsoft.AspNetCore.Authentication.OAuth.OAuthOptions;

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// OAuth 服务扩展方法
/// </summary>
public static class XiHanOAuthServiceCollectionExtensions
{
    /// <summary>
    /// 外部登录中转使用的登录方案名称
    /// </summary>
    public const string ExternalSignInScheme = "ExternalCookie";

    /// <summary>
    /// 根据配置动态注册 OAuth 提供商
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanOAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var oauthOptions = configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>();
        if (oauthOptions is not { Enabled: true } || oauthOptions.Providers.Count == 0)
        {
            return services;
        }

        services.TryAddScoped<IExternalLoginStore, DefaultExternalLoginStore>();

        // 获取或创建 AuthenticationBuilder
        var authBuilder = GetOrCreateAuthBuilder(services);

        foreach (var provider in oauthOptions.Providers.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.ClientId)))
        {
            RegisterProvider(authBuilder, provider);
        }

        return services;
    }

    private static AuthenticationBuilder GetOrCreateAuthBuilder(IServiceCollection services)
    {
        // 检查是否已调用 AddAuthentication（通过查找 IAuthenticationService 注册）
        var hasAuth = services.Any(d => d.ServiceType == typeof(IAuthenticationSchemeProvider));
        return hasAuth ? new AuthenticationBuilder(services) : services.AddAuthentication();
    }

    private static void RegisterProvider(AuthenticationBuilder builder, OAuthProviderConfig provider)
    {
        switch (provider.ResolveProviderType())
        {
            case OAuthProviderNames.Google:
                Add<GoogleAuthenticationOptions, XiHanOAuthHandler<GoogleAuthenticationOptions>>(builder, provider, "Google", _ => { });
                break;

            case OAuthProviderNames.GitHub:
                Add<GitHubAuthenticationOptions, GitHubAuthenticationHandler>(builder, provider, "GitHub", _ => { });
                break;

            case OAuthProviderNames.Gitee:
                Add<GiteeAuthenticationOptions, GiteeAuthenticationHandler>(builder, provider, "Gitee", _ => { });
                break;

            case OAuthProviderNames.QQ:
                Add<QQAuthenticationOptions, QQAuthenticationHandler>(builder, provider, "QQ", _ => { });
                break;

            case OAuthProviderNames.Weixin or OAuthProviderNames.WeChat:
                Add<WeixinAuthenticationOptions, WeixinAuthenticationHandler>(builder, provider, "微信", options => ConfigureWeixin(options, provider), scopesReplaceDefaults: true);
                break;

            case OAuthProviderNames.WorkWeixin or OAuthProviderNames.WeCom:
                Add<WorkWeixinAuthenticationOptions, WorkWeixinAuthenticationHandler>(builder, provider, "企业微信", options => ConfigureWorkWeixin(options, provider), scopesReplaceDefaults: true);
                break;

            case OAuthProviderNames.Feishu or OAuthProviderNames.Lark:
                Add<FeishuAuthenticationOptions, FeishuAuthenticationHandler>(builder, provider, "飞书", options => ConfigureFeishu(options, provider));
                break;

            case OAuthProviderNames.DingTalk:
                Add<DingTalkAuthenticationOptions, DingTalkAuthenticationHandler>(builder, provider, "钉钉", options => ConfigureDingTalk(options, provider));
                break;

            default:
                // 未知提供商，跳过
                break;
        }
    }

    private static void Add<TOptions, THandler>(
        AuthenticationBuilder builder,
        OAuthProviderConfig provider,
        string defaultDisplayName,
        Action<TOptions> configureProvider,
        bool scopesReplaceDefaults = false)
        where TOptions : XiHanOAuthProviderOptions, new()
        where THandler : OAuthHandler<TOptions>
    {
        builder.AddOAuth<TOptions, THandler>(provider.Name, provider.DisplayName ?? defaultDisplayName, options =>
        {
            options.ClientId = provider.ClientId;
            options.ClientSecret = provider.ClientSecret;
            options.CallbackPath = provider.CallbackPath ?? $"/signin-{provider.Name}";
            options.SignInScheme = ExternalSignInScheme;

            configureProvider(options);

            // 显式配置的授权页地址排在按登录方式推导之后，始终以配置为准
            if (!string.IsNullOrWhiteSpace(provider.AuthorizationEndpoint))
            {
                options.AuthorizationEndpoint = provider.AuthorizationEndpoint;
            }

            ApplyScopes(options, provider, scopesReplaceDefaults);

            foreach (var parameter in provider.AuthorizationParameters)
            {
                options.AdditionalAuthorizationParameters[parameter.Key] = parameter.Value;
            }
        });
    }

    private static void ConfigureWeixin(WeixinAuthenticationOptions options, OAuthProviderConfig provider)
    {
        if (provider.Mode != OAuthLoginMode.Account)
        {
            return;
        }

        // 账号授权走公众号网页授权页，凭据须填公众号的 AppId 与 AppSecret
        options.AuthorizationEndpoint = OAuthProviderEndpoints.Weixin.AccountAuthorization;
        ReplaceScopes(options, OAuthProviderEndpoints.Weixin.AccountScope);
    }

    private static void ConfigureWorkWeixin(WorkWeixinAuthenticationOptions options, OAuthProviderConfig provider)
    {
        options.AgentId = provider.AgentId ?? string.Empty;
        options.LoadMemberProfile = provider.LoadMemberProfile;

        if (provider.Mode != OAuthLoginMode.Account)
        {
            return;
        }

        // 应用内网页授权要显式申请权限范围，扫码页则不带 scope
        options.AuthorizationEndpoint = OAuthProviderEndpoints.WorkWeixin.AccountAuthorization;
        ReplaceScopes(options, OAuthProviderEndpoints.WorkWeixin.AccountScope);
    }

    private static void ConfigureFeishu(FeishuAuthenticationOptions options, OAuthProviderConfig provider)
    {
        if (provider.Mode != OAuthLoginMode.Account)
        {
            return;
        }

        // 两套端点的授权码不可交叉换取，三个地址成套替换
        options.AuthorizationEndpoint = OAuthProviderEndpoints.Feishu.AccountAuthorization;
        options.TokenEndpoint = OAuthProviderEndpoints.Feishu.AccountToken;
        options.UserInformationEndpoint = OAuthProviderEndpoints.Feishu.AccountUserInformation;
        options.UseFormTokenRequest = false;
    }

    private static void ConfigureDingTalk(DingTalkAuthenticationOptions options, OAuthProviderConfig provider)
    {
        options.CorpId = provider.CorpId;

        if (provider.Mode == OAuthLoginMode.Account)
        {
            options.AuthorizationEndpoint = OAuthProviderEndpoints.DingTalk.AccountAuthorization;
        }
    }

    private static void ApplyScopes(AspNetOAuthOptions options, OAuthProviderConfig provider, bool replaceDefaults)
    {
        if (provider.Scopes.Length == 0)
        {
            return;
        }

        // 微信系两种登录方式的权限范围互斥，配置值必须整体替换按登录方式推导出的范围；
        // 其余提供商是追加，配置里只写增量就不会把提供商默认值（如 Gitee 的 emails）挤掉
        if (replaceDefaults)
        {
            options.Scope.Clear();
        }

        foreach (var scope in provider.Scopes)
        {
            if (!options.Scope.Contains(scope))
            {
                options.Scope.Add(scope);
            }
        }
    }

    private static void ReplaceScopes(AspNetOAuthOptions options, string scope)
    {
        options.Scope.Clear();
        options.Scope.Add(scope);
    }
}
