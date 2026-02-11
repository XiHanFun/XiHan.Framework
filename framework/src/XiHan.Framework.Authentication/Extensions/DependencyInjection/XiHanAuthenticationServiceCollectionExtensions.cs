#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAuthenticationServiceCollectionExtensions
// Guid:7aecf557-92ae-475f-bbd9-c667564c59a8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/26 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.Otp;
using XiHan.Framework.Authentication.Password;

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
        // 配置密码哈希服务
        services.Configure<PasswordHasherOptions>(configuration.GetSection(PasswordHasherOptions.SectionName));

        // 配置密码策略
        services.Configure<PasswordPolicyOptions>(configuration.GetSection(PasswordPolicyOptions.SectionName));

        // 配置 JWT 服务
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        // 配置 OTP 服务
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));

        // 注册密码哈希服务
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        // 注册 JWT 服务
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        // 注册 OTP 服务
        services.AddSingleton<IOtpService, OtpService>();

        // 注册用户存储
        services.AddScoped<IUserStore, DefaultUserStore>();
        // 注册认证服务接口
        services.AddScoped<IAuthenticationService, DefaultAuthenticationService>();

        return services;
    }
}
