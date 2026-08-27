// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Threading.Tests;

/// <summary>
/// 线程模块装配测试
/// </summary>
/// <remarks>
/// 模块只做一件事：把线程基础服务登记进服务集合。
/// 同步与异步两个入口都要覆盖，因为异步入口是基类的模板方法，必须确认它确实转发到了本模块的同步实现。
/// </remarks>
public class XiHanThreadingModuleTests
{
    /// <summary>
    /// 模块派生自曦寒模块基类
    /// </summary>
    [Fact]
    public void Module_IsXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanThreadingModule());
    }

    /// <summary>
    /// 同步配置入口登记令牌提供者与开放泛型作用域提供者
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersThreadingServices()
    {
        var services = new ServiceCollection();

        new XiHanThreadingModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICancellationTokenProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAmbientScopeProvider<>));
    }

    /// <summary>
    /// 异步配置入口转发到同步实现，登记结果一致
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_RegistersSameServices()
    {
        var services = new ServiceCollection();

        await new XiHanThreadingModule().ConfigureServicesAsync(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICancellationTokenProvider));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAmbientScopeProvider<>));
    }

    /// <summary>
    /// 模块登记的服务在补齐环境数据上下文后可以完整解析
    /// </summary>
    /// <remarks>
    /// 模块本身不登记环境数据上下文，该登记由约定注册按单例依赖标记补齐，
    /// 所以这里手工补上后再构建容器，验证的是模块登记部分的可解析性。
    /// </remarks>
    [Fact]
    public void ConfigureServices_RegisteredServices_AreResolvable()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAmbientDataContext, AsyncLocalAmbientDataContext>();

        new XiHanThreadingModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Same(
            NullCancellationTokenProvider.Instance,
            serviceProvider.GetRequiredService<ICancellationTokenProvider>());
        Assert.IsType<AmbientDataContextAmbientScopeProvider<CancellationTokenOverride>>(
            serviceProvider.GetRequiredService<IAmbientScopeProvider<CancellationTokenOverride>>());
    }
}
