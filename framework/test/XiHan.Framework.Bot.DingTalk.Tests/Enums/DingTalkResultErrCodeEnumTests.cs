// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.DingTalk.Enums;
using XiHan.Framework.Utils.Core;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Bot.DingTalk.Tests.Enums;

/// <summary>
/// 钉钉返回错误码枚举测试
/// </summary>
/// <remarks>
/// 这些数值不是本仓自定义的，而是钉钉开放平台响应体里的 errcode，属于外部协议常量：
/// 一旦被改动，DingTalkBot 的失败分支就会把错误码翻译成错误文案（或翻译不出来），排障会被彻底带偏。
/// 因此逐个锁死数值与描述文案，并且把 DingTalkBot 反查描述用到的那条链路（EnumHelper + ConvertToInt）一起验证。
/// </remarks>
public class DingTalkResultErrCodeEnumTests
{
    /// <summary>
    /// 错误码数值逐个锁死
    /// </summary>
    /// <param name="value">枚举值</param>
    /// <param name="expected">钉钉协议数值</param>
    [Theory]
    [InlineData(DingTalkResultErrCodeEnum.MessageVerificationFailed, 310000)]
    [InlineData(DingTalkResultErrCodeEnum.GroupDisbanded, 400013)]
    [InlineData(DingTalkResultErrCodeEnum.AccessTokenNotExist, 400101)]
    [InlineData(DingTalkResultErrCodeEnum.BotDeactivated, 400102)]
    [InlineData(DingTalkResultErrCodeEnum.UnsupportedMessageType, 400105)]
    [InlineData(DingTalkResultErrCodeEnum.BotNotExist, 400106)]
    [InlineData(DingTalkResultErrCodeEnum.SendingSpeedTooFast, 410100)]
    [InlineData(DingTalkResultErrCodeEnum.UnsafeOuterChain, 430101)]
    [InlineData(DingTalkResultErrCodeEnum.ContainsInappropriateText, 430102)]
    [InlineData(DingTalkResultErrCodeEnum.ContainsInappropriateImages, 430103)]
    [InlineData(DingTalkResultErrCodeEnum.ContainsInappropriateContent, 430104)]
    public void ErrCode_NumericValue_IsLocked(DingTalkResultErrCodeEnum value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    /// <summary>
    /// 枚举成员数量固定，新增成员必须同步更新协议映射测试
    /// </summary>
    [Fact]
    public void ErrCode_MemberCount_IsEleven()
    {
        var values = Enum.GetValues<DingTalkResultErrCodeEnum>();

        Assert.Equal(11, values.Length);
    }

    /// <summary>
    /// 错误码描述文案逐个锁死
    /// </summary>
    /// <param name="value">枚举值</param>
    /// <param name="expected">描述文案</param>
    [Theory]
    [InlineData(DingTalkResultErrCodeEnum.MessageVerificationFailed, "消息校验未通过，请查看机器人的安全设置")]
    [InlineData(DingTalkResultErrCodeEnum.GroupDisbanded, "群已被解散，请向其他群发消息")]
    [InlineData(DingTalkResultErrCodeEnum.AccessTokenNotExist, "access_token不存在，请确认access_token拼写是否正确")]
    [InlineData(DingTalkResultErrCodeEnum.BotDeactivated, "机器人已停用，请联系管理员启用机器人")]
    [InlineData(DingTalkResultErrCodeEnum.UnsupportedMessageType, "不支持的消息类型，请使用文档中支持的消息类型")]
    [InlineData(DingTalkResultErrCodeEnum.BotNotExist, "机器人不存在，请确认机器人是否在群中")]
    [InlineData(DingTalkResultErrCodeEnum.SendingSpeedTooFast, "发送速度太快而限流，请降低发送速度")]
    [InlineData(DingTalkResultErrCodeEnum.UnsafeOuterChain, "含有不安全的外链，请确认发送的内容合法")]
    [InlineData(DingTalkResultErrCodeEnum.ContainsInappropriateText, "含有不合适的文本，请确认发送的内容合法")]
    [InlineData(DingTalkResultErrCodeEnum.ContainsInappropriateImages, "含有不合适的图片，请确认发送的内容合法")]
    [InlineData(DingTalkResultErrCodeEnum.ContainsInappropriateContent, "含有不合适的内容，请确认发送的内容合法")]
    public void GetDescription_ReturnsDiagnosticText(DingTalkResultErrCodeEnum value, string expected)
    {
        Assert.Equal(expected, value.GetDescription());
    }

    /// <summary>
    /// 按数值反查描述的链路可用
    /// </summary>
    /// <remarks>
    /// DingTalkBot 的失败分支用的正是 <c>EnumHelper.GetTypedEnumItems</c> + <c>ConvertToInt</c> 这条链路，
    /// 强类型枚举项的 Value 必须能被转回底层 int，否则所有失败响应都只会得到一句没有原因的"发送失败；"。
    /// </remarks>
    [Fact]
    public void TypedEnumItems_LookupByErrCode_ResolvesDescription()
    {
        var items = EnumHelper.GetTypedEnumItems<DingTalkResultErrCodeEnum>();

        var info = items.FirstOrDefault(item => item.Value.ConvertToInt() == 400101);

        Assert.NotNull(info);
        Assert.Equal(DingTalkResultErrCodeEnum.AccessTokenNotExist, info.Value);
        Assert.Equal("access_token不存在，请确认access_token拼写是否正确", info.Description);
    }

    /// <summary>
    /// 未知错误码反查不到条目
    /// </summary>
    [Fact]
    public void TypedEnumItems_LookupByUnknownErrCode_ReturnsNull()
    {
        var items = EnumHelper.GetTypedEnumItems<DingTalkResultErrCodeEnum>();

        var info = items.FirstOrDefault(item => item.Value.ConvertToInt() == 999999);

        Assert.Null(info);
    }

    /// <summary>
    /// 所有枚举项都能被反查到且描述互不重复
    /// </summary>
    [Fact]
    public void TypedEnumItems_CoverEveryMember_WithDistinctDescriptions()
    {
        var items = EnumHelper.GetTypedEnumItems<DingTalkResultErrCodeEnum>();

        Assert.Equal(11, items.Count);
        Assert.All(items, item => Assert.False(string.IsNullOrWhiteSpace(item.Description)));
        Assert.Equal(items.Count, items.Select(item => item.Description).Distinct(StringComparer.Ordinal).Count());

        foreach (var value in Enum.GetValues<DingTalkResultErrCodeEnum>())
        {
            var code = (int)value;
            Assert.Contains(items, item => item.Value.ConvertToInt() == code);
        }
    }
}
