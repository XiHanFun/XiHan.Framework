#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanAuthenticationModule
// Guid:7aecf557-92ae-475f-bbd9-c667564c59a8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:49:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.Otp;
using XiHan.Framework.Authentication.Password;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Authentication;

/// <summary>
/// 曦寒框架认证模块
/// </summary>
public class XiHanAuthenticationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 配置密码哈希服务
        services.Configure<PasswordHasherOptions>(options =>
        {
            // 使用默认配置，用户可以通过配置文件覆盖
        });
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // 配置密码策略
        services.Configure<PasswordPolicy>(options =>
        {
            // 使用默认配置，用户可以通过配置文件覆盖
        });

        // 配置 JWT 服务
        services.Configure<JwtOptions>(options =>
        {
            // 使用默认配置，用户必须通过配置文件提供密钥等信息
        });
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // 配置 OTP 服务
        services.Configure<OtpOptions>(options =>
        {
            // 使用默认配置，用户可以通过配置文件覆盖
        });
        services.AddSingleton<IOtpService, OtpService>();

        // 注册认证服务接口
        // 注意: IAuthenticationService 需要用户自己实现并注册
        // services.AddScoped<IAuthenticationService, YourAuthenticationServiceImplementation>();
    }
}
