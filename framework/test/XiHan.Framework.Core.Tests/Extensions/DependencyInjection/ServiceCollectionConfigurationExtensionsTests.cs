// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.Tests.Extensions.DependencyInjection;

/// <summary>
/// 服务集合配置扩展方法测试
/// </summary>
/// <remarks>
/// 装配期读配置有两个来源：通用主机放进来的 <see cref="HostBuilderContext"/>，以及直接登记的 <see cref="IConfiguration"/> 单例。
/// 前者优先——主机路径下这两者可能不是同一份，取错会读到还没合并完的配置。
/// 用例把「优先级」和「两条来源各自可用」分开锁死，而不是只测其中一条。
/// </remarks>
public class ServiceCollectionConfigurationExtensionsTests
{
    /// <summary>
    /// 没有任何配置来源时读取返回空
    /// </summary>
    [Fact]
    public void GetConfigurationOrNull_WhenNothingRegistered_ReturnsNull()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Null(services.GetConfigurationOrNull());
    }

    /// <summary>
    /// 没有任何配置来源时强制读取抛出框架异常
    /// </summary>
    [Fact]
    public void GetConfiguration_WhenNothingRegistered_ThrowsXiHanException()
    {
        IServiceCollection services = new ServiceCollection();

        var thrown = Assert.Throws<XiHanException>(() => services.GetConfiguration());

        Assert.Contains(nameof(IConfiguration), thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 直接登记的配置单例能被读回
    /// </summary>
    [Fact]
    public void GetConfigurationOrNull_ReadsRegisteredConfigurationInstance()
    {
        IServiceCollection services = new ServiceCollection();
        var configuration = BuildConfiguration("直接登记");
        services.AddSingleton(configuration);

        Assert.Same(configuration, services.GetConfigurationOrNull());
        Assert.Same(configuration, services.GetConfiguration());
    }

    /// <summary>
    /// 主机上下文里的配置优先于直接登记的配置
    /// </summary>
    [Fact]
    public void GetConfigurationOrNull_PrefersHostBuilderContextConfiguration()
    {
        IServiceCollection services = new ServiceCollection();
        var fromHost = BuildConfiguration("来自主机上下文");
        var registeredDirectly = BuildConfiguration("直接登记");

        services.AddSingleton(registeredDirectly);
        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>())
        {
            Configuration = fromHost
        });

        Assert.Same(fromHost, services.GetConfigurationOrNull());
    }

    /// <summary>
    /// 主机上下文存在但未携带配置时回落到直接登记的配置
    /// </summary>
    [Fact]
    public void GetConfigurationOrNull_WhenHostContextHasNoConfiguration_FallsBackToRegisteredInstance()
    {
        IServiceCollection services = new ServiceCollection();
        var registeredDirectly = BuildConfiguration("直接登记");

        services.AddSingleton(registeredDirectly);
        services.AddSingleton(new HostBuilderContext(new Dictionary<object, object>()));

        Assert.Same(registeredDirectly, services.GetConfigurationOrNull());
    }

    /// <summary>
    /// 替换配置后只留一条配置注册，且读到的是新配置
    /// </summary>
    [Fact]
    public void ReplaceConfiguration_LeavesSingleRegistrationPointingToNewConfiguration()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(BuildConfiguration("旧配置"));
        var replacement = BuildConfiguration("新配置");

        var returned = services.ReplaceConfiguration(replacement);

        Assert.Same(services, returned);
        Assert.Same(replacement, services.GetConfigurationOrNull());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IConfiguration));
    }

    /// <summary>
    /// 尚未登记配置时替换等同于新增
    /// </summary>
    [Fact]
    public void ReplaceConfiguration_WhenNothingRegistered_AddsConfiguration()
    {
        IServiceCollection services = new ServiceCollection();
        var configuration = BuildConfiguration("新配置");

        services.ReplaceConfiguration(configuration);

        Assert.Same(configuration, services.GetConfigurationOrNull());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IConfiguration));
    }

    /// <summary>
    /// 替换后的配置以单例注册，容器里解析出的是同一个实例
    /// </summary>
    [Fact]
    public void ReplaceConfiguration_RegistersConfigurationAsSingleton()
    {
        IServiceCollection services = new ServiceCollection();
        var configuration = BuildConfiguration("新配置");

        services.ReplaceConfiguration(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Same(configuration, provider.GetRequiredService<IConfiguration>());
    }

    /// <summary>
    /// 构造一份只带样例键的内存配置
    /// </summary>
    /// <param name="marker">样例值</param>
    /// <returns>配置</returns>
    private static IConfiguration BuildConfiguration(string marker)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sample:Marker"] = marker
            })
            .Build();
    }
}
