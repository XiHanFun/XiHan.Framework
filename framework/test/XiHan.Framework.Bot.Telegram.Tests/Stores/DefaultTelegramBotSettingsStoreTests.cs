// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Abstractions;
using XiHan.Framework.Bot.Telegram.Options;
using XiHan.Framework.Bot.Telegram.Stores;
using XiHan.Framework.Bot.Telegram.Tests.Fakes;

namespace XiHan.Framework.Bot.Telegram.Tests.Stores;

/// <summary>
/// <see cref="DefaultTelegramBotSettingsStore"/> 默认平台设置存储测试
/// </summary>
/// <remarks>
/// 管理器每个刷新周期都会问一次这个存储，默认实现必须始终返回选项当前值，
/// 这样「改配置文件 → 下个周期生效」才成立。
/// </remarks>
public class DefaultTelegramBotSettingsStoreTests
{
    /// <summary>
    /// 未配置时返回默认设置：平台关闭、长轮询、无 Webhook 密钥
    /// </summary>
    [Fact]
    public async Task GetSettingsAsync_WhenNotConfigured_ReturnsDisabledDefaults()
    {
        var store = new DefaultTelegramBotSettingsStore(TelegramTestFactory.CreatePlatformOptions());

        var settings = await store.GetSettingsAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(settings);
        Assert.False(settings.Enabled);
        Assert.Equal(string.Empty, settings.WebhookBaseUrl);
        Assert.Equal(string.Empty, settings.WebhookSecretToken);
    }

    /// <summary>
    /// 返回选项当前值里的那个设置对象本身
    /// </summary>
    [Fact]
    public async Task GetSettingsAsync_ReturnsConfiguredSettingsInstance()
    {
        var options = TelegramTestFactory.CreatePlatformOptions(x =>
        {
            x.Settings.Enabled = true;
            x.Settings.WebhookBaseUrl = "https://example.com";
            x.Settings.WebhookSecretToken = "s3cr3t";
        });
        var store = new DefaultTelegramBotSettingsStore(options);

        var settings = await store.GetSettingsAsync(TestContext.Current.CancellationToken);

        Assert.True(settings.Enabled);
        Assert.Equal("https://example.com", settings.WebhookBaseUrl);
        Assert.Equal("s3cr3t", settings.WebhookSecretToken);
        Assert.Same(options.CurrentValue.Settings, settings);
    }

    /// <summary>
    /// 选项整体被替换后下一次读取即时生效
    /// </summary>
    [Fact]
    public async Task GetSettingsAsync_ReflectsOptionsReplacement()
    {
        var options = TelegramTestFactory.CreatePlatformOptions();
        var store = new DefaultTelegramBotSettingsStore(options);
        Assert.False((await store.GetSettingsAsync(TestContext.Current.CancellationToken)).Enabled);

        var replaced = new TelegramBotPlatformOptions();
        replaced.Settings.Enabled = true;
        options.Set(replaced);

        Assert.True((await store.GetSettingsAsync(TestContext.Current.CancellationToken)).Enabled);
    }

    /// <summary>
    /// 不传取消令牌时同样工作
    /// </summary>
    [Fact]
    public async Task GetSettingsAsync_WithoutCancellationToken_Works()
    {
        var store = new DefaultTelegramBotSettingsStore(TelegramTestFactory.CreatePlatformOptions());

        Assert.NotNull(await store.GetSettingsAsync());
    }

    /// <summary>
    /// 默认实现挂在 ITelegramBotSettingsStore 抽象上，可被数据库实现整体替换
    /// </summary>
    [Fact]
    public void Type_ImplementsSettingsStoreAbstraction()
    {
        Assert.IsAssignableFrom<ITelegramBotSettingsStore>(
            new DefaultTelegramBotSettingsStore(TelegramTestFactory.CreatePlatformOptions()));
    }
}
