// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Validation.Abstractions;

namespace XiHan.Framework.Validation.Tests;

/// <summary>
/// 曦寒框架数据校验模块的模块契约测试
/// </summary>
/// <remarks>
/// 该项目当前只承载模块装配入口，没有业务类型，因此这里锁死的是它对模块系统的契约：
/// 能被模块加载器识别并实例化、依赖声明只指向校验抽象模块且依赖链到此终止、
/// 服务配置阶段对 IConfiguration 的硬要求，以及前后置阶段与生命周期钩子的无副作用语义。
/// 这些断言不依赖模块内部实现，模块日后新增注册也只会在「注册数量」一条上失败，便于定位。
/// </remarks>
public class XiHanValidationModuleTests
{
    /// <summary>
    /// 模块类型满足模块加载器的识别与实例化前提
    /// </summary>
    [Fact]
    public void ModuleType_SatisfiesModuleLoaderContract()
    {
        var moduleType = typeof(XiHanValidationModule);

        Assert.True(XiHanModuleHelper.IsXiHanModule(moduleType));
        Assert.True(moduleType.IsSubclassOf(typeof(XiHanModule)));
        Assert.True(moduleType.IsPublic);
        Assert.False(moduleType.IsAbstract);
        Assert.False(moduleType.IsGenericType);
        // ModuleLoader 用 Activator.CreateInstance 实例化模块，公共无参构造器是硬前提
        Assert.NotNull(moduleType.GetConstructor(Type.EmptyTypes));
    }

    /// <summary>
    /// 依赖声明只指向校验抽象模块
    /// </summary>
    [Fact]
    public void DependsOn_DeclaresValidationAbstractionsModuleOnly()
    {
        var attributes = typeof(XiHanValidationModule).GetCustomAttributes<DependsOnAttribute>().ToList();

        Assert.Single(attributes);

        var dependedTypes = attributes[0].GetDependedTypes();
        Assert.Single(dependedTypes);
        Assert.Contains(typeof(XiHanValidationAbstractionsModule), dependedTypes);

        // 同一份声明经模块助手解析后必须得到一致结果，模块助手才是加载器实际使用的入口
        var resolved = XiHanModuleHelper.FindDependedModuleTypes(typeof(XiHanValidationModule));
        Assert.Single(resolved);
        Assert.Contains(typeof(XiHanValidationAbstractionsModule), resolved);
    }

    /// <summary>
    /// 依赖链在校验抽象模块处终止
    /// </summary>
    [Fact]
    public void AbstractionsModule_IsXiHanModule_AndDeclaresNoFurtherDependencies()
    {
        Assert.True(XiHanModuleHelper.IsXiHanModule(typeof(XiHanValidationAbstractionsModule)));
        Assert.Empty(XiHanModuleHelper.FindDependedModuleTypes(typeof(XiHanValidationAbstractionsModule)));
    }

    /// <summary>
    /// 以校验模块为起点展开的模块闭包恰好是自身与校验抽象模块
    /// </summary>
    [Fact]
    public void FindAllModuleTypes_ReturnsSelfAndAbstractions_WithoutDuplicates()
    {
        var moduleTypes = XiHanModuleHelper.FindAllModuleTypes(typeof(XiHanValidationModule), null);

        Assert.Equal(2, moduleTypes.Count);
        Assert.Contains(typeof(XiHanValidationModule), moduleTypes);
        Assert.Contains(typeof(XiHanValidationAbstractionsModule), moduleTypes);
        Assert.Equal(moduleTypes.Count, moduleTypes.Distinct().Count());
    }

    /// <summary>
    /// 校验模块被多条依赖路径同时引用时只加载一次
    /// </summary>
    [Fact]
    public void FindAllModuleTypes_WhenReachedByTwoPaths_LoadsValidationModuleOnce()
    {
        var moduleTypes = XiHanModuleHelper.FindAllModuleTypes(typeof(DoubleDependentHostModule), null);

        // 宿主模块同时直接依赖校验模块与校验抽象模块，后者又是前者的依赖，构成重复可达
        Assert.Equal(3, moduleTypes.Count);
        Assert.Equal(1, moduleTypes.Count(type => type == typeof(XiHanValidationModule)));
        Assert.Equal(1, moduleTypes.Count(type => type == typeof(XiHanValidationAbstractionsModule)));
    }

    /// <summary>
    /// 模块未声明附加程序集，程序集集合只含自身程序集
    /// </summary>
    [Fact]
    public void GetAllAssemblies_ReturnsOnlyOwnAssembly()
    {
        var assemblies = XiHanModuleHelper.GetAllAssemblies(typeof(XiHanValidationModule));

        Assert.Single(assemblies);
        Assert.Same(typeof(XiHanValidationModule).Assembly, assemblies[0]);
    }

