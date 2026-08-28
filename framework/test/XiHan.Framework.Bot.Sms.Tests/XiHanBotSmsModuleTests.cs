// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Messaging;
using XiHan.Framework.Bot.Sms.Stores;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.Sms.Tests;

/// <summary>
/// <see cref="XiHanBotSmsModule"/> 短信模块装配测试
/// </summary>
/// <remarks>
/// 模块本身没有业务逻辑，它的契约只有两条：声明对 Bot 主模块的依赖（否则短信提供者挂不上调度器），
/// 以及在 ConfigureServices 阶段完成短信三件套的注册。这里用真实 ServiceConfigurationContext 走一遍。
/// </remarks>
public class XiHanBotSmsModuleTests
{
    /// <summary>
    /// 模块继承框架模块基类，可被模块加载器识别
    /// </summary>
    [Fact]
    public void Module_InheritsXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanBotSmsModule());
    }

    /// <summary>
    /// 模块声明依赖 Bot 主模块，保证 Bot 内核先于短信提供者装配
    /// </summary>
    [Fact]
    public void Module_DependsOnBotModule()
    {
        var attributes = typeof(XiHanBotSmsModule).GetCustomAttributes<DependsOnAttribute>().ToList();

        Assert.Single(attributes);
        Assert.Contains(typeof(XiHanBotModule), attributes[0].GetDependedTypes());
    }

    /// <summary>
    /// ConfigureServices 完成短信配置存储、网关解析器与短信提供者的注册
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersSmsServices()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);

        new XiHanBotSmsModule().ConfigureServices(context);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<DefaultSmsConfigStore>(provider.GetRequiredService<ISmsConfigStore>());
        Assert.IsType<SmsGatewayResolver>(provider.GetRequiredService<ISmsGatewayResolver>());
        var providers = provider.GetServices<IBotProvider>().ToList();
        Assert.Single(providers);
        Assert.IsType<SmsBotProvider>(providers[0]);
    }

    /// <summary>
    /// ConfigureServices 注册短信三件套，且不夹带其它业务服务
    /// </summary>
    /// <remarks>
    /// 断言的是「这三类各登记一条」，而不是描述符总数恰好为 3。
    /// 总数不可锁：本模块还要调 AddOptions() 引入选项基础设施
    /// （配置存储依赖 IOptionsMonitor&lt;T&gt;，不引入的话容器构建期就会炸），
    /// 那几条属于 DI 框架自身、数量随运行时版本浮动，锁死会让无关升级把这条无故变红。
    /// </remarks>
    [Fact]
    public void ConfigureServices_RegistersSmsTrio()
    {
        var services = new ServiceCollection();

        new XiHanBotSmsModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Single(services.Where(item => item.ServiceType == typeof(ISmsConfigStore)).ToList());
        Assert.Single(services.Where(item => item.ServiceType == typeof(ISmsGatewayResolver)).ToList());
        Assert.Single(services.Where(item => item.ServiceType == typeof(IBotProvider)).ToList());
    }

    /// <summary>
    /// 重复执行 ConfigureServices 不会重复注册短信提供者
    /// </summary>
    [Fact]
    public void ConfigureServices_CalledTwice_RegistersProviderOnce()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanBotSmsModule();

        module.ConfigureServices(context);
        module.ConfigureServices(context);

        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<IBotProvider>());
    }
}
