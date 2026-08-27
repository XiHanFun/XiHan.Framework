// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Logging.Options;
using XiHan.Framework.Logging.Services;

namespace XiHan.Framework.Logging.Tests.Services;

/// <summary>
/// 曦寒日志工厂测试
/// </summary>
/// <remarks>
/// 工厂本身只做一件事：把四类日志器的创建统一转交给容器。
/// 因此断言分两侧——容器里有登记时能拿到正确实现类型，容器里没登记时必须显式失败而不是返回 null。
/// </remarks>
public class XiHanLoggerFactoryTests
{
    /// <summary>
    /// 创建通用日志器时从容器解析实现
    /// </summary>
    [Fact]
    public void CreateLogger_ResolvesRegisteredImplementation()
    {
        using var provider = BuildProvider();
        var factory = new XiHanLoggerFactory(provider);

        Assert.IsType<XiHanLogger>(factory.CreateLogger("Cat"));
    }

    /// <summary>
    /// 创建泛型日志器时按闭合泛型从容器解析实现
    /// </summary>
    [Fact]
    public void CreateLoggerOfT_ResolvesClosedGenericImplementation()
    {
        using var provider = BuildProvider();
        var factory = new XiHanLoggerFactory(provider);

        Assert.IsType<XiHanLogger<XiHanLoggerFactoryTests>>(factory.CreateLogger<XiHanLoggerFactoryTests>());
    }

    /// <summary>
    /// 创建结构化日志器时从容器解析实现
    /// </summary>
    [Fact]
    public void CreateStructuredLogger_ResolvesRegisteredImplementation()
    {
        using var provider = BuildProvider();
        var factory = new XiHanLoggerFactory(provider);

        Assert.IsType<StructuredLogger>(factory.CreateStructuredLogger("Cat"));
    }

    /// <summary>
    /// 创建性能日志器时从容器解析实现
    /// </summary>
    [Fact]
    public void CreatePerformanceLogger_ResolvesRegisteredImplementation()
    {
        using var provider = BuildProvider();
        var factory = new XiHanLoggerFactory(provider);

        Assert.IsType<PerformanceLogger>(factory.CreatePerformanceLogger("Cat"));
    }

    /// <summary>
    /// 单例注册的日志器在多次创建间保持同一实例
    /// </summary>
    [Fact]
    public void CreateStructuredLogger_WithSingletonRegistration_ReturnsSameInstance()
    {
        using var provider = BuildProvider();
        var factory = new XiHanLoggerFactory(provider);

        Assert.Same(factory.CreateStructuredLogger("A"), factory.CreateStructuredLogger("B"));
    }

    /// <summary>
    /// 容器里没有登记时必须显式抛出而不是返回 null
    /// </summary>
    [Fact]
    public void CreateLogger_WhenNotRegistered_Throws()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var factory = new XiHanLoggerFactory(provider);

        Assert.Throws<InvalidOperationException>(() => factory.CreateLogger("Cat"));
        Assert.Throws<InvalidOperationException>(() => factory.CreateLogger<XiHanLoggerFactoryTests>());
        Assert.Throws<InvalidOperationException>(() => factory.CreateStructuredLogger("Cat"));
        Assert.Throws<InvalidOperationException>(() => factory.CreatePerformanceLogger("Cat"));
    }

    private static ServiceProvider BuildProvider()
    {
        IServiceCollection services = new ServiceCollection();

        // 下游 ILogger 用官方空实现，工厂测试只关心解析链路，不关心日志内容
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IOptions<XiHanLoggingOptions>>(
            Microsoft.Extensions.Options.Options.Create(new XiHanLoggingOptions()));
        services.AddTransient<IXiHanLogger, XiHanLogger>();
        services.AddTransient(typeof(IXiHanLogger<>), typeof(XiHanLogger<>));
        services.AddSingleton<IStructuredLogger, StructuredLogger>();
        services.AddSingleton<IPerformanceLogger, PerformanceLogger>();

        return services.BuildServiceProvider();
    }
}
