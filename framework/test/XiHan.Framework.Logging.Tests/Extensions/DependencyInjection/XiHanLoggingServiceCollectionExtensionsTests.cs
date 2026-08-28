// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiHan.Framework.Logging.Extensions.DependencyInjection;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Providers;
using XiHan.Framework.Logging.Services;
using XiHan.Framework.Logging.Tests.Fakes;

namespace XiHan.Framework.Logging.Tests.Extensions.DependencyInjection;

/// <summary>
/// 日志服务注册扩展测试
/// </summary>
/// <remarks>
/// 断言一律落在「关心的服务各登记了几条、什么生命周期、什么实现类型」，
/// 不去断言描述符总数——总数会被容器与第三方包的实现细节带着走，与本项目契约无关。
/// 另外只解析选项对象，不解析 ILoggerFactory：后者会真的把 Serilog 管道建起来并往工作目录落盘。
/// </remarks>
public class XiHanLoggingServiceCollectionExtensionsTests
{
    /// <summary>
    /// 文件日志提供器以单例登记进日志提供器集合
    /// </summary>
    [Fact]
    public void AddXiHanFileLogger_RegistersProviderAsSingleton()
    {
        IServiceCollection services = new ServiceCollection();

        new TestLoggingBuilder(services).AddXiHanFileLogger();

        var descriptor = Assert.Single(LoggerProviderDescriptors(services));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(XiHanFileLoggerProvider), descriptor.ImplementationType);
    }

    /// <summary>
    /// 控制台日志提供器以单例登记进日志提供器集合
    /// </summary>
    [Fact]
    public void AddXiHanConsoleLogger_RegistersProviderAsSingleton()
    {
        IServiceCollection services = new ServiceCollection();

        new TestLoggingBuilder(services).AddXiHanConsoleLogger();

        var descriptor = Assert.Single(LoggerProviderDescriptors(services));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(XiHanConsoleLoggerProvider), descriptor.ImplementationType);
    }

    /// <summary>
    /// 重复调用不会把同一个提供器登记两遍
    /// </summary>
    /// <remarks>
    /// 提供器集合是可枚举注册，登记两遍会让每条日志落盘两次，属于典型的重复接入事故。
    /// </remarks>
    [Fact]
    public void AddXiHanFileLogger_CalledTwice_RegistersProviderOnlyOnce()
    {
        IServiceCollection services = new ServiceCollection();
        var builder = new TestLoggingBuilder(services);

        builder.AddXiHanFileLogger();
        builder.AddXiHanFileLogger();

        Assert.Single(LoggerProviderDescriptors(services));
    }

    /// <summary>
    /// 文件与控制台提供器可以并存
    /// </summary>
    [Fact]
    public void AddBothLoggers_RegistersEachProviderType()
    {
        IServiceCollection services = new ServiceCollection();
        var builder = new TestLoggingBuilder(services);

        builder.AddXiHanFileLogger();
        builder.AddXiHanConsoleLogger();

        var implementationTypes = LoggerProviderDescriptors(services)
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();

        Assert.Equal(2, implementationTypes.Count);
        Assert.Contains(typeof(XiHanFileLoggerProvider), implementationTypes);
        Assert.Contains(typeof(XiHanConsoleLoggerProvider), implementationTypes);
    }

    /// <summary>
    /// 两个注册扩展都返回原构建器以支持链式调用
    /// </summary>
    [Fact]
    public void AddLoggerExtensions_ReturnSameBuilderForChaining()
    {
        IServiceCollection services = new ServiceCollection();
        var builder = new TestLoggingBuilder(services);

        Assert.Same(builder, builder.AddXiHanFileLogger());
        Assert.Same(builder, builder.AddXiHanConsoleLogger());
    }

    /// <summary>
    /// 文件日志配置委托最终作用到选项对象上
    /// </summary>
    [Fact]
    public void AddXiHanFileLogger_AppliesConfigureDelegate()
    {
        IServiceCollection services = new ServiceCollection();

        new TestLoggingBuilder(services).AddXiHanFileLogger(options =>
        {
            options.FilePath = "custom/app-{Date}.log";
            options.MinLevel = LogLevel.Debug;
            options.IncludeScopes = false;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanFileLoggerOptions>>().Value;

        Assert.Equal("custom/app-{Date}.log", options.FilePath);
        Assert.Equal(LogLevel.Debug, options.MinLevel);
        Assert.False(options.IncludeScopes);
    }

    /// <summary>
    /// 控制台日志不传配置委托时保留默认选项
    /// </summary>
    [Fact]
    public void AddXiHanConsoleLogger_WithoutConfigureDelegate_KeepsDefaults()
    {
        IServiceCollection services = new ServiceCollection();

        new TestLoggingBuilder(services).AddXiHanConsoleLogger();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanConsoleLoggerOptions>>().Value;

        Assert.Equal(LogLevel.Information, options.MinLevel);
        Assert.True(options.IncludeScopes);
        Assert.False(options.SingleLine);
    }

    /// <summary>
    /// 核心日志服务按约定的生命周期登记
    /// </summary>
    [Theory]
    [InlineData(typeof(IXiHanLoggerFactory), typeof(XiHanLoggerFactory), ServiceLifetime.Singleton)]
    [InlineData(typeof(IXiHanLogger), typeof(XiHanLogger), ServiceLifetime.Transient)]
    [InlineData(typeof(IStructuredLogger), typeof(StructuredLogger), ServiceLifetime.Singleton)]
    [InlineData(typeof(IPerformanceLogger), typeof(PerformanceLogger), ServiceLifetime.Singleton)]
    [InlineData(typeof(ILogContext), typeof(LogContext), ServiceLifetime.Scoped)]
    public void AddXiHanLogging_RegistersCoreServiceWithExpectedLifetime(Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanLogging();

        var descriptor = Assert.Single(services, item => item.ServiceType == serviceType);
        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    /// <summary>
    /// 泛型日志器以开放泛型登记为瞬态
    /// </summary>
    [Fact]
    public void AddXiHanLogging_RegistersOpenGenericLogger()
    {
        IServiceCollection services = new ServiceCollection();

        services.AddXiHanLogging();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IXiHanLogger<>));
        Assert.Equal(typeof(XiHanLogger<>), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    /// <summary>
    /// 已被宿主抢先登记的服务不会被覆盖
    /// </summary>
    /// <remarks>
    /// 全部核心服务走的都是 TryAdd 语义，宿主替换实现是被支持的扩展点；
    /// 一旦退化成无条件 Add，宿主的自定义实现会被框架默认实现顶掉。
    /// </remarks>
    [Fact]
    public void AddXiHanLogging_DoesNotOverrideHostRegistration()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<ILogContext, LogContext>();

        services.AddXiHanLogging();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(ILogContext));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 注册扩展返回原服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanLogging_ReturnsSameServiceCollection()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanLogging());
    }

    /// <summary>
    /// 从配置注册时按配置节绑定日志选项
    /// </summary>
    [Fact]
    public void AddXiHanLogging_WithConfiguration_BindsOptionsFromSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XiHan:Logging:MinimumLevel"] = "Warning",
                ["XiHan:Logging:FileOutputPath"] = "custom/x-.log",
                ["XiHan:Logging:AsyncBufferSize"] = "42"
            })
            .Build();

        IServiceCollection services = new ServiceCollection();
        services.AddXiHanLogging(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanLoggingOptions>>().Value;

        Assert.Equal(LogLevel.Warning, options.MinimumLevel);
        Assert.Equal("custom/x-.log", options.FileOutputPath);
        Assert.Equal(42, options.AsyncBufferSize);
    }

    /// <summary>
    /// 传入空配置时立刻抛出参数异常
    /// </summary>
    [Fact]
    public void AddXiHanLogging_WithNullConfiguration_ThrowsArgumentNullException()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddXiHanLogging((IConfiguration)null!));
    }

    private static List<ServiceDescriptor> LoggerProviderDescriptors(IServiceCollection services)
    {
        return [.. services.Where(descriptor => descriptor.ServiceType == typeof(ILoggerProvider))];
    }
}
