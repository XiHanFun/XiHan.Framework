// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Timing.Tests;

/// <summary>
/// 时间管理模块测试
/// </summary>
/// <remarks>
/// 模块是这个包对外的唯一装配入口，本身没有额外逻辑，价值全在「装上去之后容器里有什么」。
/// 因此断言落在两处：模块能被模块加载器识别，以及走完模块入口后三个服务确实可解析。
/// </remarks>
public class XiHanTimingModuleTests
{
    /// <summary>
    /// 模块继承框架模块基类，才能被模块加载器识别
    /// </summary>
    [Fact]
    public void Module_IsXiHanModule()
    {
        Assert.True(typeof(XiHanTimingModule).IsAssignableTo(typeof(XiHanModule)));
        Assert.True(typeof(XiHanTimingModule).IsAssignableTo(typeof(IXiHanModule)));
    }

    /// <summary>
    /// 服务配置后三个时间服务都能解析出来
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersTimingServices()
    {
        var context = CreateContext();
        var module = new XiHanTimingModule();

        module.ConfigureServices(context);

        using var provider = context.Services.BuildServiceProvider();
        Assert.IsType<Clock>(provider.GetRequiredService<IClock>());
        Assert.IsType<TZConvertTimezoneProvider>(provider.GetRequiredService<ITimezoneProvider>());
        Assert.IsType<CurrentTimezoneProvider>(provider.GetRequiredService<ICurrentTimezoneProvider>());
    }

    /// <summary>
    /// 服务配置沿用扩展方法声明的生命周期
    /// </summary>
    [Fact]
    public void ConfigureServices_KeepsLifetimesDeclaredByExtension()
    {
        var context = CreateContext();
        var module = new XiHanTimingModule();

        module.ConfigureServices(context);

        Assert.Equal(
            ServiceLifetime.Singleton,
            context.Services.Single(item => item.ServiceType == typeof(IClock)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Singleton,
            context.Services.Single(item => item.ServiceType == typeof(ITimezoneProvider)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Transient,
            context.Services.Single(item => item.ServiceType == typeof(ICurrentTimezoneProvider)).Lifetime);
    }

    /// <summary>
    /// 模块默认不打开多时区开关
    /// </summary>
    /// <remarks>
    /// 模块没有绑定任何配置节，时钟选项保持构造函数给的 Unspecified，
    /// 也就是说「按 UTC 存时间」必须由宿主显式配置，不会被模块悄悄改掉。
    /// </remarks>
    [Fact]
    public void ConfigureServices_DoesNotEnableMultipleTimezoneByDefault()
    {
        var context = CreateContext();
        var module = new XiHanTimingModule();

        module.ConfigureServices(context);

        using var provider = context.Services.BuildServiceProvider();
        var clock = provider.GetRequiredService<IClock>();

        Assert.Equal(DateTimeKind.Unspecified, clock.Kind);
        Assert.False(clock.SupportsMultipleTimezone);
    }

    /// <summary>
    /// 异步入口与同步入口装配结果一致
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_RegistersSameServicesAsSyncOverload()
    {
        var context = CreateContext();
        var module = new XiHanTimingModule();

        await module.ConfigureServicesAsync(context);

        using var provider = context.Services.BuildServiceProvider();
        Assert.IsType<Clock>(provider.GetRequiredService<IClock>());
        Assert.IsType<TZConvertTimezoneProvider>(provider.GetRequiredService<ITimezoneProvider>());
        Assert.IsType<CurrentTimezoneProvider>(provider.GetRequiredService<ICurrentTimezoneProvider>());
    }

    /// <summary>
    /// 构造带有配置源的服务配置上下文
    /// </summary>
    /// <returns>服务配置上下文</returns>
    private static ServiceConfigurationContext CreateContext()
    {
        var services = new ServiceCollection();

        // 模块的 ConfigureServices 会读取配置，缺少 IConfiguration 单例实例会直接抛出
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        return new ServiceConfigurationContext(services);
    }
}
