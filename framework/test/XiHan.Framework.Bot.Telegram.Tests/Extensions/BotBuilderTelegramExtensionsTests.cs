// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Bot.Extensions;
using XiHan.Framework.Bot.Providers;
using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Extensions;
using XiHan.Framework.Bot.Telegram.Extensions.DependencyInjection;
using XiHan.Framework.Bot.Telegram.Messaging;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Stores;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Extensions;

/// <summary>
/// <see cref="BotBuilderTelegramExtensions"/> BotBuilder Telegram 扩展测试
/// </summary>
/// <remarks>
/// UseTelegram 是 Bot 主包对外的启用入口，注册内容必须与 AddXiHanBotTelegram 一致，
/// 且同样是 TryAdd 语义——应用层先注册的数据库配置存储不能被默认实现顶掉。
/// 与 AddXiHanBotTelegram 的差别只有一个：配置委托是必填的。
/// </remarks>
public class BotBuilderTelegramExtensionsTests
{
    /// <summary>
    /// 构建器为空时抛参数空异常
    /// </summary>
    [Fact]
    public void UseTelegram_WhenBuilderNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => BotBuilderTelegramExtensions.UseTelegram(null!, _ => { }));
    }

    /// <summary>
    /// 配置委托为空时抛参数空异常（Telegram 至少要有 Token 才可能发出消息）
    /// </summary>
    [Fact]
    public void UseTelegram_WhenConfigureNull_Throws()
    {
        var builder = new BotBuilder(new ServiceCollection());

        Assert.Throws<ArgumentNullException>(() => builder.UseTelegram(null!));
    }

    /// <summary>
    /// 返回原构建器本身，支持链式调用
    /// </summary>
    [Fact]
    public void UseTelegram_ReturnsSameBuilder()
    {
        var builder = new BotBuilder(new ServiceCollection());

        Assert.Same(builder, builder.UseTelegram(_ => { }));
    }

    /// <summary>
    /// 注册 Telegram 提供者与默认配置存储
    /// </summary>
    [Fact]
    public void UseTelegram_RegistersProviderAndConfigStore()
    {
        var services = new ServiceCollection();

        _ = new BotBuilder(services).UseTelegram(_ => { });

        using var provider = services.BuildServiceProvider();
        var providers = provider.GetServices<IBotProvider>().ToList();

        Assert.IsType<DefaultTelegramConfigStore>(provider.GetRequiredService<ITelegramConfigStore>());
        Assert.Single(providers);
        Assert.IsType<TelegramBotProvider>(providers[0]);
    }

    /// <summary>
    /// 配置委托被写入选项
    /// </summary>
    [Fact]
    public void UseTelegram_AppliesConfigureDelegate()
    {
        var services = new ServiceCollection();

        _ = new BotBuilder(services).UseTelegram(options =>
        {
            options.Token = "123456:AAHfake-telegram-token";
            options.ChatId = "@my_channel";
            options.DisableNotification = true;
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TelegramOptions>>().Value;

        Assert.Equal("123456:AAHfake-telegram-token", options.Token);
        Assert.Equal("@my_channel", options.ChatId);
        Assert.True(options.DisableNotification);
    }

    /// <summary>
    /// 应用层已注册的配置存储不被默认实现覆盖（TryAdd 语义）
    /// </summary>
    [Fact]
    public void UseTelegram_KeepsPreRegisteredConfigStore()
    {
        var custom = new FakeTelegramConfigStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ITelegramConfigStore>(custom);

        _ = new BotBuilder(services).UseTelegram(_ => { });

        using var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<ITelegramConfigStore>());
    }

    /// <summary>
    /// 重复启用不会产生重复的提供者
    /// </summary>
    [Fact]
    public void UseTelegram_CalledTwice_RegistersProviderOnce()
    {
        var services = new ServiceCollection();
        var builder = new BotBuilder(services);

        _ = builder.UseTelegram(_ => { });
        _ = builder.UseTelegram(_ => { });

        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IBotProvider>());
    }

    /// <summary>
    /// 与 AddXiHanBotTelegram 注册出同一套服务类型
    /// </summary>
    /// <remarks>
    /// 两个入口注册内容一旦分叉，用哪个入口启用会决定行为差异，属于隐藏的坑。
    /// </remarks>
    [Fact]
    public void UseTelegram_RegistersSameServiceTypesAsAddXiHanBotTelegram()
    {
        var viaBuilder = new ServiceCollection();
        _ = new BotBuilder(viaBuilder).UseTelegram(_ => { });

        var viaExtension = new ServiceCollection();
        _ = viaExtension.AddXiHanBotTelegram(_ => { });

        var builderTypes = viaBuilder.Select(x => x.ServiceType).Distinct().OrderBy(x => x.FullName, StringComparer.Ordinal).ToArray();
        var extensionTypes = viaExtension.Select(x => x.ServiceType).Distinct().OrderBy(x => x.FullName, StringComparer.Ordinal).ToArray();

        Assert.Equal(extensionTypes, builderTypes);
    }
}
