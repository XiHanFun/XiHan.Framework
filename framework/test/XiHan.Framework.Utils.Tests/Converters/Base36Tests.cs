// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Converters;

namespace XiHan.Framework.Utils.Tests.Converters;

/// <summary>
/// Base36 编解码测试
/// </summary>
/// <remarks>
/// 只覆盖单字节与零值场景：多字节往返在当前实现下会颠倒字节序，
/// 详见交付报告的疑似缺陷段落，这里不写成迎合缺陷的断言。
/// </remarks>
public class Base36Tests
{
    /// <summary>
    /// 字母表按 0-9A-Z 顺序映射
    /// </summary>
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(10, "A")]
    [InlineData(35, "Z")]
    [InlineData(36, "10")]
    [InlineData(255, "73")]
    public void Encode_SingleByte_UsesDigitsThenLetters(int value, string expected)
    {
        Assert.Equal(expected, Base36.Encode([(byte)value]));
    }

    /// <summary>
    /// 单字节可以往返
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(35)]
    [InlineData(100)]
    [InlineData(255)]
    public void EncodeAndDecode_SingleByte_RoundTrip(int value)
    {
        byte[] source = [(byte)value];

        Assert.Equal(source, Base36.Decode(Base36.Encode(source)));
    }

    /// <summary>
    /// 空串解码得到空字节数组
    /// </summary>
    [Fact]
    public void Decode_WithEmptyString_ReturnsEmptyArray()
    {
        Assert.Empty(Base36.Decode(string.Empty));
    }

    /// <summary>
    /// 解码大小写敏感，小写字母与符号都不在字母表内
    /// </summary>
    [Theory]
    [InlineData("a")]
    [InlineData("-")]
    [InlineData("Z#")]
    [InlineData("曦")]
    public void Decode_WhenCharacterIsIllegal_Throws(string encoded)
    {
        var ex = Assert.Throws<ArgumentException>(() => Base36.Decode(encoded));
        Assert.Contains("Base36", ex.Message);
    }
}
