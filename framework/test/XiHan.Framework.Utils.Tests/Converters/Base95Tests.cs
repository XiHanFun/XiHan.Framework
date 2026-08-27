// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Converters;

namespace XiHan.Framework.Utils.Tests.Converters;

/// <summary>
/// Base95 编解码测试
/// </summary>
/// <remarks>
/// 只覆盖单字节、零值与非法字符场景：多字节往返在当前实现下会颠倒字节序，
/// 详见交付报告的疑似缺陷段落，这里不写成迎合缺陷的断言。
/// </remarks>
public class Base95Tests
{
    /// <summary>
    /// 字母表从 ASCII 32 开始按序映射
    /// </summary>
    [Theory]
    [InlineData(1, "!")]
    [InlineData(16, "0")]
    [InlineData(33, "A")]
    [InlineData(94, "~")]
    public void Encode_SingleByte_MapsToPrintableAscii(int value, string expected)
    {
        Assert.Equal(expected, Base95.Encode([(byte)value]));
    }

    /// <summary>
    /// 零值编码为字母表首字符（空格）
    /// </summary>
    [Fact]
    public void Encode_WithZeroValue_ReturnsFirstAlphabetChar()
    {
        Assert.Equal(" ", Base95.Encode([0x00]));
        Assert.Equal(" ", Base95.Encode([]));
    }

    /// <summary>
    /// 单字节可以往返
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(94)]
    [InlineData(95)]
    [InlineData(255)]
    public void EncodeAndDecode_SingleByte_RoundTrip(int value)
    {
        byte[] source = [(byte)value];

        Assert.Equal(source, Base95.Decode(Base95.Encode(source)));
    }

    /// <summary>
    /// 空串解码得到空字节数组
    /// </summary>
    [Fact]
    public void Decode_WithEmptyString_ReturnsEmptyArray()
    {
        Assert.Empty(Base95.Decode(string.Empty));
    }

    /// <summary>
    /// 可打印区间之外的字符会被拒绝
    /// </summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\t")]
    [InlineData("曦")]
    public void Decode_WhenCharacterIsOutOfRange_Throws(string encoded)
    {
        var ex = Assert.Throws<ArgumentException>(() => Base95.Decode(encoded));
        Assert.Contains("Base95", ex.Message);
    }
}
