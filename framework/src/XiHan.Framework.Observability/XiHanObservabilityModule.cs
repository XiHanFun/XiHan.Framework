#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanObservabilityModule
// Guid:a1b2c3d4-e5f6-47a8-b9c0-d1e2f3a4b5c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/26 3:58:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Observability.Diagnostics;
using XiHan.Framework.Observability.Metrics;
using XiHan.Framework.Observability.Performance;

namespace XiHan.Framework.Observability;

/// <summary>
/// 曦寒可观测性模块
/// </summary>
public class XiHanObservabilityModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        // 注册健康检查
        services.AddHealthChecks();

        // 注册指标收集服务
        services.AddSingleton<IMetricsCollector, MetricsCollector>();

        // 注册性能监控服务
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();

        // 注册诊断服务
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
    }
}
