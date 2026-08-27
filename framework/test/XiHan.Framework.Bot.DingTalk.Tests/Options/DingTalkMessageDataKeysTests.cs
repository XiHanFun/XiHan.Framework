// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.DingTalk.Models;
using XiHan.Framework.Bot.DingTalk.Options;
using XiHan.Framework.Bot.Enums;
using XiHan.Framework.Bot.Helpers;
using XiHan.Framework.Bot.Models;

namespace XiHan.Framework.Bot.DingTalk.Tests.Options;

/// <summary>
/// 钉钉消息扩展数据键名测试
/// </summary>
/// <remarks>
/// 这三个键是调用方与钉钉提供者之间的约定：调用方把强类型消息体塞进 <see cref="BotMessage.Data"/>，
/// 提供者按键名取回。键名一旦改动，调用方不会收到编译错误，只会静默退化成纯文本消息，因此必须锁死字面量。
/// </remarks>
public class DingTalkMessageDataKeysTests
{
    /// <summary>
    /// 键名字面量锁死
    /// </summary>
    [Fact]
    public void Keys_LiteralValues_AreLocked()
    {
        Assert.Equal("DingTalk.Link", DingTalkMessageDataKeys.DingTalkLink);
        Assert.Equal("DingTalk.ActionCard", DingTalkMessageDataKeys.DingTalkActionCard);
        Assert.Equal("DingTalk.FeedCard", DingTalkMessageDataKeys.DingTalkFeedCard);
    }

    /// <summary>
    /// 三个键互不相同（大小写无关比较下也不重叠）
    /// </summary>
    [Fact]
    public void Keys_AreDistinct_IgnoringCase()
    {
        string[] keys =
        [
            DingTalkMessageDataKeys.DingTalkLink,
            DingTalkMessageDataKeys.DingTalkActionCard,
            DingTalkMessageDataKeys.DingTalkFeedCard
        ];

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// 键名在消息扩展数据里大小写不敏感可取回
    /// </summary>
    /// <remarks>
    /// <see cref="BotMessage.Data"/> 用的是 OrdinalIgnoreCase 字典，调用方大小写写错也应命中。
    /// </remarks>
    [Fact]
    public void Keys_ResolveThroughBotMessageHelper_IgnoringCase()
    {
        var link = new DingTalkLink
        {
            Title = "构建结果",
            Text = "构建成功",
            MessageUrl = "https://example.invalid/build/1"
        };

        var message = new BotMessage
        {
            Type = BotMessageType.Link
        };
        message.Data[DingTalkMessageDataKeys.DingTalkLink.ToUpperInvariant()] = link;

        var resolved = BotMessageHelper.TryGetData(message, DingTalkMessageDataKeys.DingTalkLink, out DingTalkLink? value);

        Assert.True(resolved);
        Assert.Same(link, value);
    }

    /// <summary>
    /// 键命中但值类型不匹配时视为未提供
    /// </summary>
    [Fact]
    public void Keys_WhenValueTypeMismatched_AreTreatedAsAbsent()
    {
        var message = new BotMessage();
        message.Data[DingTalkMessageDataKeys.DingTalkActionCard] = "这不是任务卡片对象";

        var resolved = BotMessageHelper.TryGetData(message, DingTalkMessageDataKeys.DingTalkActionCard, out DingTalkActionCard? value);

        Assert.False(resolved);
        Assert.Null(value);
    }
}
