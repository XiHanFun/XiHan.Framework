// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Stores;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Stores;

/// <summary>
/// <see cref="DefaultTelegramConfigStore"/> 默认单发通道配置存储测试
/// </summary>
/// <remarks>
/// 与多机器人平台不同，这里的默认实现永远返回非 null 的选项当前值，
/// 由 <c>TelegramBotProvider</c> 按 Enabled / Token 自行 fail-closed。
/// </remarks>
public class DefaultTelegramConfigStoreTests
{
    /// <summary>
    /// 未配置时返回默认选项（默认启用但没有凭证）而不是 null
    /// </summary>
    [Fact]
    public async Task GetAsync_WhenNotConfigured_ReturnsDefaultOptions()
    {
        var store = new DefaultTelegramConfigStore(new TestOptionsMonitor<TelegramOptions>(new TelegramOptions()));

        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.True(options!.Enabled);
        Assert.Equal(string.Empty, options.Token);
        Assert.Equal(string.Empty, options.ChatId);
    }

    /// <summary>
    /// 返回选项监视器的当前值本身
    /// </summary>
    [Fact]
    public async Task GetAsync_ReturnsCurrentOptionsInstance()
    {
        var current = new TelegramOptions { Token = "123456:AAHfake-telegram-token", ChatId = "100" };
        var store = new DefaultTelegramConfigStore(new TestOptionsMonitor<TelegramOptions>(current));

        Assert.Same(current, await store.GetAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 选项被替换后下一次读取即时生效
    /// </summary>
    [Fact]
    public async Task GetAsync_ReflectsOptionsReplacement()
    {
        var monitor = new TestOptionsMonitor<TelegramOptions>(new TelegramOptions());
        var store = new DefaultTelegramConfigStore(monitor);

        monitor.Set(new TelegramOptions { Enabled = false, Token = "123456:AAHfake-telegram-token" });

        var options = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(options);
        Assert.False(options!.Enabled);
        Assert.Equal("123456:AAHfake-telegram-token", options.Token);
    }

    /// <summary>
    /// 不传取消令牌时同样工作
    /// </summary>
    [Fact]
    public async Task GetAsync_WithoutCancellationToken_Works()
    {
        var store = new DefaultTelegramConfigStore(new TestOptionsMonitor<TelegramOptions>(new TelegramOptions()));

        Assert.NotNull(await store.GetAsync());
    }

    /// <summary>
    /// 默认实现挂在 ITelegramConfigStore 抽象上，可被数据库实现整体替换
    /// </summary>
    [Fact]
    public void Type_ImplementsTelegramConfigStoreAbstraction()
    {
        Assert.IsAssignableFrom<ITelegramConfigStore>(
            new DefaultTelegramConfigStore(new TestOptionsMonitor<TelegramOptions>(new TelegramOptions())));
    }
}
