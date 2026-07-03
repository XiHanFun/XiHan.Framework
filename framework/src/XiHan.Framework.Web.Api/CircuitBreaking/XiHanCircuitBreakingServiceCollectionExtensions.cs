#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCircuitBreakingServiceCollectionExtensions
// Guid:618164cd-f368-41d8-ac1c-93a3bf90f5bb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/02 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace XiHan.Framework.Web.Api.CircuitBreaking;

/// <summary>
/// 入站熔断服务集合扩展
/// </summary>
public static class XiHanCircuitBreakingServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒入站熔断（绑定选项并注册熔断器状态单例；中间件仅在 IsEnabled=true 时由模块接入，未启用时注册无害）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanCircuitBreaking(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<XiHanCircuitBreakingOptions>(configuration.GetSection(XiHanCircuitBreakingOptions.SectionName));
        services.TryAddSingleton<XiHanCircuitBreakerState>();
        return services;
    }
}
