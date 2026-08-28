// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.WeCom.Abstractions;
using XiHan.Framework.Bot.WeCom.Extensions.DependencyInjection;
using XiHan.Framework.Bot.WeCom.Messaging;
using XiHan.Framework.Bot.WeCom.Options;
using XiHan.Framework.Bot.WeCom.Stores;
using XiHan.Framework.Bot.WeCom.Tests.Fakes;

namespace XiHan.Framework.Bot.WeCom.Tests.Extensions.DependencyInjection;

/// <summary>
/// <see cref="XiHanBotWeComServiceCollectionExtensions"/> 服务注册测试
/// </summary>
/// <remarks>
/// 注册契约有四条：默认配置存储与提供者都是单例；提供者以 TryAddEnumerable 注册因而可重复调用而不重复注册；
/// 配置存储以 TryAdd 注册因而可被应用层实现顶掉；不传配置委托时不往选项系统写任何东西。
/// </remarks>
public class XiHanBotWeComServiceCollectionExtensionsTests
{
    /// <summary>
    /// 返回同一个服务集合以支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanBotWeCom_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanBotWeCom();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 默认配置存储以单例注册且可解析
    /// </summary>
    [Fact]
    public void AddXiHanBotWeCom_RegistersDefaultConfigStoreAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotWeCom(options => options.Key = "k");

        var descriptor = services.Single(item => item.ServiceType == typeof(IWeComConfigStore));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(DefaultWeComConfigStore), descriptor.ImplementationType);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<DefaultWeComConfigStore>(provider.GetRequiredService<IWeComConfigStore>());
    }

    /// <summary>
    /// 企业微信提供者以单例加入提供者集合
    /// </summary>
    [Fact]
    public void AddXiHanBotWeCom_RegistersWeComBotProviderAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotWeCom(options => options.Key = "k");

        var descriptor = services.Single(item => item.ServiceType == typeof(IBotProvider));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(WeComBotProvider), descriptor.ImplementationType);

        using var provider = services.BuildServiceProvider();
        var botProvider = Assert.IsType<WeComBotProvider>(provider.GetServices<IBotProvider>().Single());
        Assert.Equal("WeCom", botProvider.Name);
    }

    /// <summary>
    /// 提供者单例在多次解析间保持同一实例
    /// </summary>
    [Fact]
    public void AddXiHanBotWeCom_ProviderInstance_IsSharedAcrossResolutions()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotWeCom(options => options.Key = "k");

        using var provider = services.BuildServiceProvider();

        var first = provider.GetServices<IBotProvider>().Single();
        var second = provider.GetServices<IBotProvider>().Single();

        Assert.Same(first, second);
    }

    /// <summary>
    /// 重复调用不会注册出第二个企业微信提供者
    /// </summary>
    [Fact]
    public void AddXiHanBotWeCom_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotWeCom(options => options.Key = "k1");
        services.AddXiHanBotWeCom(options => options.Key = "k2");

        Assert.Single(services, item => item.ServiceType == typeof(IBotProvider));
        Assert.Single(services, item => item.ServiceType == typeof(IWeComConfigStore));
    }

    /// <summary>
    /// 配置委托写入选项系统，多次调用按注册顺序叠加
    /// </summary>
    [Fact]
    public void AddXiHanBotWeCom_AppliesConfigureDelegatesInOrder()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotWeCom(options => options.Key = "first");
        services.AddXiHanBotWeCom(options => options.WebHookUrl = "https://proxy.internal/webhook/send");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<WeComOptions>>().Value;

        Assert.Equal("first", options.Key);
        Assert.Equal("https://proxy.internal/webhook/send", options.WebHookUrl);
    }

    /// <summary>
    /// 不传配置委托时不往选项系统写入任何配置动作
    /// </summary>
    [Fact]
    public void AddXiHanBotWeCom_WithoutConfigure_DoesNotRegisterOptionsConfiguration()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotWeCom();

        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IConfigureOptions<WeComOptions>));
    }

    /// <summary>
    /// 应用层已注册的配置存储不会被默认实现覆盖
    /// </summary>
    /// <remarks>
    /// 默认实现只是 IOptionsMonitor 兜底，数据库版配置存储必须能顶掉它，这是模块的显式设计意图。
    /// </remarks>
    [Fact]
    public void AddXiHanBotWeCom_WhenCustomConfigStoreRegistered_KeepsCustomImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWeComConfigStore>(new FakeWeComConfigStore(new WeComOptions()));

        services.AddXiHanBotWeCom(options => options.Key = "k");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakeWeComConfigStore>(provider.GetRequiredService<IWeComConfigStore>());
    }

    /// <summary>
    /// 服务集合为空时抛出参数空异常
    /// </summary>
    [Fact]
    public void AddXiHanBotWeCom_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => XiHanBotWeComServiceCollectionExtensions.AddXiHanBotWeCom(null!));

        Assert.Equal("services", exception.ParamName);
    }
}
