#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GatewayServiceCollectionExtensions
// Guid:8f9a0b1c-2d3e-4f5a-6b7c-8d9e0f1a2b3c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/22 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
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
