// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Implementations;

namespace XiHan.Framework.Traffic.Tests;

/// <summary>
/// 流量治理模块测试
/// </summary>
/// <remarks>
/// 模块只做两件事：声明对多租户抽象模块的依赖、把灰度路由服务装进容器。
/// 依赖声明不能丢——租户维度灰度依赖多租户抽象先完成装配。
/// </remarks>
public class XiHanTrafficModuleTests
{
    /// <summary>
    /// 流量治理模块是标准曦寒模块
    /// </summary>
    [Fact]
    public void Module_IsXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanTrafficModule());
    }

    /// <summary>
    /// 模块显式依赖多租户抽象模块
    /// </summary>
    [Fact]
    public void Module_DependsOnMultiTenancyAbstractionsModule()
    {
        var attributes = typeof(XiHanTrafficModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToList();

        Assert.Single(attributes);
        Assert.Contains(typeof(XiHanMultiTenancyAbstractionsModule), attributes[0].GetDependedTypes());
    }

    /// <summary>
    /// 服务配置阶段把灰度路由整套服务装进容器
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersGrayRoutingServices()
    {
        var services = CreateServices();

        new XiHanTrafficModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<DefaultGrayRuleEngine>(provider.GetRequiredService<IGrayRuleEngine>());
        Assert.IsType<InMemoryGrayRuleRepository>(provider.GetRequiredService<IGrayRuleRepository>());
        Assert.Equal(5, provider.GetServices<IGrayMatcher>().Count());
    }

    /// <summary>
    /// 重复执行服务配置不会重复注册引擎与仓储
    /// </summary>
    /// <remarks>
    /// 模块被多个入口重复依赖时 ConfigureServices 可能被走到两次，这里确认不会出现双份单例描述符。
    /// </remarks>
    [Fact]
    public void ConfigureServices_CalledTwice_DoesNotDuplicateEngineOrRepository()
    {
        var services = CreateServices();
        var module = new XiHanTrafficModule();
        var context = new ServiceConfigurationContext(services);

        module.ConfigureServices(context);
        module.ConfigureServices(context);

        var engineDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IGrayRuleEngine)).ToList();
        var repositoryDescriptors = services.Where(descriptor => descriptor.ServiceType == typeof(IGrayRuleRepository)).ToList();

        Assert.Single(engineDescriptors);
        Assert.Single(repositoryDescriptors);
    }

    /// <summary>
    /// 服务配置上下文承载的就是传入的服务集合
    /// </summary>
    [Fact]
    public void ConfigureServices_WritesIntoContextServices()
    {
        var services = CreateServices();
        var context = new ServiceConfigurationContext(services);
        var before = services.Count;

        new XiHanTrafficModule().ConfigureServices(context);

        Assert.Same(services, context.Services);
        Assert.True(services.Count > before);
    }

    /// <summary>
    /// 构造一个已具备配置与日志基础设施的服务集合
    /// </summary>
    /// <remarks>
    /// 模块的 ConfigureServices 会读取 IConfiguration，容器里没有实例注册时 GetConfiguration 会直接抛异常，
    /// 因此这里必须先按实例方式注册一份空配置。
    /// </remarks>
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        return services;
    }
}
