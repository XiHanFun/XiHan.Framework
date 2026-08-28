// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Castle.Tests.TestDoubles;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Castle.Tests;

/// <summary>
/// 曦寒框架 Castle 动态代理模块测试
/// </summary>
/// <remarks>
/// 模块本身只做一件事：在所有服务注册完成之后把动态代理应用到服务集合上。
/// 因此断言落在"经过模块的服务配置后处理之后，被登记了拦截器的服务确实变成了可用代理"这一条上。
/// </remarks>
public class XiHanCastleModuleTests
{
    /// <summary>
    /// 模块继承自框架模块基类，才能被模块系统装配
    /// </summary>
    [Fact]
    public void XiHanCastleModule_IsXiHanModule()
    {
        Assert.True(typeof(XiHanModule).IsAssignableFrom(typeof(XiHanCastleModule)));
    }

    /// <summary>
    /// 服务配置后处理会为登记了拦截器的服务创建可用代理
    /// </summary>
    [Fact]
    public void PostConfigureServices_AppliesCastleDynamicProxy()
    {
        var services = CreateServices();
        var context = new ServiceConfigurationContext(services);

        new XiHanCastleModule().PostConfigureServices(context);

        using var provider = services.BuildServiceProvider();
        var greeting = provider.GetRequiredService<IGreetingService>();
        var text = greeting.Greet("曦寒");

        Assert.True(ProxyUtil.IsProxy(greeting));
        Assert.Equal("你好，曦寒", text);

        var log = provider.GetRequiredService<CallLog>();
        Assert.Single(log.Entries);
        Assert.Equal("日志:Greet", log.Entries[0]);
    }

    /// <summary>
    /// 异步版服务配置后处理与同步版效果一致
    /// </summary>
    [Fact]
    public async Task PostConfigureServicesAsync_AppliesCastleDynamicProxy()
    {
        var services = CreateServices();
        var context = new ServiceConfigurationContext(services);

        await new XiHanCastleModule().PostConfigureServicesAsync(context);

        using var provider = services.BuildServiceProvider();

        Assert.True(ProxyUtil.IsProxy(provider.GetRequiredService<IGreetingService>()));
    }

    /// <summary>
    /// 禁用类拦截器后模块不改动任何描述器
    /// </summary>
    [Fact]
    public void PostConfigureServices_WhenClassInterceptorsDisabled_LeavesDescriptorUntouched()
    {
        var services = CreateServices();
        services.DisableClassInterceptors();
        var before = services.Single(d => d.ServiceType == typeof(IGreetingService));

        new XiHanCastleModule().PostConfigureServices(new ServiceConfigurationContext(services));

        Assert.Same(before, services.Single(d => d.ServiceType == typeof(IGreetingService)));

        using var provider = services.BuildServiceProvider();
        Assert.False(ProxyUtil.IsProxy(provider.GetRequiredService<IGreetingService>()));
    }

    /// <summary>
    /// 模块作用于上下文携带的服务集合本身，而不是它的副本
    /// </summary>
    [Fact]
    public void PostConfigureServices_OperatesOnContextServices()
    {
        var services = CreateServices();
        var context = new ServiceConfigurationContext(services);

        Assert.Same(services, context.Services);

        var before = services.Single(d => d.ServiceType == typeof(IGreetingService));
        new XiHanCastleModule().PostConfigureServices(context);
        var after = services.Single(d => d.ServiceType == typeof(IGreetingService));

        Assert.NotSame(before, after);
        Assert.NotNull(after.ImplementationFactory);
    }

    /// <summary>
    /// 构造一个已登记拦截器的服务集合
    /// </summary>
    /// <returns>服务集合</returns>
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<CallLog>();
        services.AddTransient<LoggingInterceptor>();
        services.AddTransient<IGreetingService, GreetingService>();
        services.OnRegistered(context =>
        {
            if (context.ImplementationType == typeof(GreetingService))
            {
                context.Interceptors.TryAdd<LoggingInterceptor>();
            }
        });

        return services;
    }
}
