// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramBotSettings"/> 平台全局设置测试
/// </summary>
/// <remarks>
/// 三个默认值属于安全基线，改动会直接放大生产风险：
/// Enabled 默认关闭（引入依赖不等于自动上线机器人）、WebhookBaseUrl 默认空（默认走长轮询，
/// 不要求应用暴露公网入口）、WebhookSecretToken 默认空（配合中间件 fail-closed 拒绝一切 Webhook 请求）。
/// </remarks>
public class TelegramBotSettingsTests
{
    /// <summary>
    /// 平台默认关闭，默认走长轮询且没有 Webhook 密钥
    /// </summary>
    [Fact]
    public void Defaults_AreDisabledPollingWithoutSecret()
    {
        var settings = new TelegramBotSettings();

        Assert.False(settings.Enabled);
        Assert.Equal(string.Empty, settings.WebhookBaseUrl);
        Assert.Equal(string.Empty, settings.WebhookSecretToken);
        Assert.False(settings.EnableFallbackReply);
    }

    /// <summary>
    /// 缓存与刷新周期默认均为 5 秒
    /// </summary>
    [Fact]
    public void Defaults_CacheAndRefreshSecondsAreFive()
    {
        var settings = new TelegramBotSettings();

        Assert.Equal(5, settings.ConfigCacheSeconds);
        Assert.Equal(5, settings.ManagerRefreshSeconds);
    }

    /// <summary>
    /// Webhook 路由前缀默认取平台常量，不允许两处各写一份字面量
    /// </summary>
    [Fact]
    public void Defaults_WebhookRoutePrefixComesFromPlatformConsts()
    {
        var settings = new TelegramBotSettings();

        Assert.Equal(TelegramBotPlatformConsts.DefaultWebhookRoutePrefix, settings.WebhookRoutePrefix);
        Assert.Equal("/api/telegram-bot/webhook", settings.WebhookRoutePrefix);
    }

    /// <summary>
    /// 网络配置默认已实例化，读取时不需要判空
    /// </summary>
    [Fact]
    public void Defaults_NetworkIsInitialized()
    {
        var settings = new TelegramBotSettings();

        Assert.NotNull(settings.Network);
        Assert.Equal(100, settings.Network.TimeoutSeconds);
    }

    /// <summary>
    /// 每个实例持有独立的网络配置对象，互不串改
    /// </summary>
    [Fact]
    public void Defaults_NetworkIsNotSharedBetweenInstances()
    {
        var first = new TelegramBotSettings();
        var second = new TelegramBotSettings();

        first.Network.TimeoutSeconds = 15;

        Assert.Equal(100, second.Network.TimeoutSeconds);
    }
}
