// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Enums;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// <see cref="BotMessageType"/> 枚举测试
/// </summary>
/// <remarks>
/// 消息类型会随 <c>BotMessage</c> 序列化，各提供者子包也按该值分支组装平台报文，
/// 序号一旦插队就会让历史配置指向错误的类型，因此锁死序号与顺序。
/// </remarks>
public class BotMessageTypeTests
{
    /// <summary>
    /// 各成员的序号不漂移
    /// </summary>
    [Theory]
    [InlineData(BotMessageType.Text, 0)]
    [InlineData(BotMessageType.Markdown, 1)]
    [InlineData(BotMessageType.Card, 2)]
    [InlineData(BotMessageType.Image, 3)]
    [InlineData(BotMessageType.File, 4)]
    [InlineData(BotMessageType.Link, 5)]
    public void Values_AreStable(BotMessageType type, int expected)
    {
        Assert.Equal(expected, (int)type);
    }

    /// <summary>
    /// 成员数量固定为六个
    /// </summary>
    [Fact]
    public void Members_CountIsSix()
    {
        Assert.Equal(6, Enum.GetValues<BotMessageType>().Length);
    }

    /// <summary>
    /// 默认值是纯文本
    /// </summary>
    [Fact]
    public void Default_IsText()
    {
        Assert.Equal(BotMessageType.Text, default(BotMessageType));
    }
}
