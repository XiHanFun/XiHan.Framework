// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Observability.Diagnostics;
using XiHan.Framework.Observability.Metrics;
using XiHan.Framework.Observability.Options;
using XiHan.Framework.Observability.Performance;

namespace XiHan.Framework.Observability.Tests;

/// <summary>
/// 可观测性模块测试
/// </summary>
/// <remarks>
/// 模块本身只做一件事：从服务集合里取配置再转调 AddXiHanObservability。
/// 因此测两侧——配置存在时三件套确实进容器；配置缺失时按框架约定抛 XiHanException 而不是静默降级。
/// </remarks>
public class XiHanObservabilityModuleTests
{
    /// <summary>
    /// 模块继承自框架模块基类
    /// </summary>
    [Fact]
    public void XiHanObservabilityModule_Always_DerivesFromXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanObservabilityModule());
    }

    /// <summary>
    /// 服务集合中存在配置时完成可观测性服务装配
    /// </summary>
    [Fact]
    public void ConfigureServices_WithConfigurationInServices_RegistersObservabilityServices()
    {
        var services = CreateServicesWithConfiguration();
        var module = new XiHanObservabilityModule();

        module.ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<MetricsCollector>(provider.GetRequiredService<IMetricsCollector>());
        Assert.IsType<PerformanceMonitor>(provider.GetRequiredService<IPerformanceMonitor>());
        Assert.IsType<DiagnosticsService>(provider.GetRequiredService<IDiagnosticsService>());
    }

    /// <summary>
    /// 模块装配后配置节的值绑定到选项上
    /// </summary>
    [Fact]
    public void ConfigureServices_WithObservabilitySection_BindsOptions()
    {
        var services = CreateServicesWithConfiguration(new Dictionary<string, string?>
        {
            ["XiHan:Observability:ServiceName"] = "module-service",
            ["XiHan:Observability:EnableMetrics"] = "true"
        });
        var module = new XiHanObservabilityModule();

        module.ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanObservabilityOptions>>().Value;

        Assert.Equal("module-service", options.ServiceName);
        Assert.True(options.EnableMetrics);
        Assert.False(options.Enabled);
    }

    /// <summary>
    /// 异步入口等价于同步入口，同样完成装配
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_WithConfigurationInServices_RegistersSameServices()
    {
        var services = CreateServicesWithConfiguration();
        var module = new XiHanObservabilityModule();

        await module.ConfigureServicesAsync(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IMetricsCollector>());
        Assert.NotNull(provider.GetService<IPerformanceMonitor>());
        Assert.NotNull(provider.GetService<IDiagnosticsService>());
    }

    /// <summary>
    /// 服务集合中没有配置时按框架约定抛出曦寒异常
    /// </summary>
    [Fact]
    public void ConfigureServices_WithoutConfiguration_ThrowsXiHanException()
    {
        var module = new XiHanObservabilityModule();
        var context = new ServiceConfigurationContext(new ServiceCollection());

        Assert.Throws<XiHanException>(() => module.ConfigureServices(context));
    }

    /// <summary>
    /// 模块不改写上下文里的服务集合引用
    /// </summary>
    [Fact]
    public void ConfigureServices_Always_KeepsContextServiceCollectionInstance()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);

        new XiHanObservabilityModule().ConfigureServices(context);

        Assert.Same(services, context.Services);
    }

    /// <summary>
    /// 构造一个已放入配置与日志基础设施的服务集合
    /// </summary>
    private static IServiceCollection CreateServicesWithConfiguration(Dictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? [])
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        return services;
    }
}
