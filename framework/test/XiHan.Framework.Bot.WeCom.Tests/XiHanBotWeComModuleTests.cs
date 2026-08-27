// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.WeCom.Abstractions;
using XiHan.Framework.Bot.WeCom.Messaging;
using XiHan.Framework.Bot.WeCom.Stores;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.WeCom.Tests;

/// <summary>
/// <see cref="XiHanBotWeComModule"/> 模块装配测试
/// </summary>
/// <remarks>
/// 模块本身只有两件事：声明对机器人主模块的依赖，以及在服务配置阶段把企业微信的注册扩展接进容器。
/// 依赖声明漏了会导致主模块未初始化时企业微信提供者拿不到 Bot 内核服务，因此单独断言。
/// </remarks>
public class XiHanBotWeComModuleTests
{
    /// <summary>
    /// 显式依赖机器人主模块
    /// </summary>
    [Fact]
    public void Module_DeclaresDependencyOnBotModule()
    {
        var dependedTypes = typeof(XiHanBotWeComModule)
            .GetCustomAttributes<DependsOnAttribute>(false)
            .SelectMany(attribute => attribute.GetDependedTypes())
            .ToArray();

        Assert.Contains(typeof(XiHanBotModule), dependedTypes);
    }

    /// <summary>
    /// 模块本身是可实例化的具体模块
    /// </summary>
    [Fact]
    public void Module_IsConcreteXiHanModule()
    {
        var module = new XiHanBotWeComModule();

        Assert.IsAssignableFrom<XiHanModule>(module);
    }

    /// <summary>
    /// 服务配置阶段注册企业微信提供者与默认配置存储
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersWeComProviderAndConfigStore()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);

        new XiHanBotWeComModule().ConfigureServices(context);

        Assert.Contains(services, item => item.ServiceType == typeof(IWeComConfigStore)
            && item.ImplementationType == typeof(DefaultWeComConfigStore));
        Assert.Contains(services, item => item.ServiceType == typeof(IBotProvider)
            && item.ImplementationType == typeof(WeComBotProvider));
    }
}
