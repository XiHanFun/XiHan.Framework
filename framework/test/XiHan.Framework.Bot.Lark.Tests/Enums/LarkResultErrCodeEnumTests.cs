// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Bot.Lark.Enums;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Bot.Lark.Tests.Enums;

/// <summary>
/// 飞书结果错误码枚举测试
/// </summary>
/// <remarks>
/// 这些数值直接来自飞书开放平台响应体的 code 字段，属于对外协议常量。
/// LarkBot 依赖「枚举数值 == 响应 code」把错误码翻译成可读描述，数值一旦漂移错误分支就静默失配，
/// 因此逐个锁死，不做区间断言。
/// </remarks>
public class LarkResultErrCodeEnumTests
{
    /// <summary>
    /// 错误码数值与飞书开放平台文档一致
    /// </summary>
    [Fact]
    public void Values_Always_MatchLarkOpenPlatformCodes()
    {
        Assert.Equal(9499, (int)LarkResultErrCodeEnum.BadRequest);
        Assert.Equal(19021, (int)LarkResultErrCodeEnum.SignMatchFail);
        Assert.Equal(19022, (int)LarkResultErrCodeEnum.IpNotAllowed);
        Assert.Equal(19024, (int)LarkResultErrCodeEnum.KeyWordsNotFound);
    }

    /// <summary>
    /// 枚举成员数量固定为四个
    /// </summary>
    [Fact]
    public void Members_Always_AreExactlyFour()
    {
        Assert.Equal(4, Enum.GetValues<LarkResultErrCodeEnum>().Length);
    }

    /// <summary>
    /// 错误码集合中不包含成功码 0
    /// </summary>
    /// <remarks>
    /// LarkBot 把 code == 0 判定为成功，错误码枚举里若混入 0 会让成功响应被翻译成失败描述。
    /// </remarks>
    [Fact]
    public void Values_Always_DoNotContainSuccessCodeZero()
    {
        Assert.DoesNotContain(Enum.GetValues<LarkResultErrCodeEnum>(), value => (int)value == 0);
    }

    /// <summary>
    /// 错误码数值互不重复
    /// </summary>
    [Fact]
    public void Values_Always_AreDistinct()
    {
        var values = Enum.GetValues<LarkResultErrCodeEnum>().Select(value => (int)value).ToList();

        Assert.Equal(values.Count, values.Distinct().Count());
    }

    /// <summary>
    /// 每个错误码都带有可读的中文描述
    /// </summary>
    [Theory]
    [InlineData(LarkResultErrCodeEnum.BadRequest, "请求体格式错误")]
    [InlineData(LarkResultErrCodeEnum.SignMatchFail, "签名校验失败")]
    [InlineData(LarkResultErrCodeEnum.IpNotAllowed, "IP校验失败")]
    [InlineData(LarkResultErrCodeEnum.KeyWordsNotFound, "关键词校验失败")]
    public void GetDescription_ForEachMember_ContainsExpectedKeyword(LarkResultErrCodeEnum code, string keyword)
    {
        var description = code.GetDescription();

        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Contains(keyword, description);
    }

    /// <summary>
    /// 描述文本不会退化成枚举成员名
    /// </summary>
    /// <remarks>
    /// EnumHelper 在缺少 Description 特性时会回落成员名，这里确保四个成员都真的挂了特性。
    /// </remarks>
    [Fact]
    public void GetDescription_ForEachMember_IsNotMemberName()
    {
        foreach (var value in Enum.GetValues<LarkResultErrCodeEnum>())
        {
            Assert.NotEqual(value.ToString(), value.GetDescription());
        }
    }
}
