// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Telegram.Options;

namespace XiHan.Framework.Bot.Telegram.Tests.Options;

/// <summary>
/// <see cref="TelegramMessageDataKeys"/> 消息 Data 键名常量测试
/// </summary>
/// <remarks>
/// 键名是调用方写进 <c>BotMessage.Data</c> 的字面量契约，改名会让所有调用方的会话覆盖与解析模式覆盖静默失效。
/// </remarks>
public class TelegramMessageDataKeysTests
{
    /// <summary>
    /// 会话 Id 键名锁死
    /// </summary>
    [Fact]
    public void TelegramChatId_IsStableKey()
    {
        Assert.Equal("Telegram.ChatId", TelegramMessageDataKeys.TelegramChatId);
    }

    /// <summary>
    /// 解析模式键名锁死
    /// </summary>
    [Fact]
    public void TelegramParseMode_IsStableKey()
    {
        Assert.Equal("Telegram.ParseMode", TelegramMessageDataKeys.TelegramParseMode);
    }

    /// <summary>
    /// 两个键名不重复，避免互相覆盖
    /// </summary>
    [Fact]
    public void Keys_AreDistinct()
    {
        Assert.NotEqual(TelegramMessageDataKeys.TelegramChatId, TelegramMessageDataKeys.TelegramParseMode);
    }
}
