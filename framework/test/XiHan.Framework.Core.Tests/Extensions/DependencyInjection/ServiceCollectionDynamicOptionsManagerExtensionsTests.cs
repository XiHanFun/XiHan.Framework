// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Tests.Extensions.Options;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务集合动态选项管理器扩展方法测试
/// </summary>
/// <remarks>
/// 动态选项要同时接管 <see cref="IOptions{TOptions}"/> 与 <see cref="IOptionsSnapshot{TOptions}"/> 两条入口，
/// 只换其中一条会出现「同一个请求里两处读到的选项不一致」这种极难排查的问题，因此两条一起断言。
/// 生命周期必须是作用域：动态选项按请求上下文（租户、用户）解析，注册成单例会造成跨请求串数据。
/// </remarks>
public class ServiceCollectionDynamicOptionsManagerExtensionsTests
{
    /// <summary>
    /// 选项与选项快照两条入口都被换成动态管理器
    /// </summary>
    [Fact]
    public void AddXiHanDynamicOptions_ReplacesBothOptionsAndSnapshot()
    {
        IServiceCollection services = BuildServices();

        services.AddXiHanDynamicOptions<DynamicSampleOptions, SampleDynamicOptionsManager>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<SampleDynamicOptionsManager>(scope.ServiceProvider.GetRequiredService<IOptions<DynamicSampleOptions>>());
        Assert.IsType<SampleDynamicOptionsManager>(scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DynamicSampleOptions>>());
    }

    /// <summary>
    /// 扩展返回同一个服务集合，可以继续链式调用
    /// </summary>
    [Fact]
    public void AddXiHanDynamicOptions_ReturnsSameCollection()
    {
        IServiceCollection services = BuildServices();

        var returned = services.AddXiHanDynamicOptions<DynamicSampleOptions, SampleDynamicOptionsManager>();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 两条入口都以作用域生命周期登记
    /// </summary>
    [Fact]
    public void AddXiHanDynamicOptions_RegistersScopedLifetime()
    {
        IServiceCollection services = BuildServices();

        services.AddXiHanDynamicOptions<DynamicSampleOptions, SampleDynamicOptionsManager>();

        var optionsDescriptor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IOptions<DynamicSampleOptions>));
        var snapshotDescriptor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IOptionsSnapshot<DynamicSampleOptions>));

        Assert.Equal(ServiceLifetime.Scoped, optionsDescriptor.Lifetime);
        Assert.Equal(typeof(SampleDynamicOptionsManager), optionsDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, snapshotDescriptor.Lifetime);
        Assert.Equal(typeof(SampleDynamicOptionsManager), snapshotDescriptor.ImplementationType);
    }

    /// <summary>
    /// 已存在的同类型闭合注册会被替换而不是叠加
    /// </summary>
    [Fact]
    public void AddXiHanDynamicOptions_ReplacesExistingClosedRegistration()
    {
        IServiceCollection services = BuildServices();
        services.AddScoped<IOptions<DynamicSampleOptions>, OptionsManager<DynamicSampleOptions>>();

        services.AddXiHanDynamicOptions<DynamicSampleOptions, SampleDynamicOptionsManager>();

        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IOptions<DynamicSampleOptions>));

        Assert.Equal(typeof(SampleDynamicOptionsManager), descriptor.ImplementationType);
    }

    /// <summary>
    /// 接管之后仍然读得到已登记的配置委托产出的值
    /// </summary>
    /// <remarks>
    /// 动态管理器继承自框架的选项管理器，配置委托这条链路不能因为接管而断掉，
    /// 否则所有 <c>Configure</c> 写下的默认值都会消失。
    /// </remarks>
    [Fact]
    public void AddXiHanDynamicOptions_KeepsConfiguredValues()
    {
        IServiceCollection services = BuildServices();

        services.AddXiHanDynamicOptions<DynamicSampleOptions, SampleDynamicOptionsManager>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<DynamicSampleOptions>>();

        Assert.Equal("初始", options.Value.Name);
    }

    /// <summary>
    /// 不同作用域拿到不同的动态管理器实例
    /// </summary>
    [Fact]
    public void AddXiHanDynamicOptions_ResolvesPerScopeInstances()
    {
        IServiceCollection services = BuildServices();

        services.AddXiHanDynamicOptions<DynamicSampleOptions, SampleDynamicOptionsManager>();

        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        Assert.NotSame(
            first.ServiceProvider.GetRequiredService<IOptions<DynamicSampleOptions>>(),
            second.ServiceProvider.GetRequiredService<IOptions<DynamicSampleOptions>>());
    }

    /// <summary>
    /// 构造一份已开启选项支持并登记了默认值的服务集合
    /// </summary>
    /// <returns>服务集合</returns>
    private static IServiceCollection BuildServices()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddOptions();
        services.Configure<DynamicSampleOptions>(options => options.Name = "初始");
        return services;
    }
}
