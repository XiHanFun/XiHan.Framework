// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Authorization.Abac;
using XiHan.Framework.Authorization.AspNetCore;
using XiHan.Framework.Authorization.Extensions.DependencyInjection;
using XiHan.Framework.Authorization.Permissions;
using XiHan.Framework.Authorization.Policies;
using XiHan.Framework.Authorization.Roles;
using XiHan.Framework.Authorization.Tests.Infrastructure;
using XiHan.Framework.Security.Users;

namespace XiHan.Framework.Authorization.Tests.Extensions.DependencyInjection;

/// <summary>
/// 曦寒授权服务注册测试
/// </summary>
/// <remarks>
/// 注册全部走 TryAdd 语义：宿主先注册的实现必须优先，重复调用不能产生重复的授权处理器。
/// 生命周期也是契约——存储与检查器按请求作用域、策略提供器按单例，前者错成单例会跨租户串数据，
/// 后者错成作用域会让 ASP.NET Core 授权管线在启动期解析失败。
/// </remarks>
public class XiHanAuthorizationServiceCollectionExtensionsTests
{
    /// <summary>
    /// 八个作用域服务与一个单例策略提供器都被注册
    /// </summary>
    [Fact]
    public void AddXiHanAuthorization_RegistersAllContracts()
    {
        var services = CreateServices();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRoleStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IPermissionStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IPermissionChecker));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IPolicyStore));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IPolicyEvaluator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAbacAttributeCollector));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAbacEvaluator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuthorizationService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuthorizationPolicyProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAuthorizationHandler));
    }

    /// <summary>
    /// 存储、检查器与授权服务都是请求作用域
    /// </summary>
    /// <param name="serviceType">服务契约类型</param>
    [Theory]
    [InlineData(typeof(IRoleStore))]
    [InlineData(typeof(IPermissionStore))]
    [InlineData(typeof(IPermissionChecker))]
    [InlineData(typeof(IPolicyStore))]
    [InlineData(typeof(IPolicyEvaluator))]
    [InlineData(typeof(IAbacAttributeCollector))]
    [InlineData(typeof(IAbacEvaluator))]
    [InlineData(typeof(IAuthorizationService))]
    [InlineData(typeof(IAuthorizationHandler))]
    public void AddXiHanAuthorization_RegistersScopedLifetime(Type serviceType)
    {
        var services = CreateServices();

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == serviceType));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    /// <summary>
    /// 策略提供器必须是单例，才能被授权管线在根容器里解析
    /// </summary>
    [Fact]
    public void AddXiHanAuthorization_RegistersPolicyProviderAsSingleton()
    {
        var services = CreateServices();

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(IAuthorizationPolicyProvider)));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    /// <summary>
    /// 重复调用不会重复注册授权处理器
    /// </summary>
    [Fact]
    public void AddXiHanAuthorization_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddXiHanAuthorization(configuration);
        services.AddXiHanAuthorization(configuration);

        Assert.Single(services.Where(item => item.ServiceType == typeof(IAuthorizationHandler)));
        Assert.Single(services.Where(item => item.ServiceType == typeof(IPermissionChecker)));
    }

    /// <summary>
    /// 宿主已注册的实现优先，扩展方法不覆盖
    /// </summary>
    [Fact]
    public void AddXiHanAuthorization_KeepsPreRegisteredImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRoleStore>(new FaultInjectingRoleStore());

        services.AddXiHanAuthorization(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(services.Where(item => item.ServiceType == typeof(IRoleStore)));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.IsType<FaultInjectingRoleStore>(descriptor.ImplementationInstance);
    }

    /// <summary>
    /// 扩展方法返回原服务集合，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanAuthorization_ReturnsSameCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanAuthorization(new ConfigurationBuilder().Build()));
    }

    /// <summary>
    /// 作用域内所有契约都能解析出默认实现
    /// </summary>
    [Fact]
    public void AddXiHanAuthorization_ResolvesDefaultImplementations()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        Assert.IsType<DefaultRoleStore>(services.GetRequiredService<IRoleStore>());
        Assert.IsType<DefaultPermissionStore>(services.GetRequiredService<IPermissionStore>());
        Assert.IsType<DefaultPermissionChecker>(services.GetRequiredService<IPermissionChecker>());
        Assert.IsType<DefaultPolicyStore>(services.GetRequiredService<IPolicyStore>());
        Assert.IsType<DefaultPolicyEvaluator>(services.GetRequiredService<IPolicyEvaluator>());
        Assert.IsType<DefaultAbacAttributeCollector>(services.GetRequiredService<IAbacAttributeCollector>());
        Assert.IsType<DefaultAbacEvaluator>(services.GetRequiredService<IAbacEvaluator>());
        Assert.IsType<DefaultAuthorizationService>(services.GetRequiredService<IAuthorizationService>());
        Assert.IsType<HybridPermissionPolicyProvider>(services.GetRequiredService<IAuthorizationPolicyProvider>());
    }

    /// <summary>
    /// 混合授权处理器以可枚举方式注册，能被授权管线取到
    /// </summary>
    [Fact]
    public void AddXiHanAuthorization_ResolvesHybridAuthorizationHandler()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handlers = scope.ServiceProvider.GetServices<IAuthorizationHandler>();

        Assert.Contains(handlers, handler => handler is HybridPermissionAuthorizationHandler);
    }

    /// <summary>
    /// 同一作用域内取两次是同一个实例，跨作用域则不是
    /// </summary>
    [Fact]
    public void AddXiHanAuthorization_ScopedServicesAreIsolatedPerScope()
    {
        using var provider = BuildProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var inFirst = first.ServiceProvider.GetRequiredService<IPermissionStore>();

        Assert.Same(inFirst, first.ServiceProvider.GetRequiredService<IPermissionStore>());
        Assert.NotSame(inFirst, second.ServiceProvider.GetRequiredService<IPermissionStore>());
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddXiHanAuthorization(new ConfigurationBuilder().Build());
        return services;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<ICurrentUser>(new FakeCurrentUser());
        services.AddXiHanAuthorization(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }
}
