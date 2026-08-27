// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Timing.Extensions.DependencyInjection;

namespace XiHan.Framework.Timing.Tests;

/// <summary>
/// 时间服务注册扩展测试
/// </summary>
/// <remarks>
/// 生命周期在这里是硬契约：时钟与时区提供器是无状态的单例，
/// 而当前时区提供器承载的是「按调用流程隔离」的状态，必须是瞬时的——
/// 一旦被误注册成单例，多租户/多时区场景会串时区，所以逐个锁死。
/// </remarks>
public class ServiceCollectionExtensionsTests
{
    /// <summary>
    /// 扩展方法返回同一个服务集合，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanTiming_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanTiming();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 时钟注册为单例，实现为默认时钟
    /// </summary>
    [Fact]
    public void AddXiHanTiming_RegistersClockAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanTiming();

        var descriptor = services.Single(item => item.ServiceType == typeof(IClock));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(Clock), descriptor.ImplementationType);
    }

    /// <summary>
    /// 时区提供器注册为单例，实现为 TimeZoneConverter 封装
    /// </summary>
    [Fact]
    public void AddXiHanTiming_RegistersTimezoneProviderAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanTiming();

        var descriptor = services.Single(item => item.ServiceType == typeof(ITimezoneProvider));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(TZConvertTimezoneProvider), descriptor.ImplementationType);
    }

    /// <summary>
    /// 当前时区提供器注册为瞬时，避免跨调用流程串时区
    /// </summary>
    [Fact]
    public void AddXiHanTiming_RegistersCurrentTimezoneProviderAsTransient()
    {
        var services = new ServiceCollection();

        services.AddXiHanTiming();

        var descriptor = services.Single(item => item.ServiceType == typeof(ICurrentTimezoneProvider));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(CurrentTimezoneProvider), descriptor.ImplementationType);
    }

    /// <summary>
    /// 注册后三个服务都能解析出来，且生命周期与声明一致
    /// </summary>
    [Fact]
    public void AddXiHanTiming_ResolvesServicesWithDeclaredLifetimes()
    {
        var services = new ServiceCollection();
        services.AddXiHanTiming();

        using var provider = services.BuildServiceProvider();

        var clock = provider.GetRequiredService<IClock>();
        var timezoneProvider = provider.GetRequiredService<ITimezoneProvider>();
        var firstCurrentTimezoneProvider = provider.GetRequiredService<ICurrentTimezoneProvider>();
        var secondCurrentTimezoneProvider = provider.GetRequiredService<ICurrentTimezoneProvider>();

        Assert.IsType<Clock>(clock);
        Assert.IsType<TZConvertTimezoneProvider>(timezoneProvider);
        Assert.IsType<CurrentTimezoneProvider>(firstCurrentTimezoneProvider);
        Assert.Same(clock, provider.GetRequiredService<IClock>());
        Assert.Same(timezoneProvider, provider.GetRequiredService<ITimezoneProvider>());
        Assert.NotSame(firstCurrentTimezoneProvider, secondCurrentTimezoneProvider);
    }

    /// <summary>
    /// 容器在开启作用域校验时也能构建，说明单例时钟没有俘获作用域依赖
    /// </summary>
    [Fact]
    public void AddXiHanTiming_BuildsWithScopeValidationEnabled()
    {
        var services = new ServiceCollection();
        services.AddXiHanTiming();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        Assert.IsType<Clock>(provider.GetRequiredService<IClock>());
    }

    /// <summary>
    /// 未额外配置时，时钟选项保持未指定，时钟不宣称支持多时区
    /// </summary>
    [Fact]
    public void AddXiHanTiming_WithoutConfiguration_LeavesClockKindUnspecified()
    {
        var services = new ServiceCollection();
        services.AddXiHanTiming();

        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<XiHanClockOptions>>();
        var clock = provider.GetRequiredService<IClock>();

        Assert.Equal(DateTimeKind.Unspecified, options.Value.Kind);
        Assert.Equal(DateTimeKind.Unspecified, clock.Kind);
        Assert.False(clock.SupportsMultipleTimezone);
    }

    /// <summary>
    /// 配置为 UTC 后，解析出的时钟按 UTC 语义工作
    /// </summary>
    [Fact]
    public void AddXiHanTiming_WhenKindConfiguredAsUtc_ClockHonoursConfiguredKind()
    {
        var services = new ServiceCollection();
        services.AddXiHanTiming();
        services.Configure<XiHanClockOptions>(options => options.Kind = DateTimeKind.Utc);

        using var provider = services.BuildServiceProvider();

        var clock = provider.GetRequiredService<IClock>();

        Assert.Equal(DateTimeKind.Utc, clock.Kind);
        Assert.True(clock.SupportsMultipleTimezone);
        Assert.Equal(DateTimeKind.Utc, clock.Now.Kind);
    }

    /// <summary>
    /// 时钟选项在扩展方法之前配置同样生效，注册顺序不影响结果
    /// </summary>
    [Fact]
    public void AddXiHanTiming_WhenKindConfiguredBeforeRegistration_StillHonoursConfiguredKind()
    {
        var services = new ServiceCollection();
        services.Configure<XiHanClockOptions>(options => options.Kind = DateTimeKind.Local);
        services.AddXiHanTiming();

        using var provider = services.BuildServiceProvider();

        var clock = provider.GetRequiredService<IClock>();

        Assert.Equal(DateTimeKind.Local, clock.Kind);
        Assert.False(clock.SupportsMultipleTimezone);
    }
}
