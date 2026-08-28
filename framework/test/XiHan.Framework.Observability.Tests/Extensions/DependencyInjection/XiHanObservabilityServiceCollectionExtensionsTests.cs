// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using XiHan.Framework.Observability.Diagnostics;
using XiHan.Framework.Observability.Extensions.DependencyInjection;
using XiHan.Framework.Observability.Metrics;
using XiHan.Framework.Observability.Options;
using XiHan.Framework.Observability.Performance;

namespace XiHan.Framework.Observability.Tests.Extensions.DependencyInjection;

/// <summary>
/// 可观测性服务注册扩展测试
/// </summary>
/// <remarks>
/// 该扩展的核心契约是「总开关关闭时装配即孤儿」：自研的指标/性能/诊断三件套照常注册，
/// 但一个 OpenTelemetry SDK 的服务都不进容器。用真实 ServiceCollection + 内存配置验证两侧行为。
/// 开启用例刻意把 Tracing/Metrics 都关掉，只验证 SDK 总闸接上，避免把 ASP.NET Core instrumentation 拉进非 Web 测试宿主。
/// </remarks>
public class XiHanObservabilityServiceCollectionExtensionsTests
{
    /// <summary>
    /// 扩展方法返回同一个服务集合实例，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanObservability_Always_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanObservability(BuildConfiguration());

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 指标、性能、诊断三件套按单例注册，且实现类型固定
    /// </summary>
    [Fact]
    public void AddXiHanObservability_Always_RegistersCoreServicesAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddXiHanObservability(BuildConfiguration());

