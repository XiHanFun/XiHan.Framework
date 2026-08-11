// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace XiHan.Framework.Authentication.Oidc;

/// <summary>
/// OpenID Connect 服务注册扩展
/// </summary>
public static class XiHanOidcServiceCollectionExtensions
{
    /// <summary>
    /// 注册 OIDC 签名密钥提供器与 id_token 签发服务。
    /// </summary>
    /// <remarks>
    /// 密钥提供器为单例：签名与 JWKS 必须同源，且 kid 要在进程内稳定。
    /// 端点（发现文档 / JWKS / 用户信息）由应用侧映射，因为用户资料声明的来源属于应用。
    /// </remarks>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanOidc(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        _ = services.Configure<OidcOptions>(configuration.GetSection(OidcOptions.SectionName));

        services.TryAddSingleton<IOidcSigningKeyProvider, OidcSigningKeyProvider>();
        services.TryAddSingleton<IIdTokenService, IdTokenService>();

        return services;
    }
}
