// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Converters;

namespace XiHan.Framework.Utils.Tests.Converters;

/// <summary>
/// 自定义进制编码器测试
/// </summary>
/// <remarks>
/// 只覆盖构造校验、单字节编解码与非法字符：多字节输入在当前实现下会因为结果缓冲区
/// 估算公式写反而越界，详见交付报告的疑似缺陷段落，这里不构造会崩的用例。
/// </remarks>
public class CustomRadixTests
{
    private const string Alphabet36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// 字符集少于两个字符时抛参数异常
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("0")]
    public void Constructor_WhenAlphabetTooShort_Throws(string alphabet)
    {
        var ex = Assert.Throws<ArgumentException>(() => new CustomRadix(alphabet));
        Assert.Contains("字符集长度", ex.Message);
    }

    /// <summary>
    /// 字符集出现重复字符时抛参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenAlphabetHasDuplicates_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new CustomRadix("01201"));
        Assert.Contains("字符重复", ex.Message);
    }

    /// <summary>
    /// 恰好两个字符的字符集可以构造
    /// </summary>
    [Fact]
    public void Constructor_WithMinimalAlphabet_Succeeds()
    {
        var radix = new CustomRadix("01");

        Assert.Equal("1", radix.Encode([0x01]));
    }

    /// <summary>
    /// 按自定义字符集编码单字节
    /// </summary>
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(35, "Z")]
    [InlineData(100, "2S")]
    [InlineData(255, "73")]
    public void Encode_SingleByte_UsesGivenAlphabet(int value, string expected)
    {
        var radix = new CustomRadix(Alphabet36);

        Assert.Equal(expected, radix.Encode([(byte)value]));
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
        var radix = new CustomRadix(Alphabet36);
        byte[] source = [(byte)value];

        Assert.Equal(source, radix.Decode(radix.Encode(source)));
    }

    /// <summary>
    /// 空串解码得到空字节数组
    /// </summary>
    [Fact]
    public void Decode_WithEmptyString_ReturnsEmptyArray()
    {
        var radix = new CustomRadix(Alphabet36);

        Assert.Empty(radix.Decode(string.Empty));
    }

    /// <summary>
    /// 出现字符集以外的字符时抛参数异常
    /// </summary>
    [Theory]
    [InlineData("a")]
    [InlineData("-")]
    [InlineData("曦")]
    public void Decode_WhenCharacterIsIllegal_Throws(string encoded)
    {
        var radix = new CustomRadix(Alphabet36);

        var ex = Assert.Throws<ArgumentException>(() => radix.Decode(encoded));
        Assert.Contains("非法字符", ex.Message);
    }

    /// <summary>
    /// 不同字符集对同一字节给出不同表示
    /// </summary>
    [Fact]
    public void Encode_WithDifferentAlphabets_ProducesDifferentText()
    {
        var decimalRadix = new CustomRadix("0123456789");
        var hexRadix = new CustomRadix("0123456789ABCDEF");

        Assert.Equal("15", decimalRadix.Encode([0x0F]));
        Assert.Equal("F", hexRadix.Encode([0x0F]));
    }
}
