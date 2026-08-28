// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using XiHan.Framework.Bot.Lark.Abstractions;
using XiHan.Framework.Bot.Lark.Messaging;
using XiHan.Framework.Bot.Lark.Options;
using XiHan.Framework.Bot.Lark.Stores;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.Lark.Tests;

/// <summary>
/// 曦寒框架机器人飞书模块测试
/// </summary>
/// <remarks>
/// 模块只做两件事：声明对 Bot 主模块的依赖、把服务注册委托给 AddXiHanBotLark。
/// 依赖声明漏了会导致 Bot 内核（调度器/渠道/模板）没被装配，飞书提供者注册了也无人调用，
/// 所以依赖特性与注册结果都要断言。
/// </remarks>
public class XiHanBotLarkModuleTests
{
    /// <summary>
    /// 模块继承自框架模块基类
    /// </summary>
    [Fact]
    public void Module_Always_DerivesFromXiHanModule()
    {
        Assert.True(typeof(XiHanBotLarkModule).IsSubclassOf(typeof(XiHanModule)));
    }

    /// <summary>
    /// 模块声明依赖 Bot 主模块
    /// </summary>
    [Fact]
    public void Module_Always_DependsOnBotModule()
    {
        var attribute = Assert.Single(typeof(XiHanBotLarkModule).GetCustomAttributes<DependsOnAttribute>(false));

        Assert.Contains(typeof(XiHanBotModule), attribute.GetDependedTypes());
    }

    /// <summary>
    /// 模块服务配置注册飞书配置存储与提供者
    /// </summary>
    [Fact]
    public void ConfigureServices_Always_RegistersLarkStoreAndProvider()
    {
        var services = new ServiceCollection();

        new XiHanBotLarkModule().ConfigureServices(new ServiceConfigurationContext(services));

        var store = Assert.Single(services, item => item.ServiceType == typeof(ILarkConfigStore));
        var provider = Assert.Single(services, item => item.ServiceType == typeof(IBotProvider));

        Assert.Equal(typeof(DefaultLarkConfigStore), store.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, store.Lifetime);
        Assert.Equal(typeof(LarkBotProvider), provider.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, provider.Lifetime);
    }

    /// <summary>
    /// 模块装配路径不写入任何飞书选项配置
    /// </summary>
    /// <remarks>
    /// 模块调用的是无参重载，选项应完全交给应用层的 Configure / 配置文件绑定，
    /// 模块里若偷偷塞默认配置会覆盖应用层的绑定顺序。
    /// </remarks>
    [Fact]
    public void ConfigureServices_Always_DoesNotWriteOptions()
    {
        var services = new ServiceCollection();

        new XiHanBotLarkModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IConfigureOptions<LarkOptions>));
    }

    /// <summary>
    /// 重复执行服务配置保持幂等
    /// </summary>
    [Fact]
    public void ConfigureServices_CalledTwice_RegistersEachServiceOnce()
    {
        var services = new ServiceCollection();
        var module = new XiHanBotLarkModule();

        module.ConfigureServices(new ServiceConfigurationContext(services));
        module.ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Single(services, item => item.ServiceType == typeof(ILarkConfigStore));
        Assert.Single(services, item => item.ServiceType == typeof(IBotProvider));
    }

    /// <summary>
    /// 模块注册后补上选项即可解析出飞书提供者
    /// </summary>
    /// <remarks>
    /// 模拟真实装配顺序：模块先注册服务，应用层再绑定选项，最终依赖链必须是通的。
    /// </remarks>
    [Fact]
    public void ConfigureServices_WhenOptionsBoundAfterwards_ResolvesLarkProvider()
    {
        var services = new ServiceCollection();

        new XiHanBotLarkModule().ConfigureServices(new ServiceConfigurationContext(services));
        services.Configure<LarkOptions>(options => options.AccessToken = "abc-token");

        using var serviceProvider = services.BuildServiceProvider();
        var botProvider = Assert.Single(serviceProvider.GetServices<IBotProvider>());

        Assert.True(botProvider is LarkBotProvider);
        Assert.Equal("Lark", botProvider.Name);
    }
}
