// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramOptions"/> 单发通道配置测试
/// </summary>
/// <remarks>
/// 与多机器人平台设置不同，单发提供者配置默认 <c>Enabled = true</c>——
/// 它是「配了就用」的语义，未配置 Token 时由提供者自己 fail-closed 返回 BadRequest。
/// </remarks>
public class TelegramOptionsTests
{
    /// <summary>
    /// 默认启用、无 Token、无默认会话、不禁用通知
    /// </summary>
    [Fact]
    public void Defaults_AreEnabledWithoutCredentials()
    {
        var options = new TelegramOptions();

        Assert.True(options.Enabled);
        Assert.Equal(string.Empty, options.Token);
        Assert.Equal(string.Empty, options.ChatId);
        Assert.Null(options.ParseMode);
        Assert.False(options.DisableNotification);
    }

    /// <summary>
    /// 全部属性可写并原样读回
    /// </summary>
    [Fact]
    public void Properties_AreMutableAndRoundTrip()
    {
        var options = new TelegramOptions
        {
            Enabled = false,
            Token = "123456:AAHfake-telegram-token",
            ChatId = "@my_channel",
            ParseMode = "MarkdownV2",
            DisableNotification = true
        };

        Assert.False(options.Enabled);
        Assert.Equal("123456:AAHfake-telegram-token", options.Token);
        Assert.Equal("@my_channel", options.ChatId);
        Assert.Equal("MarkdownV2", options.ParseMode);
        Assert.True(options.DisableNotification);
    }

    /// <summary>
    /// JSON 往返保持字段名与取值（配置可能由应用层落库回传）
    /// </summary>
    [Fact]
    public void JsonRoundTrip_KeepsFieldNames()
    {
        var options = new TelegramOptions
        {
            Enabled = true,
            Token = "123456:AAHfake-telegram-token",
            ChatId = "-100123456",
            ParseMode = "Html",
            DisableNotification = true
        };

        var json = JsonSerializer.Serialize(options);

        Assert.Contains("\"Enabled\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Token\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ChatId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ParseMode\"", json, StringComparison.Ordinal);
        Assert.Contains("\"DisableNotification\"", json, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<TelegramOptions>(json);

        Assert.NotNull(restored);
        Assert.True(restored!.Enabled);
        Assert.Equal(options.Token, restored.Token);
        Assert.Equal(options.ChatId, restored.ChatId);
        Assert.Equal(options.ParseMode, restored.ParseMode);
        Assert.True(restored.DisableNotification);
    }
}
