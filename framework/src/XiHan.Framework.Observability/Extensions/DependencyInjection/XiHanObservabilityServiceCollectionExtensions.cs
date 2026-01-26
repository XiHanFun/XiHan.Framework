#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObservabilityServiceCollectionExtensions
// Guid:a1b2c3d4-e5f6-47a8-b9c0-d1e2f3a4b5c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/26
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Observability.Diagnostics;
using XiHan.Framework.Observability.Metrics;
using XiHan.Framework.Observability.Performance;

namespace XiHan.Framework.Observability.Extensions.DependencyInjection;

/// <summary>
/// 曦寒可观测性服务集合扩展
/// </summary>
public static class XiHanObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// 添加曦寒可观测性服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddXiHanObservability(this IServiceCollection services, IConfiguration configuration)
    {
        // 注册健康检查
        services.AddHealthChecks();

        // 注册指标收集服务
        services.AddSingleton<IMetricsCollector, MetricsCollector>();

        // 注册性能监控服务
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();

        // 注册诊断服务
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();

        return services;
    }
}
