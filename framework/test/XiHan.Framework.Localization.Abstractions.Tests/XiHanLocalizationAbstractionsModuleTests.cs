// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Localization.Abstractions.Tests;

/// <summary>
/// 本地化抽象模块测试
/// </summary>
/// <remarks>
/// 抽象包不注册任何服务，模块的实际契约只有两条：能被模块系统识别；装配过程不往容器里塞东西。
/// 另外它在 ConfigureServices 里读了一次配置，因此对"服务集合中已存在 IConfiguration"存在硬依赖，一并锁住。
/// </remarks>
public class XiHanLocalizationAbstractionsModuleTests
{
    /// <summary>
    /// 模块必须被模块系统识别为曦寒模块
    /// </summary>
    [Fact]
    public void Module_IsRecognizedAsXiHanModule()
    {
        var module = new XiHanLocalizationAbstractionsModule();

        Assert.IsAssignableFrom<XiHanModule>(module);
        Assert.IsAssignableFrom<IXiHanModule>(module);
    }

    /// <summary>
    /// 抽象包不得向容器注册任何服务
    /// </summary>
    [Fact]
    public void ConfigureServices_WhenConfigurationRegistered_RegistersNothing()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        var countBefore = services.Count;
        var module = new XiHanLocalizationAbstractionsModule();

        module.ConfigureServices(context);

        Assert.Equal(countBefore, services.Count);
    }

    /// <summary>
    /// 异步入口与同步入口行为一致，同样不注册服务
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_WhenConfigurationRegistered_RegistersNothing()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        var countBefore = services.Count;
        var module = new XiHanLocalizationAbstractionsModule();

        await module.ConfigureServicesAsync(context);

        Assert.Equal(countBefore, services.Count);
    }

    /// <summary>
    /// 服务集合中缺少 IConfiguration 时装配失败并抛出框架异常
    /// </summary>
    [Fact]
    public void ConfigureServices_WhenConfigurationMissing_ThrowsXiHanException()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());
        var module = new XiHanLocalizationAbstractionsModule();

        var exception = Assert.Throws<XiHanException>(() => module.ConfigureServices(context));

        Assert.Contains("IConfiguration", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 异步入口在缺少配置时同样抛出框架异常
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_WhenConfigurationMissing_ThrowsXiHanException()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());
        var module = new XiHanLocalizationAbstractionsModule();

        await Assert.ThrowsAsync<XiHanException>(() => module.ConfigureServicesAsync(context));
    }

    /// <summary>
    /// 装配上下文的服务集合必须是传入的同一个实例，模块不得替换容器
    /// </summary>
    [Fact]
    public void ConfigureServices_KeepsSameServiceCollectionInstance()
    {
        var services = CreateServicesWithConfiguration();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanLocalizationAbstractionsModule();

        module.ConfigureServices(context);

        Assert.Same(services, context.Services);
    }

    /// <summary>
    /// 构造一个已登记空配置的服务集合
    /// </summary>
    /// <returns>服务集合</returns>
    private static ServiceCollection CreateServicesWithConfiguration()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton(configuration);
        return services;
    }
}
