// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Logging;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 初始化日志扩展方法测试
/// </summary>
/// <remarks>
/// 初始化日志解决的是「容器还没建好、真正的日志管线还不存在」这段空窗期：
/// 装配期的日志先攒在内存里，等应用初始化时再一次性回放到真正的日志器。
/// 因此这个扩展的两条契约是：必须能在只有服务集合的阶段拿到日志器；
/// 同一类别多次获取拿到同一个日志器（否则攒在前一个实例里的条目会连同回放一起丢掉）。
/// </remarks>
public class ServiceCollectionLoggingExtensionsTests
{
    /// <summary>
    /// 从已登记的工厂拿到初始化日志器
    /// </summary>
    [Fact]
    public void GetInitLogger_ReturnsLoggerFromRegisteredFactory()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IInitLoggerFactory>(new DefaultInitLoggerFactory());

        var logger = services.GetInitLogger<ServiceCollectionLoggingExtensionsTests>();

        Assert.NotNull(logger);
        Assert.IsAssignableFrom<IInitLogger<ServiceCollectionLoggingExtensionsTests>>(logger);
    }

    /// <summary>
    /// 同一类别多次获取拿到同一个日志器
    /// </summary>
    [Fact]
    public void GetInitLogger_ForSameCategory_ReturnsSameLogger()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IInitLoggerFactory>(new DefaultInitLoggerFactory());

        var first = services.GetInitLogger<ServiceCollectionLoggingExtensionsTests>();
        var second = services.GetInitLogger<ServiceCollectionLoggingExtensionsTests>();

        Assert.Same(first, second);
    }

    /// <summary>
    /// 不同类别拿到不同的日志器
    /// </summary>
    [Fact]
    public void GetInitLogger_ForDifferentCategories_ReturnsDifferentLoggers()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IInitLoggerFactory>(new DefaultInitLoggerFactory());

        var first = services.GetInitLogger<ServiceCollectionLoggingExtensionsTests>();
        var second = services.GetInitLogger<InitLoggerCategorySample>();

        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 装配期写进去的日志条目被攒下来等待回放
    /// </summary>
    [Fact]
    public void GetInitLogger_CollectsEntriesForLaterReplay()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IInitLoggerFactory>(new DefaultInitLoggerFactory());

        var logger = services.GetInitLogger<InitLoggerCategorySample>();
        logger.LogInformation("装配期日志:{Marker}", "曦寒");

        var initLogger = Assert.IsAssignableFrom<IInitLogger<InitLoggerCategorySample>>(logger);
        var entry = Assert.Single(initLogger.Entries);

        Assert.Equal(LogLevel.Information, entry.LogLevel);
        Assert.Equal("装配期日志:曦寒", entry.Message);
    }

    /// <summary>
    /// 未登记初始化日志工厂时抛出无效操作异常
    /// </summary>
    [Fact]
    public void GetInitLogger_WhenFactoryMissing_Throws()
    {
        IServiceCollection services = new ServiceCollection();

        var thrown = Assert.Throws<InvalidOperationException>(() => services.GetInitLogger<InitLoggerCategorySample>());

        Assert.Contains(nameof(IInitLoggerFactory), thrown.Message, StringComparison.Ordinal);
    }
}

/// <summary>
/// 初始化日志测试用的日志类别标记
/// </summary>
public sealed class InitLoggerCategorySample
{
    /// <summary>
    /// 类别标记
    /// </summary>
    public string Marker { get; } = "category";
}
