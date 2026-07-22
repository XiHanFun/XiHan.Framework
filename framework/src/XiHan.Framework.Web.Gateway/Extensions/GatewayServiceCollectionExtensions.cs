// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Web.Gateway.Options;

namespace XiHan.Framework.Web.Gateway.Extensions;

/// <summary>
/// 网关服务集合扩展
/// </summary>
public static class GatewayServiceCollectionExtensions
{
    /// <summary>
    /// 添加网关服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configureOptions">配置选项</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddGateway(
        this IServiceCollection services,
        Action<XiHanGatewayOptions>? configureOptions = null)
    {
        // 配置选项
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        return services;
    }
}
