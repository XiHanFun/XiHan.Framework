#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanOAuthServiceCollectionExtensions
// Guid:a1b2c3d4-5e6f-7890-abcd-ef1234567806
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using AspNet.Security.OAuth.GitHub;
using AspNet.Security.OAuth.QQ;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace XiHan.Framework.Authentication.OAuth;

/// <summary>
/// OAuth 服务扩展方法
/// </summary>
public static class XiHanOAuthServiceCollectionExtensions
{
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
        var name = provider.Name.ToLowerInvariant();

        switch (name)
        {
            case "google":
                builder.AddGoogle(provider.Name, provider.DisplayName ?? "Google", options =>
                {
                    options.ClientId = provider.ClientId;
                    options.ClientSecret = provider.ClientSecret;
                    options.CallbackPath = provider.CallbackPath ?? $"/signin-{provider.Name}";
                    options.SignInScheme = "ExternalCookie";
                    foreach (var scope in provider.Scopes)
                    {
                        options.Scope.Add(scope);
                    }
                });
                break;

            case "github":
                builder.AddGitHub(provider.Name, provider.DisplayName ?? "GitHub", options =>
                {
                    options.ClientId = provider.ClientId;
                    options.ClientSecret = provider.ClientSecret;
                    options.CallbackPath = provider.CallbackPath ?? $"/signin-{provider.Name}";
                    options.SignInScheme = "ExternalCookie";
                    foreach (var scope in provider.Scopes)
                    {
                        options.Scope.Add(scope);
                    }
                });
                break;

            case "qq":
                builder.AddQQ(provider.Name, provider.DisplayName ?? "QQ", options =>
                {
                    options.ClientId = provider.ClientId;
                    options.ClientSecret = provider.ClientSecret;
                    options.CallbackPath = provider.CallbackPath ?? $"/signin-{provider.Name}";
                    options.SignInScheme = "ExternalCookie";
                    foreach (var scope in provider.Scopes)
                    {
                        options.Scope.Add(scope);
                    }
                });
                break;

            default:
                // 未知提供商，跳过
                break;
        }
    }
}
