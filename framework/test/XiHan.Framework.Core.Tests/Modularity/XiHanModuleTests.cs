// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Application;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 曦寒模块基类测试
/// </summary>
/// <remarks>
/// 模块基类同时实现了配置与应用生命周期的全部契约接口，生命周期贡献者靠 is 判断决定要不要回调，
/// 因此「基类实现了哪些接口」本身就是对外协议；异步入口一律转调同步入口，模块只重写一侧即可。
/// 服务配置上下文只在配置阶段有效，越界访问必须显式报错而不是返回空。
/// </remarks>
public class XiHanModuleTests
{
    /// <summary>
    /// 模块基类实现全部生命周期契约
    /// </summary>
    [Fact]
    public void XiHanModule_ImplementsEveryLifecycleContract()
    {
        var module = new XmProbeModule();

        Assert.IsAssignableFrom<IXiHanModule>(module);
        Assert.IsAssignableFrom<IPreConfigureServices>(module);
        Assert.IsAssignableFrom<IPostConfigureServices>(module);
        Assert.IsAssignableFrom<IOnPreApplicationInitialization>(module);
        Assert.IsAssignableFrom<IOnApplicationInitialization>(module);
        Assert.IsAssignableFrom<IOnPostApplicationInitialization>(module);
        Assert.IsAssignableFrom<IOnApplicationShutdown>(module);
    }

    /// <summary>
    /// 配置阶段的异步入口转调同步入口
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_DelegatesToSynchronousOverload()
    {
        var module = new XmProbeModule();
        var context = new ServiceConfigurationContext(new ServiceCollection());

        await module.PreConfigureServicesAsync(context);
        await module.ConfigureServicesAsync(context);
        await module.PostConfigureServicesAsync(context);

        Assert.Equal(3, module.Calls.Count);
        Assert.Equal(nameof(XiHanModule.PreConfigureServices), module.Calls[0]);
        Assert.Equal(nameof(XiHanModule.ConfigureServices), module.Calls[1]);
        Assert.Equal(nameof(XiHanModule.PostConfigureServices), module.Calls[2]);
    }

    /// <summary>
    /// 应用初始化阶段的异步入口转调同步入口
    /// </summary>
    [Fact]
    public async Task InitializationAsync_DelegatesToSynchronousOverload()
    {
        var module = new XmProbeModule();
        IServiceCollection services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var context = new ApplicationInitializationContext(provider);

        await module.OnPreApplicationInitializationAsync(context);
        await module.OnApplicationInitializationAsync(context);
        await module.OnPostApplicationInitializationAsync(context);

        Assert.Equal(3, module.Calls.Count);
        Assert.Equal(nameof(XiHanModule.OnPreApplicationInitialization), module.Calls[0]);
        Assert.Equal(nameof(XiHanModule.OnApplicationInitialization), module.Calls[1]);
        Assert.Equal(nameof(XiHanModule.OnPostApplicationInitialization), module.Calls[2]);
    }

    /// <summary>
    /// 应用关闭的异步入口转调同步入口
    /// </summary>
    [Fact]
    public async Task OnApplicationShutdownAsync_DelegatesToSynchronousOverload()
    {
        var module = new XmProbeModule();
        IServiceCollection services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        await module.OnApplicationShutdownAsync(new ApplicationShutdownContext(provider));

        Assert.Equal(nameof(XiHanModule.OnApplicationShutdown), Assert.Single(module.Calls));
    }

    /// <summary>
    /// 未重写任何钩子的模块调用全部入口都不抛出
    /// </summary>
    [Fact]
    public async Task DefaultHooks_AreNoOperations()
    {
        var module = new XmBareModule();
        IServiceCollection services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var configurationContext = new ServiceConfigurationContext(services);
        var initializationContext = new ApplicationInitializationContext(provider);

        await module.PreConfigureServicesAsync(configurationContext);
        await module.ConfigureServicesAsync(configurationContext);
        await module.PostConfigureServicesAsync(configurationContext);
        await module.OnPreApplicationInitializationAsync(initializationContext);
        await module.OnApplicationInitializationAsync(initializationContext);
        await module.OnPostApplicationInitializationAsync(initializationContext);
        await module.OnApplicationShutdownAsync(new ApplicationShutdownContext(provider));

        Assert.Empty(configurationContext.Items);
    }

    /// <summary>
    /// 配置阶段之外访问服务配置上下文时抛出
    /// </summary>
    [Fact]
    public void ServiceConfigurationContext_WhenAccessedOutsideConfiguration_Throws()
    {
        var module = new XmProbeModule();

        var exception = Assert.Throws<XiHanException>(() => module.ReadServiceConfigurationContext());

        Assert.Contains(nameof(XiHanModule.ConfigureServices), exception.Message);
    }

    /// <summary>
    /// 默认不跳过自动服务注册且子类可关闭
    /// </summary>
    [Fact]
    public void SkipAutoServiceRegistration_DefaultsToFalseAndIsSettableByModule()
    {
        var module = new XmProbeModule();

        Assert.False(module.ReadSkipAutoServiceRegistration());

        module.MarkSkipAutoServiceRegistration();

        Assert.True(module.ReadSkipAutoServiceRegistration());
    }
}

/// <summary>
/// 记录钩子调用的探针模块
/// </summary>
internal sealed class XmProbeModule : XiHanModule
{
    /// <summary>
    /// 已记录的钩子调用
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// 服务配置前
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        Calls.Add(nameof(PreConfigureServices));
    }

    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Calls.Add(nameof(ConfigureServices));
    }

    /// <summary>
    /// 服务配置后
    /// </summary>
    /// <param name="context">服务配置上下文</param>
    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        Calls.Add(nameof(PostConfigureServices));
    }

    /// <summary>
    /// 应用初始化前
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        Calls.Add(nameof(OnPreApplicationInitialization));
    }

    /// <summary>
    /// 应用初始化
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        Calls.Add(nameof(OnApplicationInitialization));
    }

    /// <summary>
    /// 应用初始化后
    /// </summary>
    /// <param name="context">应用初始化上下文</param>
    public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
        Calls.Add(nameof(OnPostApplicationInitialization));
    }

    /// <summary>
    /// 应用关闭
    /// </summary>
    /// <param name="context">应用关闭上下文</param>
    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        Calls.Add(nameof(OnApplicationShutdown));
    }

    /// <summary>
    /// 读取服务配置上下文
    /// </summary>
    /// <returns>服务配置上下文</returns>
    public ServiceConfigurationContext ReadServiceConfigurationContext()
    {
        return ServiceConfigurationContext;
    }

    /// <summary>
    /// 读取是否跳过自动服务注册
    /// </summary>
    /// <returns>是否跳过</returns>
    public bool ReadSkipAutoServiceRegistration()
    {
        return SkipAutoServiceRegistration;
    }

    /// <summary>
    /// 声明跳过自动服务注册
    /// </summary>
    public void MarkSkipAutoServiceRegistration()
    {
        SkipAutoServiceRegistration = true;
    }
}

/// <summary>
/// 不重写任何钩子的空模块
/// </summary>
internal sealed class XmBareModule : XiHanModule;