        AssertSingletonRegistration<IMetricsCollector, MetricsCollector>(services);
        AssertSingletonRegistration<IPerformanceMonitor, PerformanceMonitor>(services);
        AssertSingletonRegistration<IDiagnosticsService, DiagnosticsService>(services);
    }

    /// <summary>
    /// 三件套能真正解析出来，且同一容器内是同一个实例
    /// </summary>
    [Fact]
    public void AddXiHanObservability_Always_ResolvesCoreServicesAsSameInstance()
    {
        using var provider = BuildProvider();

        var metrics = provider.GetRequiredService<IMetricsCollector>();
        var performance = provider.GetRequiredService<IPerformanceMonitor>();
        var diagnostics = provider.GetRequiredService<IDiagnosticsService>();

        Assert.IsType<MetricsCollector>(metrics);
        Assert.IsType<PerformanceMonitor>(performance);
        Assert.IsType<DiagnosticsService>(diagnostics);
        Assert.Same(metrics, provider.GetRequiredService<IMetricsCollector>());
        Assert.Same(performance, provider.GetRequiredService<IPerformanceMonitor>());
        Assert.Same(diagnostics, provider.GetRequiredService<IDiagnosticsService>());
    }

    /// <summary>
    /// 健康检查基础设施随扩展一起注册
    /// </summary>
    [Fact]
    public void AddXiHanObservability_Always_RegistersHealthCheckService()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetService<HealthCheckService>());
    }

    /// <summary>
    /// 配置节的值绑定到选项上并可从容器解析
    /// </summary>
    [Fact]
    public void AddXiHanObservability_Always_BindsOptionsFromSection()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["XiHan:Observability:ServiceName"] = "bound-service",
            ["XiHan:Observability:ServiceVersion"] = "9.9.9",
            ["XiHan:Observability:SamplingRatio"] = "0.4"
        });

        var options = provider.GetRequiredService<IOptions<XiHanObservabilityOptions>>().Value;

        Assert.Equal("bound-service", options.ServiceName);
        Assert.Equal("9.9.9", options.ServiceVersion);
        Assert.Equal(0.4d, options.SamplingRatio);
    }

    /// <summary>
    /// 配置为空时选项保持类型默认值
    /// </summary>
    [Fact]
    public void AddXiHanObservability_WithEmptyConfiguration_KeepsOptionDefaults()
    {
        using var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<XiHanObservabilityOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Equal("XiHan.App", options.ServiceName);
        Assert.True(options.EnableTracing);
        Assert.False(options.EnableMetrics);
        Assert.Equal(1.0d, options.SamplingRatio);
    }

    /// <summary>
    /// 总开关关闭时不引入任何 OpenTelemetry 服务，保持装配即孤儿
    /// </summary>
    [Fact]
    public void AddXiHanObservability_WhenDisabled_RegistersNoOpenTelemetryService()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddXiHanObservability(BuildConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:Observability:Enabled"] = "false"
        }));

        Assert.DoesNotContain(services, descriptor => IsOpenTelemetryDescriptor(descriptor));
    }

    /// <summary>
    /// 配置里完全没写总开关时同样按关闭处理
    /// </summary>
    [Fact]
    public void AddXiHanObservability_WithoutEnabledKey_TreatsAsDisabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddXiHanObservability(BuildConfiguration());

        Assert.DoesNotContain(services, descriptor => IsOpenTelemetryDescriptor(descriptor));
    }

    /// <summary>
    /// 总开关打开时接上 OpenTelemetry SDK 总闸
    /// </summary>
    /// <remarks>
    /// 链路追踪与指标都关掉，避免把 ASP.NET Core / HttpClient instrumentation 拉进非 Web 测试宿主；
    /// 这里只验证「总闸接上了」以及注册数量确实增加，不去断言具体的 SDK 内部服务名。
    /// </remarks>
    [Fact]
    public void AddXiHanObservability_WhenEnabled_RegistersOpenTelemetryServices()
    {
        var disabled = new ServiceCollection();
        disabled.AddLogging();
        disabled.AddXiHanObservability(BuildConfiguration(EnabledSettings("false")));

        var enabled = new ServiceCollection();
        enabled.AddLogging();
        enabled.AddXiHanObservability(BuildConfiguration(EnabledSettings("true")));

        Assert.True(enabled.Count > disabled.Count);
        Assert.Contains(enabled, descriptor => IsOpenTelemetryDescriptor(descriptor));
    }

    /// <summary>
    /// 总开关打开时自研三件套仍照常注册与解析
    /// </summary>
    [Fact]
    public void AddXiHanObservability_WhenEnabled_StillResolvesCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanObservability(BuildConfiguration(EnabledSettings("true")));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<MetricsCollector>(provider.GetRequiredService<IMetricsCollector>());
        Assert.IsType<PerformanceMonitor>(provider.GetRequiredService<IPerformanceMonitor>());
        Assert.IsType<DiagnosticsService>(provider.GetRequiredService<IDiagnosticsService>());
        Assert.True(provider.GetRequiredService<IOptions<XiHanObservabilityOptions>>().Value.Enabled);
    }

    /// <summary>
    /// 重复调用不会把三件套注册成多份
    /// </summary>
    /// <remarks>
    /// 扩展用的是 AddSingleton 而非 TryAdd，重复调用会追加描述符；
    /// 这里锁定「最后解析出来的仍是唯一实例、类型正确」，把重复注册的可见影响面钉死。
    /// </remarks>
    [Fact]
    public void AddXiHanObservability_CalledTwice_StillResolvesSingleInstance()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanObservability(BuildConfiguration());
        services.AddXiHanObservability(BuildConfiguration());

        using var provider = services.BuildServiceProvider();
        var metrics = provider.GetRequiredService<IMetricsCollector>();

        Assert.IsType<MetricsCollector>(metrics);
        Assert.Same(metrics, provider.GetRequiredService<IMetricsCollector>());
    }

    /// <summary>
    /// 断言某个服务按单例注册且实现类型固定
    /// </summary>
    private static void AssertSingletonRegistration<TService, TImplementation>(IServiceCollection services)
    {
        var descriptor = services.Single(d => d.ServiceType == typeof(TService));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
    }

    /// <summary>
    /// 判断服务描述符是否来自 OpenTelemetry SDK
    /// </summary>
    private static bool IsOpenTelemetryDescriptor(ServiceDescriptor descriptor)
    {
        return IsOpenTelemetryType(descriptor.ServiceType) || IsOpenTelemetryType(descriptor.ImplementationType);
    }

    /// <summary>
    /// 判断类型是否位于 OpenTelemetry 命名空间下
    /// </summary>
    private static bool IsOpenTelemetryType(Type? type)
    {
        return type?.FullName?.StartsWith("OpenTelemetry.", StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// 只切换总开关、关闭链路与指标的最小配置
    /// </summary>
    private static Dictionary<string, string?> EnabledSettings(string enabled)
    {
        return new Dictionary<string, string?>
        {
            ["XiHan:Observability:Enabled"] = enabled,
            ["XiHan:Observability:EnableTracing"] = "false",
            ["XiHan:Observability:EnableMetrics"] = "false"
        };
    }

    /// <summary>
    /// 构造内存配置
    /// </summary>
    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? settings = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? [])
            .Build();
    }

    /// <summary>
    /// 构造已完成注册的容器
    /// </summary>
    private static ServiceProvider BuildProvider(Dictionary<string, string?>? settings = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddXiHanObservability(BuildConfiguration(settings));
        return services.BuildServiceProvider();
    }
}
