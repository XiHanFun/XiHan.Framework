// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Stores;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Stores;

/// <summary>
/// <see cref="DefaultTelegramBotConfigStore"/> 默认机器人配置存储测试
/// </summary>
/// <remarks>
/// 默认实现是「配置文件兜底」：每次都从 IOptionsMonitor 当前值取数，
/// 所以应用层改配置文件后无需重启就能被管理器的刷新周期读到。
/// 另一条关键契约是返回快照而不是内部列表本身——管理器会遍历并加工这个列表，
/// 泄漏内部列表会让选项对象被间接改写。
/// </remarks>
public class DefaultTelegramBotConfigStoreTests
{
    /// <summary>
    /// 未配置机器人时返回空列表而不是 null
    /// </summary>
    [Fact]
    public async Task GetBotConfigsAsync_WhenNoBots_ReturnsEmptyList()
    {
        var store = new DefaultTelegramBotConfigStore(TelegramTestFactory.CreatePlatformOptions());

        var configs = await store.GetBotConfigsAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(configs);
        Assert.Empty(configs);
    }

    /// <summary>
    /// 返回选项当前值里的机器人配置，顺序与配置一致
    /// </summary>
    [Fact]
    public async Task GetBotConfigsAsync_ReturnsConfiguredBots()
    {
        var options = TelegramTestFactory.CreatePlatformOptions(x =>
        {
            x.Bots.Add(TelegramTestFactory.CreateConfig(name: "bot-a"));
            x.Bots.Add(TelegramTestFactory.CreateConfig(name: "bot-b"));
        });
        var store = new DefaultTelegramBotConfigStore(options);

        var configs = await store.GetBotConfigsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, configs.Count);
        Assert.Equal("bot-a", configs[0].Name);
        Assert.Equal("bot-b", configs[1].Name);
    }

    /// <summary>
    /// 每次返回的是新的快照，且能反映选项内部列表的最新内容
    /// </summary>
    [Fact]
    public async Task GetBotConfigsAsync_ReturnsFreshSnapshotEveryCall()
    {
        var options = TelegramTestFactory.CreatePlatformOptions(x => x.Bots.Add(TelegramTestFactory.CreateConfig(name: "bot-a")));
        var store = new DefaultTelegramBotConfigStore(options);

        var first = await store.GetBotConfigsAsync(TestContext.Current.CancellationToken);
        options.CurrentValue.Bots.Add(TelegramTestFactory.CreateConfig(name: "bot-b"));
        var second = await store.GetBotConfigsAsync(TestContext.Current.CancellationToken);

        Assert.Single(first);
        Assert.Equal(2, second.Count);
        Assert.NotSame(first, second);
    }

    /// <summary>
    /// 选项整体被替换后下一次读取即时生效（配置热更新）
    /// </summary>
    [Fact]
    public async Task GetBotConfigsAsync_ReflectsOptionsReplacement()
    {
        var options = TelegramTestFactory.CreatePlatformOptions();
        var store = new DefaultTelegramBotConfigStore(options);
        Assert.Empty(await store.GetBotConfigsAsync(TestContext.Current.CancellationToken));

        var replaced = new TelegramBotPlatformOptions();
        replaced.Bots.Add(TelegramTestFactory.CreateConfig(name: "bot-new"));
        options.Set(replaced);

        var configs = await store.GetBotConfigsAsync(TestContext.Current.CancellationToken);

        Assert.Single(configs);
        Assert.Equal("bot-new", configs[0].Name);
    }

    /// <summary>
    /// 不传取消令牌时同样工作
    /// </summary>
    [Fact]
    public async Task GetBotConfigsAsync_WithoutCancellationToken_Works()
    {
        var store = new DefaultTelegramBotConfigStore(TelegramTestFactory.CreatePlatformOptions());

        Assert.Empty(await store.GetBotConfigsAsync());
    }

    /// <summary>
    /// 默认实现挂在 ITelegramBotConfigStore 抽象上，可被数据库实现整体替换
    /// </summary>
    [Fact]
    public void Type_ImplementsConfigStoreAbstraction()
    {
        Assert.IsAssignableFrom<ITelegramBotConfigStore>(
            new DefaultTelegramBotConfigStore(TelegramTestFactory.CreatePlatformOptions()));
    }
}
