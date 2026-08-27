// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.DingTalk.Abstractions;
using XiHan.Framework.Bot.DingTalk.Messaging;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.DingTalk.Stores;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Core.Modularity;

namespace XiHan.Framework.Bot.DingTalk.Tests;

/// <summary>
/// 曦寒框架机器人钉钉模块测试
/// </summary>
/// <remarks>
/// 模块的价值全在两处：把 Bot 主模块拉进模块图（否则调度器、渠道、模板都不存在），
/// 以及把钉钉提供者与配置存储注册进容器。依赖声明丢失不会有编译错误，只会推迟到运行期才炸，所以在这里锁死。
/// 模块自身不写入任何选项，配置由应用层通过 UseDingTalk/AddXiHanBotDingTalk 或配置文件提供。
/// </remarks>
public class XiHanBotDingTalkModuleTests
{
    /// <summary>
    /// 模块继承框架模块基类，才能被模块加载器识别
    /// </summary>
    [Fact]
    public void Module_IsXiHanModule()
    {
        Assert.True(typeof(XiHanBotDingTalkModule).IsAssignableTo(typeof(XiHanModule)));
        Assert.True(typeof(XiHanBotDingTalkModule).IsAssignableTo(typeof(IXiHanModule)));
    }

    /// <summary>
    /// 模块仅依赖机器人主模块
    /// </summary>
    [Fact]
    public void Module_DependsOnBotModule()
    {
        var attribute = typeof(XiHanBotDingTalkModule).GetCustomAttribute<DependsOnAttribute>(false);

        Assert.NotNull(attribute);
        Assert.Equal(typeof(XiHanBotModule), Assert.Single(attribute.GetDependedTypes()));
    }

    /// <summary>
    /// 服务配置注册钉钉提供者与默认配置存储
    /// </summary>
    [Fact]
    public void ConfigureServices_RegistersProviderAndConfigStore()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());
        var module = new XiHanBotDingTalkModule();

        module.ConfigureServices(context);

        Assert.Contains(
            context.Services,
            item => item.ServiceType == typeof(IDingTalkConfigStore) && item.ImplementationType == typeof(DefaultDingTalkConfigStore));
        Assert.Contains(
            context.Services,
            item => item.ServiceType == typeof(IBotProvider) && item.ImplementationType == typeof(DingTalkBotProvider));
    }

    /// <summary>
    /// 模块不代替应用层写入任何钉钉选项
    /// </summary>
    [Fact]
    public void ConfigureServices_DoesNotWriteAnyOptions()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());
        var module = new XiHanBotDingTalkModule();

        module.ConfigureServices(context);

        Assert.DoesNotContain(context.Services, item => item.ServiceType == typeof(IConfigureOptions<DingTalkOptions>));
    }

    /// <summary>
    /// 异步入口与同步入口行为一致
    /// </summary>
    [Fact]
    public async Task ConfigureServicesAsync_DelegatesToSyncOverload()
    {
        var context = new ServiceConfigurationContext(new ServiceCollection());
        var module = new XiHanBotDingTalkModule();

        await module.ConfigureServicesAsync(context);

        Assert.Contains(context.Services, item => item.ServiceType == typeof(IBotProvider));
    }

    /// <summary>
    /// 模块注册出来的服务图可被容器解析
    /// </summary>
    [Fact]
    public void ConfigureServices_ProducesResolvableGraph()
    {
        var services = new ServiceCollection();
        var context = new ServiceConfigurationContext(services);
        var module = new XiHanBotDingTalkModule();

        module.ConfigureServices(context);

        // 模块本身不写选项，容器里没有选项基础设施，需要应用层补上才能解析配置存储
        services.AddOptions();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<DefaultDingTalkConfigStore>(serviceProvider.GetRequiredService<IDingTalkConfigStore>());
        Assert.IsType<DingTalkBotProvider>(Assert.Single(serviceProvider.GetServices<IBotProvider>()));
    }
}
