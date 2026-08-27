// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 模块生命周期贡献者测试
/// </summary>
/// <remarks>
/// 四个内置贡献者各自只认一个契约接口：模块没实现对应接口就必须整体跳过而不是抛错，
/// 这是「模块可以只关心自己在乎的阶段」的实现基础。
/// 基类的四个方法默认全是空实现，派生贡献者只重写自己负责的那一侧。
/// </remarks>
public class ModuleLifecycleContributorTests
{
    /// <summary>
    /// 前置初始化贡献者只触发前置初始化钩子
    /// </summary>
    [Fact]
    public void OnPreApplicationInitializationContributor_TriggersOnlyPreHook()
    {
        var module = new MlcProbeModule();
        var contributor = new OnPreApplicationInitializationModuleLifecycleContributor();

        contributor.Initialize(CreateInitializationContext(), module);

        Assert.Equal("Pre", Assert.Single(module.Calls));
    }

    /// <summary>
    /// 初始化贡献者只触发初始化钩子
    /// </summary>
    [Fact]
    public void OnApplicationInitializationContributor_TriggersOnlyInitializationHook()
    {
        var module = new MlcProbeModule();
        var contributor = new OnApplicationInitializationModuleLifecycleContributor();

        contributor.Initialize(CreateInitializationContext(), module);

        Assert.Equal("Init", Assert.Single(module.Calls));
    }

    /// <summary>
    /// 后置初始化贡献者只触发后置初始化钩子
    /// </summary>
    [Fact]
    public void OnPostApplicationInitializationContributor_TriggersOnlyPostHook()
    {
        var module = new MlcProbeModule();
        var contributor = new OnPostApplicationInitializationModuleLifecycleContributor();

        contributor.Initialize(CreateInitializationContext(), module);

        Assert.Equal("Post", Assert.Single(module.Calls));
    }

    /// <summary>
    /// 关闭贡献者只触发关闭钩子
    /// </summary>
    [Fact]
    public void OnApplicationShutdownContributor_TriggersOnlyShutdownHook()
    {
        var module = new MlcProbeModule();
        var contributor = new OnApplicationShutdownModuleLifecycleContributor();

        contributor.Shutdown(CreateShutdownContext(), module);

        Assert.Equal("Shutdown", Assert.Single(module.Calls));
    }

    /// <summary>
    /// 初始化类贡献者的关闭入口是空实现
    /// </summary>
    [Fact]
    public void InitializationContributors_ShutdownIsNoOperation()
    {
        var module = new MlcProbeModule();

        new OnPreApplicationInitializationModuleLifecycleContributor().Shutdown(CreateShutdownContext(), module);
        new OnApplicationInitializationModuleLifecycleContributor().Shutdown(CreateShutdownContext(), module);
        new OnPostApplicationInitializationModuleLifecycleContributor().Shutdown(CreateShutdownContext(), module);

        Assert.Empty(module.Calls);
    }

    /// <summary>
    /// 关闭贡献者的初始化入口是空实现
    /// </summary>
    [Fact]
    public void ShutdownContributor_InitializeIsNoOperation()
    {
        var module = new MlcProbeModule();

        new OnApplicationShutdownModuleLifecycleContributor().Initialize(CreateInitializationContext(), module);

        Assert.Empty(module.Calls);
    }

    /// <summary>
    /// 异步入口按同样规则分发
    /// </summary>
    [Fact]
    public async Task Contributors_AsyncEntryPoints_DispatchToMatchingHook()
    {
        var module = new MlcProbeModule();

        await new OnPreApplicationInitializationModuleLifecycleContributor().InitializeAsync(CreateInitializationContext(), module);
        await new OnApplicationInitializationModuleLifecycleContributor().InitializeAsync(CreateInitializationContext(), module);
        await new OnPostApplicationInitializationModuleLifecycleContributor().InitializeAsync(CreateInitializationContext(), module);
        await new OnApplicationShutdownModuleLifecycleContributor().ShutdownAsync(CreateShutdownContext(), module);

        Assert.Equal(4, module.Calls.Count);
        Assert.Equal("Pre", module.Calls[0]);
        Assert.Equal("Init", module.Calls[1]);
        Assert.Equal("Post", module.Calls[2]);
        Assert.Equal("Shutdown", module.Calls[3]);
    }

