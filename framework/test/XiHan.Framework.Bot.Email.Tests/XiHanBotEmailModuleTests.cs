// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Email.Abstractions;
using XiHan.Framework.Bot.Email.Messaging;
using XiHan.Framework.Bot.Email.Stores;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.Email.Tests;

/// <summary>
/// <see cref="XiHanBotEmailModule"/> 模块装配测试
/// </summary>
/// <remarks>
/// 模块只做两件事：声明对机器人主模块的依赖、把注册委托给 AddXiHanBotEmail。
/// 依赖声明不能丢——选项系统（AddOptions）由主模块引入，缺了它默认配置存储无法解析。
/// </remarks>
public class XiHanBotEmailModuleTests
{
    /// <summary>
    /// 模块继承自 XiHanModule
    /// </summary>
    [Fact]
    public void Module_InheritsXiHanModule()
    {
        Assert.True(typeof(XiHanModule).IsAssignableFrom(typeof(XiHanBotEmailModule)));
    }

    /// <summary>
    /// 模块声明依赖机器人主模块
    /// </summary>
    [Fact]
    public void Module_DependsOnBotModule()
    {
        var attributes = typeof(XiHanBotEmailModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToList();

        Assert.NotEmpty(attributes);
        var dependedTypes = attributes.SelectMany(attribute => attribute.GetDependedTypes()).ToList();
        Assert.Contains(typeof(XiHanBotModule), dependedTypes);
    }

    /// <summary>
    /// 服务配置阶段注册默认配置存储与邮件提供者
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersConfigStoreAndProvider()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);

        new XiHanBotEmailModule().ConfigureServices(context);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IEmailConfigStore) &&
            descriptor.ImplementationType == typeof(DefaultEmailConfigStore));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IBotProvider) &&
            descriptor.ImplementationType == typeof(EmailBotProvider));
    }

    /// <summary>
    /// 服务配置写入的是上下文自带的服务集合
    /// </summary>
    [Fact]
    public void ConfigureServices_WritesIntoContextServices()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);

        new XiHanBotEmailModule().ConfigureServices(context);

        Assert.Same(services, context.Services);
        Assert.NotEmpty(services);
    }

    /// <summary>
    /// 配合选项基础设施后可解析出邮件提供者
    /// </summary>
    /// <remarks>
    /// 模块调用的是无参重载，不写入任何选项配置；选项系统由主模块的 AddXiHanBot 引入，
    /// 这里用 AddOptions 等价替代，避免把整条主模块依赖链拉进单元测试。
    /// </remarks>
    [Fact]
    public async Task ConfigureServices_WithOptionsInfrastructure_ResolvesProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        new XiHanBotEmailModule().ConfigureServices(new ServiceConfigurationContext(services));

        await using var serviceProvider = services.BuildServiceProvider();

        var providers = serviceProvider.GetServices<IBotProvider>().ToList();
        Assert.Single(providers);
        Assert.IsType<EmailBotProvider>(providers[0]);
        var options = await serviceProvider.GetRequiredService<IEmailConfigStore>().GetAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(options);
        Assert.True(options.Enabled);
    }
}
