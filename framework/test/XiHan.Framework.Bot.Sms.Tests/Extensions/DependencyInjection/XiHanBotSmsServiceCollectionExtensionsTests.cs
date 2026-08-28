// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Sms.Abstractions;
using XiHan.Framework.Bot.Sms.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Sms.Messaging;
using XiHan.Framework.Bot.Sms.Stores;
using XiHan.Framework.Bot.Sms.Tests.Fakes;

namespace XiHan.Framework.Bot.Sms.Tests.Extensions.DependencyInjection;

/// <summary>
/// <see cref="XiHanBotSmsServiceCollectionExtensions"/> 短信 Bot 服务注册扩展测试
/// </summary>
/// <remarks>
/// 注册全是 TryAdd 语义：应用层必须能用数据库实现覆盖默认的空配置存储，
/// 否则短信永远发不出去。这里用真实 ServiceCollection 与真实容器验证解析结果与生命周期。
/// </remarks>
public class XiHanBotSmsServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为 null 时抛 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddXiHanBotSms_WhenServicesNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => XiHanBotSmsServiceCollectionExtensions.AddXiHanBotSms(null!));
    }

    /// <summary>
    /// 返回原服务集合本身，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanBotSms_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddXiHanBotSms();

        Assert.Same(services, returned);
    }

    /// <summary>
    /// 默认配置存储注册为单例，实现类型为 DefaultSmsConfigStore
    /// </summary>
    [Fact]
    public void AddXiHanBotSms_RegistersDefaultConfigStoreAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotSms();

        var descriptors = services.Where(item => item.ServiceType == typeof(ISmsConfigStore)).ToList();
        var item = Assert.Single(descriptors);
        Assert.Equal(ServiceLifetime.Singleton, item.Lifetime);
        Assert.Equal(typeof(DefaultSmsConfigStore), descriptors[0].ImplementationType);
    }

    /// <summary>
    /// 网关解析器注册为单例，实现类型为 SmsGatewayResolver
    /// </summary>
    /// <remarks>
    /// 解析器内部持有客户端缓存字典，必须是单例，否则每次注入都清空缓存、反复重建云 SDK 客户端。
    /// </remarks>
    [Fact]
    public void AddXiHanBotSms_RegistersGatewayResolverAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotSms();

        var descriptors = services.Where(item => item.ServiceType == typeof(ISmsGatewayResolver)).ToList();
        var item = Assert.Single(descriptors);
        Assert.Equal(ServiceLifetime.Singleton, item.Lifetime);
        Assert.Equal(typeof(SmsGatewayResolver), descriptors[0].ImplementationType);
    }

    /// <summary>
    /// 短信提供者以可枚举方式注册，与其他渠道提供者共存
    /// </summary>
    [Fact]
    public void AddXiHanBotSms_RegistersSmsBotProviderAsEnumerable()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotSms();

        var descriptors = services.Where(item => item.ServiceType == typeof(IBotProvider)).ToList();
        var item = Assert.Single(descriptors);
        Assert.Equal(ServiceLifetime.Singleton, item.Lifetime);
        Assert.Equal(typeof(SmsBotProvider), descriptors[0].ImplementationType);
    }

    /// <summary>
    /// 注册后三项服务都能从真实容器解析出来，且单例复用同一实例
    /// </summary>
    [Fact]
    public void AddXiHanBotSms_ResolvesAllServicesAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotSms();
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<ISmsConfigStore>();
        var resolver = provider.GetRequiredService<ISmsGatewayResolver>();
        var providers = provider.GetServices<IBotProvider>().ToList();

        Assert.IsType<DefaultSmsConfigStore>(store);
        Assert.IsType<SmsGatewayResolver>(resolver);
        var item = Assert.Single(providers);
        Assert.IsType<SmsBotProvider>(item);
        Assert.Same(store, provider.GetRequiredService<ISmsConfigStore>());
        Assert.Same(resolver, provider.GetRequiredService<ISmsGatewayResolver>());
    }

    /// <summary>
    /// 应用层已注册的配置存储不被默认实现覆盖（TryAdd 语义）
    /// </summary>
    [Fact]
    public void AddXiHanBotSms_KeepsPreRegisteredConfigStore()
    {
        var custom = new FakeSmsConfigStore();
        var services = new ServiceCollection();
        services.AddSingleton<ISmsConfigStore>(custom);

        services.AddXiHanBotSms();

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<ISmsConfigStore>());
    }

    /// <summary>
    /// 重复注册不会产生重复的短信提供者，避免同一条短信被发两次
    /// </summary>
    [Fact]
    public void AddXiHanBotSms_CalledTwice_RegistersProviderOnce()
    {
        var services = new ServiceCollection();

        services.AddXiHanBotSms();
        services.AddXiHanBotSms();

        using var provider = services.BuildServiceProvider();
        var providers = provider.GetServices<IBotProvider>().ToList();

        Assert.Single(providers);
    }

    /// <summary>
    /// 解析出来的解析器在未配置时返回 null，与默认配置存储串起来即整体 fail-closed
    /// </summary>
    [Fact]
    public async Task AddXiHanBotSms_DefaultWiring_ResolvesToNoGateway()
    {
        var services = new ServiceCollection();
        services.AddXiHanBotSms();
        using var provider = services.BuildServiceProvider();

        var resolver = provider.GetRequiredService<ISmsGatewayResolver>();
        var gateway = await resolver.ResolveAsync(TestContext.Current.CancellationToken);

        Assert.Null(gateway);
    }
}
