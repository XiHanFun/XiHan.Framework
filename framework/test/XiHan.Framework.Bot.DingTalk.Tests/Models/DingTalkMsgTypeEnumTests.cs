// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.DingTalk.Models;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Bot.DingTalk.Tests.Models;

/// <summary>
/// 钉钉消息类型枚举测试
/// </summary>
/// <remarks>
/// DingTalkBot 发送时把枚举的 Description 直接写进请求体的 msgtype 字段，
/// 也就是说这些描述文案不是给人看的说明，而是钉钉协议的字面量，大小写都不能动
/// （actionCard / feedCard 是小驼峰，写成 actioncard 会被钉钉判为不支持的消息类型）。
/// </remarks>
public class DingTalkMsgTypeEnumTests
{
    /// <summary>
    /// 描述文案即协议 msgtype 字面量
    /// </summary>
    /// <param name="value">枚举值</param>
    /// <param name="expected">协议 msgtype</param>
    [Theory]
    [InlineData(DingTalkMsgTypeEnum.Text, "text")]
    [InlineData(DingTalkMsgTypeEnum.Link, "link")]
    [InlineData(DingTalkMsgTypeEnum.Markdown, "markdown")]
    [InlineData(DingTalkMsgTypeEnum.ActionCard, "actionCard")]
    [InlineData(DingTalkMsgTypeEnum.FeedCard, "feedCard")]
    public void GetDescription_ReturnsProtocolMsgType(DingTalkMsgTypeEnum value, string expected)
    {
        Assert.Equal(expected, value.GetDescription());
    }

    /// <summary>
    /// 自定义机器人只支持这五种消息类型
    /// </summary>
    [Fact]
    public void MsgType_MemberCount_IsFive()
    {
        Assert.Equal(5, Enum.GetValues<DingTalkMsgTypeEnum>().Length);
    }

    /// <summary>
    /// 五个 msgtype 字面量互不相同
    /// </summary>
    [Fact]
    public void MsgType_ProtocolLiterals_AreDistinct()
    {
        var literals = Enum.GetValues<DingTalkMsgTypeEnum>()
            .Select(value => value.GetDescription())
            .ToList();

        Assert.Equal(literals.Count, literals.Distinct(StringComparer.Ordinal).Count());
    }
}