    /// <summary>
    /// 模块未实现对应契约时贡献者整体跳过
    /// </summary>
    [Fact]
    public async Task Contributors_WhenModuleDoesNotImplementContract_DoNothing()
    {
        var module = new MlcBareModule();

        new OnPreApplicationInitializationModuleLifecycleContributor().Initialize(CreateInitializationContext(), module);
        new OnApplicationInitializationModuleLifecycleContributor().Initialize(CreateInitializationContext(), module);
        new OnPostApplicationInitializationModuleLifecycleContributor().Initialize(CreateInitializationContext(), module);
        new OnApplicationShutdownModuleLifecycleContributor().Shutdown(CreateShutdownContext(), module);
        await new OnApplicationInitializationModuleLifecycleContributor().InitializeAsync(CreateInitializationContext(), module);
        await new OnApplicationShutdownModuleLifecycleContributor().ShutdownAsync(CreateShutdownContext(), module);

        Assert.False(module.Touched);
    }

    /// <summary>
    /// 贡献者基类四个入口默认都是空实现
    /// </summary>
    [Fact]
    public async Task ContributorBase_AllEntryPointsAreNoOperations()
    {
        var module = new MlcProbeModule();
        var contributor = new MlcInertContributor();

        contributor.Initialize(CreateInitializationContext(), module);
        contributor.Shutdown(CreateShutdownContext(), module);
        await contributor.InitializeAsync(CreateInitializationContext(), module);
        await contributor.ShutdownAsync(CreateShutdownContext(), module);

        Assert.Empty(module.Calls);
    }

    /// <summary>
    /// 构建应用初始化上下文
    /// </summary>
    /// <returns>应用初始化上下文</returns>
    private static ApplicationInitializationContext CreateInitializationContext()
    {
        return new ApplicationInitializationContext(new ServiceCollection().BuildServiceProvider());
    }

    /// <summary>
    /// 构建应用关闭上下文
    /// </summary>
    /// <returns>应用关闭上下文</returns>
    private static ApplicationShutdownContext CreateShutdownContext()
    {
        return new ApplicationShutdownContext(new ServiceCollection().BuildServiceProvider());
    }
}

/// <summary>
/// 记录被触发钩子的探针模块
/// </summary>
internal sealed class MlcProbeModule : XiHanModule
{
    /// <summary>
    /// 已触发的钩子
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// 应用初始化前
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        Calls.Add("Pre");
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        Calls.Add("Init");
    }

    /// <summary>
    /// 应用初始化后
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
        Calls.Add("Post");
    }

    /// <summary>
    /// 应用关闭
    /// </summary>
    /// <param name="context">应用关闭上下文</param>
    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        Calls.Add("Shutdown");
    }
}

/// <summary>
/// 只实现模块基本契约的模块
/// </summary>
internal sealed class MlcBareModule : IXiHanModule
{
    /// <summary>
    /// 是否被触碰过
    /// </summary>
    public bool Touched { get; private set; }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public void ConfigureServices(ServiceConfigurationContext context)
    {
        Touched = true;
    }

    /// <summary>
    /// 服务配置，异步
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    /// <returns>任务</returns>
    public Task ConfigureServicesAsync(ServiceConfigurationContext context)
    {
        Touched = true;
        return Task.CompletedTask;
    }
}

/// <summary>
/// 不重写任何入口的贡献者
/// </summary>
internal sealed class MlcInertContributor : ModuleLifecycleContributorBase;
