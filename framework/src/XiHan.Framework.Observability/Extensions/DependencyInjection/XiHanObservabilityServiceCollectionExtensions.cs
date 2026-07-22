// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.Observability.Diagnostics;
using XiHan.Framework.Observability.Metrics;
using XiHan.Framework.Observability.Options;
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
        var options = new XiHanObservabilityOptions();
        configuration.GetSection(XiHanObservabilityOptions.SectionName).Bind(options);
        services.Configure<XiHanObservabilityOptions>(configuration.GetSection(XiHanObservabilityOptions.SectionName));

        // 注册健康检查
        services.AddHealthChecks();

        // 注册指标收集服务
        services.AddSingleton<IMetricsCollector, MetricsCollector>();

        // 注册性能监控服务
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();

        // 注册诊断服务
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();

        // 未启用 OpenTelemetry：保持原「装配即孤儿」行为，不引入任何运行时开销
        if (!options.Enabled)
        {
            return services;
        }

        var otel = services.AddOpenTelemetry();

        // Resource：service.name/service.version（+ SDK 默认富化）
        otel.ConfigureResource(resource => resource.AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion));

        // 链路追踪（Tracing）：入站/出站 span 由运行时 instrumentation 自动产出；框架源经 XiHanActivitySources.All 纳入
        if (options.EnableTracing)
        {
            otel.WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(Math.Clamp(options.SamplingRatio, 0d, 1d))))
                    .AddSource(XiHanActivitySources.All)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (options.AdditionalSources.Count > 0)
                {
                    tracing.AddSource([.. options.AdditionalSources]);
                }

                if (options.ConsoleExporter)
                {
                    tracing.AddConsoleExporter();
                }

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(options.OtlpEndpoint));
                }
            });
        }

        // 指标（Metrics）：MetricsCollector 的 Meter 直出 OTel，可导 OTLP/Prometheus
        if (options.EnableMetrics)
        {
            otel.WithMetrics(metrics =>
            {
                metrics.AddMeter(MetricsCollector.MeterName);

                if (options.ConsoleExporter)
                {
                    metrics.AddConsoleExporter();
                }

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(options.OtlpEndpoint));
                }
            });
        }

        // Logging（OTLP logs）：后续增量

        return services;
    }
}
