// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.MultiTenancy.Abstractions.Tests;

/// <summary>
/// 多租户抽象模块的测试
/// </summary>
/// <remarks>
/// 抽象包的模块目前不注册任何服务，价值在于「被装配时不会产生副作用」以及「异步入口确实路由到同步实现」。
/// 需要注意它在 ConfigureServices 里调用了 <c>services.GetConfiguration()</c>：
/// 这使得该模块在服务集合里缺少 <see cref="IConfiguration"/> 时会直接抛异常，测试把这条前置条件显式记录下来。
/// </remarks>
public class XiHanMultiTenancyAbstractionsModuleTests
{
    /// <summary>
    /// 模块实现了框架的模块契约
    /// </summary>
    [Fact]
    public void Module_ImplementsModuleContract()
    {
        var module = new XiHanMultiTenancyAbstractionsModule();

        Assert.IsAssignableFrom<XiHanModule>(module);
        Assert.IsAssignableFrom<IXiHanModule>(module);
    }

    /// <summary>
    /// 配置服务时不向容器追加任何注册
    /// </summary>
    /// <remarks>
    /// 抽象包只应提供契约，任何实现注册都必须落在实现包里；这里用注册数量的前后对比把这条边界钉住。
    /// </remarks>
    [Fact]
    public void ConfigureServices_WithConfigurationPresent_AddsNoServiceDescriptor()
    {
        var services = CreateServicesWithConfiguration();
        var countBefore = services.Count;
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanMultiTenancyAbstractionsModule();

        module.ConfigureServices(context);

        Assert.Equal(countBefore, services.Count);
    }

    /// <summary>
    /// 重复配置服务保持幂等，不会产生累加副作用
    /// </summary>
    [Fact]
    public void ConfigureServices_CalledTwice_IsIdempotent()
    {
        var services = CreateServicesWithConfiguration();
        var countBefore = services.Count;
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanMultiTenancyAbstractionsModule();

        module.ConfigureServices(context);
        module.ConfigureServices(context);

        Assert.Equal(countBefore, services.Count);
    }

    /// <summary>
    /// 配置服务不改动上下文的共享条目字典
    /// </summary>
    [Fact]
    public void ConfigureServices_DoesNotTouchSharedItems()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanMultiTenancyAbstractionsModule();

        module.ConfigureServices(context);

        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 异步入口同样不向容器追加任何注册
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_WithConfigurationPresent_AddsNoServiceDescriptor()
    {
        var services = CreateServicesWithConfiguration();
        var countBefore = services.Count;
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanMultiTenancyAbstractionsModule();

        await module.ConfigureServicesAsync(context);

        Assert.Equal(countBefore, services.Count);
    }

    /// <summary>
    /// 服务集合中缺少配置时同步入口抛出框架异常
    /// </summary>
    /// <remarks>
    /// 这是当前实现的既有前置条件（见「疑似缺陷」：模块取到配置后并未使用），
    /// 用例把它记录成显式契约，一旦实现改为不再强依赖配置，这条断言会立刻暴露出来供复核。
    /// </remarks>
    [Fact]
    public void ConfigureServices_WithoutConfiguration_ThrowsXiHanException()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());
        var module = new XiHanMultiTenancyAbstractionsModule();

        Assert.Throws<XiHanException>(() => module.ConfigureServices(context));
    }

    /// <summary>
    /// 服务集合中缺少配置时异步入口同样抛出框架异常
    /// </summary>
    /// <remarks>
    /// 基类的异步入口只是转调同步方法，这里用「异步入口抛出同一个异常」反证这条转调链没有被断开。
    /// </remarks>
    [Fact]
    public async Task ConfigureServicesAsync_WithoutConfiguration_ThrowsXiHanException()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());
        var module = new XiHanMultiTenancyAbstractionsModule();

        await Assert.ThrowsAsync<XiHanException>(() => module.ConfigureServicesAsync(context));
    }

    /// <summary>
    /// 未被覆写的生命周期钩子保持空实现，不产生副作用
    /// </summary>
    [Fact]
    public void Module_DoesNotOverrideOtherLifecycleHooks()
    {
        var moduleType = typeof(XiHanMultiTenancyAbstractionsModule);

        Assert.NotNull(moduleType.GetMethod(nameof(XiHanMultiTenancyAbstractionsModule.ConfigureServices)));
        Assert.Equal(moduleType, moduleType.GetMethod(nameof(XiHanMultiTenancyAbstractionsModule.ConfigureServices))!.DeclaringType);
        Assert.NotEqual(moduleType, moduleType.GetMethod(nameof(XiHanMultiTenancyAbstractionsModule.PreConfigureServices))!.DeclaringType);
        Assert.NotEqual(moduleType, moduleType.GetMethod(nameof(XiHanMultiTenancyAbstractionsModule.PostConfigureServices))!.DeclaringType);
        Assert.NotEqual(moduleType, moduleType.GetMethod(nameof(XiHanMultiTenancyAbstractionsModule.OnApplicationInitialization))!.DeclaringType);
        Assert.NotEqual(moduleType, moduleType.GetMethod(nameof(XiHanMultiTenancyAbstractionsModule.OnApplicationShutdown))!.DeclaringType);
    }

    /// <summary>
    /// 创建已注册配置的服务集合
    /// </summary>
    /// <returns>服务集合</returns>
    private static IServiceCollection CreateServicesWithConfiguration()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton(configuration);
        return services;
    }
}