    /// <summary>
    /// 服务集合中缺少配置时，服务配置阶段抛出曦寒异常
    /// </summary>
    [Fact]
    public void ConfigureServices_WhenConfigurationMissing_ThrowsXiHanException()
    {
        var module = new XiHanValidationModule();
        var context = new ServiceConfigurationContext(new ServiceCollection());

        var exception = Assert.Throws<XiHanException>(() => module.ConfigureServices(context));

        Assert.Contains("找不到", exception.Message);
        Assert.Contains("IConfiguration", exception.Message);
    }

    /// <summary>
    /// 异步服务配置阶段与同步版本抛出同一异常
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_WhenConfigurationMissing_ThrowsXiHanException()
    {
        var module = new XiHanValidationModule();
        var context = new ServiceConfigurationContext(new ServiceCollection());

        var exception = await Assert.ThrowsAsync<XiHanException>(() => module.ConfigureServicesAsync(context));

        Assert.Contains("找不到", exception.Message);
        Assert.Contains("IConfiguration", exception.Message);
    }

    /// <summary>
    /// 已注册配置时服务配置阶段不追加注册，且重复调用幂等
    /// </summary>
    [Fact]
    public void ConfigureServices_WithConfiguration_RegistersNothing_AndIsIdempotent()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanValidationModule();
        var countBefore = services.Count;

        module.ConfigureServices(context);
        var countAfterFirst = services.Count;
        module.ConfigureServices(context);

        // 校验模块目前是纯装配入口，本身不向容器追加任何注册；重复调用同样不产生副作用
        Assert.Equal(countBefore, countAfterFirst);
        Assert.Equal(countBefore, services.Count);
        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 异步服务配置阶段与同步版本产生相同的注册结果
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_WithConfiguration_MatchesSyncRegistrationResult()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanValidationModule();
        var countBefore = services.Count;

        await module.ConfigureServicesAsync(context);

        Assert.Equal(countBefore, services.Count);
        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 前置与后置配置阶段不读取配置，缺配置也不抛异常
    /// </summary>
    [Fact]
    public void PreAndPostConfigureServices_DoNotRequireConfiguration()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanValidationModule();

        module.PreConfigureServices(context);
        module.PostConfigureServices(context);

        // 只有 ConfigureServices 读取配置，前后置阶段保持空实现，这是与 ConfigureServices 的行为分界
        Assert.Empty(services);
        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 前置与后置配置阶段的异步版本同步完成且无副作用
    /// </summary>
    [Fact]
    public async Task PreAndPostConfigureServicesAsync_CompleteWithoutSideEffects()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanValidationModule();

        await module.PreConfigureServicesAsync(context);
        await module.PostConfigureServicesAsync(context);

        Assert.Empty(services);
        Assert.Empty(context.Items);
    }

    /// <summary>
    /// 应用生命周期钩子未被覆写，异步钩子同步完成
    /// </summary>
    [Fact]
    public void ApplicationLifecycleHooks_CompleteSynchronously_AndDoNotThrow()
    {
        var module = new XiHanValidationModule();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var initializationContext = new ApplicationInitializationContext(provider);
        var shutdownContext = new ApplicationShutdownContext(provider);

        module.OnPreApplicationInitialization(initializationContext);
        module.OnApplicationInitialization(initializationContext);
        module.OnPostApplicationInitialization(initializationContext);
        module.OnApplicationShutdown(shutdownContext);

        // 模块没有覆写任何钩子，异步版本应当直接返回已完成任务，不引入额外调度
        Assert.True(module.OnPreApplicationInitializationAsync(initializationContext).IsCompletedSuccessfully);
        Assert.True(module.OnApplicationInitializationAsync(initializationContext).IsCompletedSuccessfully);
        Assert.True(module.OnPostApplicationInitializationAsync(initializationContext).IsCompletedSuccessfully);
        Assert.True(module.OnApplicationShutdownAsync(shutdownContext).IsCompletedSuccessfully);
    }

    /// <summary>
    /// 创建一个已注册空配置的服务集合
    /// </summary>
    /// <returns>服务集合</returns>
    private static ServiceCollection CreateServicesWithConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        return services;
    }

    /// <summary>
    /// 同时依赖校验模块与校验抽象模块的测试宿主模块
    /// </summary>
    /// <remarks>
    /// 用于构造「校验抽象模块被两条路径可达」的场景，验证模块闭包的去重语义。
    /// </remarks>
    [DependsOn(
        typeof(XiHanValidationModule),
        typeof(XiHanValidationAbstractionsModule)
    )]
    private sealed class DoubleDependentHostModule : XiHanModule
    {
    }
}
