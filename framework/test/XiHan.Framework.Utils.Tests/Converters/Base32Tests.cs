// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Converters;

namespace XiHan.Framework.Utils.Tests.Converters;

/// <summary>
/// Base32 编解码测试
/// </summary>
/// <remarks>
/// 用 RFC 4648 的官方测试向量钉死字母表与分组口径，这是该编码对外互通的硬约束，
/// 一旦漂移，TOTP 之类的调用方会静默算错。
/// </remarks>
public class Base32Tests
{
    /// <summary>
    /// 编码结果与 RFC 4648 测试向量一致（本实现不产生尾部填充符）
    /// </summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Encode_MatchesRfc4648Vectors(string text, string expected)
    {
        Assert.Equal(expected, Base32.Encode(Encoding.ASCII.GetBytes(text)));
    }

    /// <summary>
    /// 解码结果与 RFC 4648 测试向量一致
    /// </summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("MY", "f")]
    [InlineData("MZXQ", "fo")]
    [InlineData("MZXW6", "foo")]
    [InlineData("MZXW6YQ", "foob")]
    [InlineData("MZXW6YTB", "fooba")]
    [InlineData("MZXW6YTBOI", "foobar")]
    public void Decode_MatchesRfc4648Vectors(string encoded, string expected)
    {
        Assert.Equal(Encoding.ASCII.GetBytes(expected), Base32.Decode(encoded));
    }

    /// <summary>
    /// 任意字节序列可以往返
    /// </summary>
    [Fact]
    public void EncodeAndDecode_RoundTripArbitraryBytes()
    {
        byte[][] samples =
        [
            [],
            [0x00],
            [0xFF],
            [0x01, 0x02, 0x03],
            [0x00, 0x00, 0x00, 0x00, 0x00],
            [0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x11, 0x22]
        ];

        foreach (var sample in samples)
        {
            Assert.Equal(sample, Base32.Decode(Base32.Encode(sample)));
        }
    }

    /// <summary>
    /// 解码忽略尾部填充符与大小写差异
    /// </summary>
    [Fact]
    public void Decode_IgnoresPaddingAndCase()
    {
        var expected = Encoding.ASCII.GetBytes("foo");

        Assert.Equal(expected, Base32.Decode("MZXW6==="));
        Assert.Equal(expected, Base32.Decode("mzxw6"));
        Assert.Equal(expected, Base32.Decode("  MZXW6  "));
    }

    /// <summary>
    /// 空输入编解码得到空结果
    /// </summary>
    [Fact]
    public void EncodeAndDecode_WithEmptyInput_ReturnEmptyResult()
    {
        Assert.Equal(string.Empty, Base32.Encode([]));
        Assert.Empty(Base32.Decode(string.Empty));
        Assert.Empty(Base32.Decode("===="));
    }

    /// <summary>
    /// 输入为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void EncodeAndDecode_WhenInputIsNull_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => Base32.Encode(null!));
        Assert.Throws<ArgumentNullException>(() => Base32.Decode(null!));
    }

    /// <summary>
    /// 出现字母表以外的字符时抛参数异常
    /// </summary>
    [Theory]
    [InlineData("MZXW1")]
    [InlineData("MZXW-")]
    [InlineData("曦寒")]
    public void Decode_WhenCharacterIsIllegal_Throws(string encoded)
    {
        Assert.Throws<ArgumentException>(() => Base32.Decode(encoded));
    }
}
