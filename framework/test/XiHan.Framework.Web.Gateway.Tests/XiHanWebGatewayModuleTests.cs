// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Traffic.GrayRouting.Abstractions;
using XiHan.Framework.Traffic.GrayRouting.Implementations;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 网关模块装配测试
/// </summary>
/// <remarks>
/// 模块只做两件事：声明依赖模块、把灰度路由服务注册进容器。
/// 依赖声明用类型名断言而不是 typeof 比较，避免测试工程为了断言而引入一堆并不使用的项目引用。
/// </remarks>
public class XiHanWebGatewayModuleTests
{
    /// <summary>
    /// 声明了网关运行所必需的依赖模块
    /// </summary>
    /// <remarks>
    /// 少声明一个依赖不会编译报错，只会在运行期表现为「某个服务解析不出来」，
    /// 这类问题排查成本极高，所以在这里锁死。
    /// </remarks>
    [Fact]
    public void Module_DeclaresRequiredModuleDependencies()
    {
        var attribute = typeof(XiHanWebGatewayModule).GetCustomAttribute<DependsOnAttribute>();

        Assert.NotNull(attribute);

        var dependedNames = attribute.GetDependedTypes().Select(type => type.Name).ToArray();
        Assert.Contains("XiHanWebCoreModule", dependedNames);
        Assert.Contains("XiHanTrafficModule", dependedNames);
        Assert.Contains("XiHanMultiTenancyModule", dependedNames);
        Assert.Contains("XiHanLoggingModule", dependedNames);
        Assert.Contains("XiHanSerializationModule", dependedNames);
    }

    /// <summary>
    /// 模块是可被模块系统装配的具体模块类型
    /// </summary>
    [Fact]
    public void Module_IsConcreteXiHanModule()
    {
        var moduleType = typeof(XiHanWebGatewayModule);

        Assert.True(moduleType.IsAssignableTo(typeof(XiHanModule)));
        Assert.False(moduleType.IsAbstract);
        Assert.True(moduleType.IsPublic);
    }

    /// <summary>
    /// 服务配置注册灰度路由的引擎、仓储与全部内置匹配器
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersGrayRoutingServices()
    {
        var services = CreateServicesWithConfiguration();

        new XiHanWebGatewayModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IGrayRuleEngine));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IGrayRuleRepository));

        var matchers = services.Where(descriptor => descriptor.ServiceType == typeof(IGrayMatcher)).ToList();
        Assert.Equal(5, matchers.Count);
    }

    /// <summary>
    /// 灰度引擎与规则仓储注册为单例
    /// </summary>
    /// <remarks>
    /// 内存仓储持有规则集合，注册成瞬时会让每次请求拿到空规则集，灰度直接失效。
    /// </remarks>
    [Fact]
    public void ConfigureServices_RegistersGrayRoutingAsSingleton()
    {
        var services = CreateServicesWithConfiguration();

        new XiHanWebGatewayModule().ConfigureServices(new ServiceConfigurationContext(services));

        var engine = services.Single(descriptor => descriptor.ServiceType == typeof(IGrayRuleEngine));
        var repository = services.Single(descriptor => descriptor.ServiceType == typeof(IGrayRuleRepository));

        Assert.Equal(ServiceLifetime.Singleton, engine.Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, repository.Lifetime);
        Assert.Equal(typeof(DefaultGrayRuleEngine), engine.ImplementationType);
        Assert.Equal(typeof(InMemoryGrayRuleRepository), repository.ImplementationType);
    }

    /// <summary>
    /// 注册后的灰度引擎能真正从容器里解析出来
    /// </summary>
    /// <remarks>
    /// 只断言描述符存在还不够：引擎构造函数依赖仓储、匹配器集合和日志器，
    /// 少注册任何一环都要到运行期才暴露。
    /// </remarks>
    [Fact]
    public void ConfigureServices_ResolvesGrayRuleEngineFromContainer()
    {
        var services = CreateServicesWithConfiguration();
        services.AddLogging();

        new XiHanWebGatewayModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();

        var engine = provider.GetRequiredService<IGrayRuleEngine>();
        var repository = provider.GetRequiredService<IGrayRuleRepository>();
        var matchers = provider.GetServices<IGrayMatcher>().ToList();

        Assert.IsType<DefaultGrayRuleEngine>(engine);
        Assert.IsType<InMemoryGrayRuleRepository>(repository);
        Assert.Equal(5, matchers.Count);
    }

    /// <summary>
    /// 同一个作用域内解析出的灰度引擎是同一实例
    /// </summary>
    [Fact]
    public void ConfigureServices_ResolvesSameGrayRuleEngineInstance()
    {
        var services = CreateServicesWithConfiguration();
        services.AddLogging();

        new XiHanWebGatewayModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IGrayRuleEngine>(), provider.GetRequiredService<IGrayRuleEngine>());
    }

    /// <summary>
    /// 内置匹配器覆盖到全部规则类型，互不重复
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersDistinctMatcherTypes()
    {
        var services = CreateServicesWithConfiguration();
        services.AddLogging();

        new XiHanWebGatewayModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();
        var ruleTypes = provider.GetServices<IGrayMatcher>().Select(matcher => matcher.RuleType).ToList();

        Assert.Equal(ruleTypes.Count, ruleTypes.Distinct().Count());
    }

    /// <summary>
    /// 服务集合里没有配置时装配直接失败
    /// </summary>
    /// <remarks>
    /// 模块在 ConfigureServices 里强制读取 IConfiguration，缺失时抛框架异常而不是继续静默装配。
    /// </remarks>
    [Fact]
    public void ConfigureServices_WithoutConfiguration_Throws()
    {
        var services = new ServiceCollection();
        var module = new XiHanWebGatewayModule();
        var context = new ServiceConfigurationContext(services);

        Assert.Throws<XiHanException>(() => module.ConfigureServices(context));
    }

    /// <summary>
    /// 构造带有空配置的服务集合
    /// </summary>
    private static ServiceCollection CreateServicesWithConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return services;
    }
}
