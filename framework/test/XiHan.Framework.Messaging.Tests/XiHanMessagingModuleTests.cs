// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Options;
using XiHan.Framework.Messaging.Services;

namespace XiHan.Framework.Messaging.Tests;

/// <summary>
/// 曦寒框架消息模块测试
/// </summary>
/// <remarks>
/// 模块本身只是把注册扩展挂到模块化生命周期上，所以这里验证的是「挂对了」：
/// 走模块装配得到的服务清单必须与直接调用注册扩展一致，且模块可重复装配。
/// </remarks>
public class XiHanMessagingModuleTests
{
    /// <summary>
    /// 模块继承自曦寒模块基类
    /// </summary>
    [Fact]
    public void Module_IsXiHanModule()
    {
        Assert.IsAssignableFrom<XiHanModule>(new XiHanMessagingModule());
    }

    /// <summary>
    /// 服务配置阶段注册调度器与兜底发送器
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersDispatcherAndFallbackSender()
    {
        var services = new ServiceCollection();
        var module = new XiHanMessagingModule();

        module.ConfigureServices(new ServiceConfigurationContext(services));

        var dispatcher = Assert.Single(services.Where(item => item.ServiceType == typeof(IMessageDispatcher)).ToArray());
        var sender = Assert.Single(services.Where(item => item.ServiceType == typeof(IMessageSender)).ToArray());

        Assert.Equal(typeof(DefaultMessageDispatcher), dispatcher.ImplementationType);
        Assert.Equal(typeof(NotConfiguredMessageSender), sender.ImplementationType);
    }

    /// <summary>
    /// 服务配置阶段注册默认配置
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();

        new XiHanMessagingModule().ConfigureServices(new ServiceConfigurationContext(services));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XiHanMessagingOptions>>().Value;

        Assert.True(options.ContinueOnError);
        Assert.False(options.ThrowWhenNoSender);
    }

    /// <summary>
    /// 重复装配模块不会产生重复注册
    /// </summary>
    [Fact]
    public void ConfigureServices_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanMessagingModule();

        module.ConfigureServices(context);
        module.ConfigureServices(context);

        Assert.Single(services.Where(item => item.ServiceType == typeof(IMessageDispatcher)).ToArray());
        Assert.Single(services.Where(item => item.ServiceType == typeof(IMessageSender)).ToArray());
    }
}
