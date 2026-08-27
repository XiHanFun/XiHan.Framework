// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Stores;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Extensions.DependencyInjection;

/// <summary>
/// <see cref="XiHanBotTelegramServiceCollectionExtensions"/> 单发通道注册扩展测试
/// </summary>
/// <remarks>
/// 注册全是 TryAdd / TryAddEnumerable 语义：
/// 应用层必须能用数据库实现覆盖默认的配置存储，重复注册也不能让同一条消息被发两次。
/// 这里用真实 ServiceCollection 与真实容器验证解析结果与生命周期。
/// </remarks>
public class XiHanBotTelegramServiceCollectionExtensionsTests
{
    /// <summary>
    /// 服务集合为空时抛参数空异常
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegram_WhenServicesNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => XiHanBotTelegramServiceCollectionExtensions.AddXiHanBotTelegram(null!));
    }

    /// <summary>
    /// 返回原服务集合本身，支持链式调用
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegram_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddXiHanBotTelegram());
    }

    /// <summary>
    /// 默认配置存储注册为单例
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegram_RegistersDefaultConfigStoreAsSingleton()
    {
        var services = new ServiceCollection();

        _ = services.AddXiHanBotTelegram();

        var descriptors = services.Where(x => x.ServiceType == typeof(ITelegramConfigStore)).ToList();
        Assert.Equal(1, descriptors.Count);
        Assert.Equal(ServiceLifetime.Singleton, descriptors[0].Lifetime);
        Assert.Equal(typeof(DefaultTelegramConfigStore), descriptors[0].ImplementationType);
    }

    /// <summary>
    /// Telegram 提供者以可枚举方式注册，与其它渠道提供者共存
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegram_RegistersProviderAsEnumerable()
    {
        var services = new ServiceCollection();

        _ = services.AddXiHanBotTelegram();

        var descriptors = services.Where(x => x.ServiceType == typeof(IBotProvider)).ToList();
        Assert.Equal(1, descriptors.Count);
        Assert.Equal(ServiceLifetime.Singleton, descriptors[0].Lifetime);
        Assert.Equal(typeof(TelegramBotProvider), descriptors[0].ImplementationType);
    }

    /// <summary>
    /// 注册后两项服务都能从真实容器解析出来，且单例复用同一实例
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegram_ResolvesServicesAsSingletons()
    {
        var services = new ServiceCollection();
        _ = services.AddXiHanBotTelegram();
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredService<ITelegramConfigStore>();
        var providers = provider.GetServices<IBotProvider>().ToList();

        Assert.IsType<DefaultTelegramConfigStore>(store);
        Assert.Equal(1, providers.Count);
        Assert.IsType<TelegramBotProvider>(providers[0]);
        Assert.Same(store, provider.GetRequiredService<ITelegramConfigStore>());
    }

    /// <summary>
    /// 配置委托被写入选项
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegram_AppliesConfigureDelegate()
    {
        var services = new ServiceCollection();

        _ = services.AddXiHanBotTelegram(options =>
        {
            options.Token = "123456:AAHfake-telegram-token";
            options.ChatId = "-100123";
            options.ParseMode = "MarkdownV2";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TelegramOptions>>().Value;

        Assert.Equal("123456:AAHfake-telegram-token", options.Token);
        Assert.Equal("-100123", options.ChatId);
        Assert.Equal("MarkdownV2", options.ParseMode);
        Assert.True(options.Enabled);
    }

    /// <summary>
    /// 不传配置委托时不写入任何选项配置项
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegram_WithoutConfigureDelegate_KeepsDefaultOptions()
    {
        var services = new ServiceCollection();

        _ = services.AddXiHanBotTelegram();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TelegramOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.Equal(string.Empty, options.Token);
    }

    /// <summary>
    /// 应用层已注册的配置存储不被默认实现覆盖（TryAdd 语义）
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegram_KeepsPreRegisteredConfigStore()
    {
        var custom = new FakeTelegramConfigStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ITelegramConfigStore>(custom);

        _ = services.AddXiHanBotTelegram();

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<ITelegramConfigStore>());
    }

    /// <summary>
    /// 重复注册不会产生重复的提供者，避免同一条消息被发两次
    /// </summary>
    [Fact]
    public void AddXiHanBotTelegram_CalledTwice_RegistersProviderOnce()
    {
        var services = new ServiceCollection();

        _ = services.AddXiHanBotTelegram();
        _ = services.AddXiHanBotTelegram();

        using var provider = services.BuildServiceProvider();

        Assert.Equal(1, provider.GetServices<IBotProvider>().Count());
    }
}
