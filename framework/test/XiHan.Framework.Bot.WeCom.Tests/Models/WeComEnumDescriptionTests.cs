// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.WeCom.Models;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Bot.WeCom.Tests.Models;

/// <summary>
/// 企业微信枚举描述值测试
/// </summary>
/// <remarks>
/// <c>WeComBot</c> 是直接把枚举的 Description 当作报文里的 msgtype / card_type 发出去的，
/// 也就是说这些描述值本身就是企业微信协议字面量，改一个字符消息就发不出去，必须逐个锁死。
/// </remarks>
public class WeComEnumDescriptionTests
{
    /// <summary>
    /// 消息类型描述值与企业微信 msgtype 一致
    /// </summary>
    /// <param name="msgType">消息类型</param>
    /// <param name="expected">期望协议值</param>
    [Theory]
    [InlineData(WeComMsgTypeEnum.Text, "text")]
    [InlineData(WeComMsgTypeEnum.Markdown, "markdown")]
    [InlineData(WeComMsgTypeEnum.Image, "image")]
    [InlineData(WeComMsgTypeEnum.News, "news")]
    [InlineData(WeComMsgTypeEnum.File, "file")]
    [InlineData(WeComMsgTypeEnum.Voice, "voice")]
    [InlineData(WeComMsgTypeEnum.TemplateCard, "template_card")]
    public void MsgTypeDescription_MatchesProtocolValue(WeComMsgTypeEnum msgType, string expected)
    {
        Assert.Equal(expected, msgType.GetDescription());
    }

    /// <summary>
    /// 模版卡片类型描述值与企业微信 card_type 一致
    /// </summary>
    /// <param name="cardType">卡片类型</param>
    /// <param name="expected">期望协议值</param>
    [Theory]
    [InlineData(WeComTemplateCardType.TextNotice, "text_notice")]
    [InlineData(WeComTemplateCardType.NewsNotice, "news_notice")]
    public void TemplateCardTypeDescription_MatchesProtocolValue(WeComTemplateCardType cardType, string expected)
    {
        Assert.Equal(expected, cardType.GetDescription());
    }

    /// <summary>
    /// 上传类型描述值与企业微信 type 查询参数一致
    /// </summary>
    /// <param name="uploadType">上传类型</param>
    /// <param name="expected">期望协议值</param>
    [Theory]
    [InlineData(WeComUploadType.File, "file")]
    [InlineData(WeComUploadType.Voice, "voice")]
    public void UploadTypeDescription_MatchesProtocolValue(WeComUploadType uploadType, string expected)
    {
        Assert.Equal(expected, uploadType.GetDescription());
    }

    /// <summary>
    /// 每个枚举成员都写了显式描述，没有漏标而回落到成员名
    /// </summary>
    /// <remarks>
    /// 描述缺失时取描述的实现会回落成 CLR 成员名（如 "TemplateCard"），
    /// 那样发出去的 msgtype 是大驼峰，企业微信直接拒收；这里做一次整体兜底校验。
    /// </remarks>
    [Fact]
    public void AllEnumMembers_DeclareLowerCaseProtocolDescription()
    {
        foreach (var value in Enum.GetValues<WeComMsgTypeEnum>())
        {
            AssertProtocolDescription(value.GetDescription(), value.ToString());
        }

        foreach (var value in Enum.GetValues<WeComTemplateCardType>())
        {
            AssertProtocolDescription(value.GetDescription(), value.ToString());
        }

        foreach (var value in Enum.GetValues<WeComUploadType>())
        {
            AssertProtocolDescription(value.GetDescription(), value.ToString());
        }
    }

    private static void AssertProtocolDescription(string description, string memberName)
    {
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.NotEqual(memberName, description);
        Assert.Equal(description.ToLowerInvariant(), description);
    }
}
