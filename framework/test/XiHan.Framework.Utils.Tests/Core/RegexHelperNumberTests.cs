// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Core;

namespace XiHan.Framework.Utils.Tests.Core;

/// <summary>
/// 金额类正则测试
/// </summary>
/// <remarks>
/// 两个小数正则原先把小数点写成未转义的 <c>.</c>，在正则里是"任意字符"，
/// <c>"12a34"</c> 会被判定为合法的两位小数金额。金额校验放过任意字符是能进数据库的错误。
/// </remarks>
public class RegexHelperNumberTests
{
    /// <summary>
    /// 两位小数金额只接受真正的小数点
    /// </summary>
    [Theory]
    [InlineData("12.34", true)]
    [InlineData("0.00", true)]
    [InlineData("12", true)]
    [InlineData("12a34", false)]
    [InlineData("12,34", false)]
    [InlineData("12-34", false)]
    [InlineData("12.3", false)]
    [InlineData("12.345", false)]
    [InlineData("-12.34", false)]
    public void NumberPositiveRealTwoDoubleRegex_RequiresLiteralDecimalPoint(string value, bool expected)
    {
        Assert.Equal(expected, RegexHelper.NumberPositiveRealTwoDoubleRegex().IsMatch(value));
    }

    /// <summary>
    /// 一到三位小数金额只接受真正的小数点
    /// </summary>
    [Theory]
    [InlineData("1.2", true)]
    [InlineData("1.23", true)]
    [InlineData("1.234", true)]
    [InlineData("1", true)]
    [InlineData("1x234", false)]
    [InlineData("1 234", false)]
    [InlineData("1.2345", false)]
    public void NumberPositiveRealOneOrThreeDoubleRegex_RequiresLiteralDecimalPoint(string value, bool expected)
    {
        Assert.Equal(expected, RegexHelper.NumberPositiveRealOneOrThreeDoubleRegex().IsMatch(value));
    }

    /// <summary>
    /// 数字主键正则不接受前导零、零本身与串尾换行
    /// </summary>
    [Theory]
    [InlineData("1", true)]
    [InlineData("123", true)]
    [InlineData("0", false)]
    [InlineData("01", false)]
    [InlineData("12\n", false)]
    [InlineData("", false)]
    public void NumberIdRegex_AcceptsOnlyNonZeroIntegersWithoutLeadingZero(string value, bool expected)
    {
        Assert.Equal(expected, RegexHelper.NumberIdRegex().IsMatch(value));
    }
}
