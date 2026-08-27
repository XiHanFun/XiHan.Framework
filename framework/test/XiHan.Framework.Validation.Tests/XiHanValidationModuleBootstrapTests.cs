// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Validation.Abstractions;

namespace XiHan.Framework.Validation.Tests;

/// <summary>
/// 曦寒框架数据校验模块的真实装配测试
/// </summary>
/// <remarks>
/// 用真实 ServiceCollection 走一遍 AddApplication 装配流程，验证校验模块进入模块系统之后的 DI 契约：
/// 模块闭包与加载顺序、依赖边、模块实例的注册生命周期与可解析性，以及校验两个包对容器的实际贡献面。
/// 这里不构造任何替身，装配失败即代表模块声明本身有问题。
/// </remarks>
public class XiHanValidationModuleBootstrapTests
{
    /// <summary>
    /// 装配后模块闭包为两个模块，且抽象模块排在校验模块之前
    /// </summary>
    [Fact]
    public void AddApplication_LoadsAbstractionsBeforeValidationModule()
    {
        var services = CreateServices();

        using var application = services.AddApplication<XiHanValidationModule>();

        Assert.Equal(typeof(XiHanValidationModule), application.StartupModuleType);
        Assert.Equal(2, application.Modules.Count);
        // 拓扑排序保证依赖先于被依赖者，启动模块被移到最后
        Assert.Equal(typeof(XiHanValidationAbstractionsModule), application.Modules[0].Type);
        Assert.Equal(typeof(XiHanValidationModule), application.Modules[1].Type);
    }

    /// <summary>
    /// 装配后校验模块的依赖边指向抽象模块，且模块实例类型正确
    /// </summary>
    [Fact]
    public void AddApplication_WiresValidationModuleDependencyEdge()
    {
        var services = CreateServices();

        using var application = services.AddApplication<XiHanValidationModule>();

        var validation = application.Modules.First(descriptor => descriptor.Type == typeof(XiHanValidationModule));
        var abstractions = application.Modules.First(descriptor => descriptor.Type == typeof(XiHanValidationAbstractionsModule));

        Assert.Single(validation.Dependencies);
        Assert.Same(abstractions, validation.Dependencies[0]);
        Assert.Empty(abstractions.Dependencies);
        Assert.IsType<XiHanValidationModule>(validation.Instance);
        Assert.IsType<XiHanValidationAbstractionsModule>(abstractions.Instance);
        Assert.False(validation.IsLoadedAsPlugIn);
        Assert.False(abstractions.IsLoadedAsPlugIn);
        Assert.Same(typeof(XiHanValidationModule).Assembly, validation.Assembly);
        Assert.Same(typeof(XiHanValidationAbstractionsModule).Assembly, abstractions.Assembly);
    }

    /// <summary>
    /// 两个模块都以单例注册且可从容器解析出装配期的同一实例
    /// </summary>
    [Fact]
    public void AddApplication_RegistersModulesAsResolvableSingletons()
    {
        var services = CreateServices();

        using var application = services.AddApplication<XiHanValidationModule>();

        // Single 同时锁死「只注册一次」，避免依赖被多路径可达时产生重复注册
        var validationDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(XiHanValidationModule));
        var abstractionsDescriptor = services.Single(descriptor => descriptor.ServiceType == typeof(XiHanValidationAbstractionsModule));
        Assert.Equal(ServiceLifetime.Singleton, validationDescriptor.Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, abstractionsDescriptor.Lifetime);

        using var provider = services.BuildServiceProvider();

        var module = provider.GetRequiredService<XiHanValidationModule>();
        var descriptorInstance = application.Modules.First(descriptor => descriptor.Type == typeof(XiHanValidationModule)).Instance;
        Assert.Same(descriptorInstance, module);
        Assert.Same(module, provider.GetRequiredService<XiHanValidationModule>());
        Assert.NotNull(provider.GetRequiredService<XiHanValidationAbstractionsModule>());
    }

    /// <summary>
    /// 模块单例在不同作用域间共享同一实例
    /// </summary>
    [Fact]
    public void AddApplication_ModuleSingletonIsSharedAcrossScopes()
    {
        var services = CreateServices();

        using var application = services.AddApplication<XiHanValidationModule>();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<XiHanValidationModule>(),
            secondScope.ServiceProvider.GetRequiredService<XiHanValidationModule>());
    }

    /// <summary>
    /// 校验两个包对容器的贡献仅限模块类型本身
    /// </summary>
    [Fact]
    public void AddApplication_ValidationPackagesContributeOnlyModuleRegistrations()
    {
        var services = CreateServices();

        using var application = services.AddApplication<XiHanValidationModule>();

        var validationAssembly = typeof(XiHanValidationModule).Assembly;
        var abstractionsAssembly = typeof(XiHanValidationAbstractionsModule).Assembly;
        var contributed = services
            .Select(descriptor => descriptor.ServiceType)
            .Where(serviceType => serviceType.Assembly == validationAssembly || serviceType.Assembly == abstractionsAssembly)
            .Distinct()
            .ToList();

        // 两个包都不含带生命周期标记的类型，常规注册扫描不应额外暴露服务（异常类与接口都不入容器）
        Assert.Equal(2, contributed.Count);
        Assert.Contains(typeof(XiHanValidationModule), contributed);
        Assert.Contains(typeof(XiHanValidationAbstractionsModule), contributed);
    }

    /// <summary>
    /// 装配后可完成初始化与关闭的完整生命周期
    /// </summary>
    [Fact]
    public void Initialize_ThenShutdown_RunsModuleLifecycleWithoutError()
    {
        var services = CreateServices();

        using var application = services.AddApplication<XiHanValidationModule>();
        using var provider = services.BuildServiceProvider();

        application.Initialize(provider);

        Assert.Same(provider, application.ServiceProvider);

        // 两个模块都没有覆写生命周期钩子，关闭流程应当静默走完而不抛出
        application.Shutdown();
    }

    /// <summary>
    /// 装配后可完成异步初始化与异步关闭的完整生命周期
    /// </summary>
    [Fact]
    public async Task InitializeAsync_ThenShutdownAsync_RunsModuleLifecycleWithoutError()
    {
        var services = CreateServices();

        using var application = services.AddApplication<XiHanValidationModule>();
        using var provider = services.BuildServiceProvider();

        await application.InitializeAsync(provider);

        Assert.Same(provider, application.ServiceProvider);

        await application.ShutdownAsync();
    }

    /// <summary>
    /// 创建一个已注册空配置的服务集合
    /// </summary>
    /// <remarks>
    /// 模块的 ConfigureServices 会调用 GetConfiguration，这里显式给一份空配置，
    /// 避免落到核心库默认的 appsettings 探测路径而受运行目录影响。
    /// </remarks>
    /// <returns>服务集合</returns>
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return services;
    }
}
