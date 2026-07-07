#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAuthenticationServiceCollectionExtensions
// Guid:926b757a-137d-4daf-b2f9-2395130e94c4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.OAuth;
using XiHan.Framework.Authentication.OneTimeCode;
using XiHan.Framework.Authentication.Otp;
using XiHan.Framework.Security.Password;
using XiHan.Framework.Authentication.Users;

namespace XiHan.Framework.Authentication.Extensions.DependencyInjection;

/// <summary>
/// 曦寒认证服务集合扩展
/// </summary>
public static class XiHanAuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒认证服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // 密码哈希/策略选项与 IPasswordHasher 已下沉至 XiHan.Framework.Security 自注册
        // （类型归属 Security；Authentication [DependsOn Security] 保证其先加载），此处不再重复登记。

        // 配置 JWT 服务
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // 配置 OTP 服务
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));

        // 注册刷新令牌存储
        services.TryAddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
        // 注册 JWT 服务
        services.TryAddSingleton<IJwtTokenService, JwtTokenService>();
        // 注册 OTP 服务
        services.TryAddSingleton<IOtpService, OtpService>();
        // 注册一次性验证码服务（分布式缓存后端：邮箱/短信验证码等签发与一次性消费）
        services.TryAddSingleton<IOneTimeCodeService, DistributedOneTimeCodeService>();
        // 注册用户存储
        services.TryAddScoped<IUserStore, DefaultUserStore>();
        // 注册认证服务接口
        services.TryAddScoped<IAuthenticationService, DefaultAuthenticationService>();

        // 始终绑定 OAuthOptions（即使未启用，保证 IOptions<OAuthOptions> 可注入）
        // 使用显式赋值而非 section.Bind()，避免 List<T> 属性被多次追加导致重复
        var oauthSection = configuration.GetSection(OAuthOptions.SectionName);
        services.Configure<OAuthOptions>(options =>
        {
            options.Enabled = oauthSection.GetValue<bool>(nameof(OAuthOptions.Enabled));
            var frontendUrl = oauthSection.GetValue<string>(nameof(OAuthOptions.FrontendCallbackUrl));
            if (!string.IsNullOrEmpty(frontendUrl))
            {
                options.FrontendCallbackUrl = frontendUrl;
            }
            options.Providers = oauthSection.GetSection(nameof(OAuthOptions.Providers))
                .Get<List<OAuthProviderConfig>>() ?? [];
        });

        // 注册 OAuth 第三方登录
        services.AddXiHanOAuth(configuration);

        return services;
    }
}
