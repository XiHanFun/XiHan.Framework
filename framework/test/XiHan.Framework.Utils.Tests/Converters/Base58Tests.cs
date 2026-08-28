// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Converters;

namespace XiHan.Framework.Utils.Tests.Converters;

/// <summary>
/// Base58 编解码测试
/// </summary>
/// <remarks>
/// 只覆盖单字节、前导零与非法字符场景：多字节往返在当前实现下会颠倒字节序，
/// 详见交付报告的疑似缺陷段落，这里不写成迎合缺陷的断言。
/// </remarks>
public class Base58Tests
{
    /// <summary>
    /// 字母表排除了 0、O、I、l 四个易混字符
    /// </summary>
    [Theory]
    [InlineData(1, "2")]
    [InlineData(8, "9")]
    [InlineData(9, "A")]
    [InlineData(57, "z")]
    [InlineData(255, "5Q")]
    public void Encode_SingleByte_UsesBitcoinAlphabet(int value, string expected)
    {
        Assert.Equal(expected, Base58.Encode([(byte)value]));
    }

    /// <summary>
    /// 单字节可以往返
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(57)]
    [InlineData(100)]
    [InlineData(255)]
    public void EncodeAndDecode_SingleByte_RoundTrip(int value)
    {
        byte[] source = [(byte)value];

        Assert.Equal(source, Base58.Decode(Base58.Encode(source)));
    }

    /// <summary>
    /// 前导零字节编码成同样数量的 '1'，并可原样解回
    /// </summary>
    [Fact]
    public void EncodeAndDecode_LeadingZeroBytes_MapToLeadingOnes()
    {
        byte[] source = [0x00, 0x00, 0x00];

        var encoded = Base58.Encode(source);

        Assert.Equal("111", encoded);
        Assert.Equal(source, Base58.Decode(encoded));
    }

    /// <summary>
    /// 单个零字节编码为一个 '1'
    /// </summary>
    [Fact]
    public void EncodeAndDecode_SingleZeroByte_RoundTrip()
    {
        byte[] source = [0x00];

        Assert.Equal("1", Base58.Encode(source));
        Assert.Equal(source, Base58.Decode("1"));
    }

    /// <summary>
    /// 空串解码得到空字节数组
    /// </summary>
    [Fact]
    public void Decode_WithEmptyString_ReturnsEmptyArray()
    {
        Assert.Empty(Base58.Decode(string.Empty));
    }

    /// <summary>
    /// 易混字符与其他非法字符都会被拒绝
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("O")]
    [InlineData("I")]
    [InlineData("l")]
    [InlineData("2+")]
    public void Decode_WhenCharacterIsIllegal_Throws(string encoded)
    {
        Assert.Throws<FormatException>(() => Base58.Decode(encoded));
    }
}
