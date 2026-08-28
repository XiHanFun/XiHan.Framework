// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// 编码扩展方法测试
/// </summary>
/// <remarks>
/// FromStringToBinary/FromBinaryToString 原先编码端按变长八进制写出、解码端按十进制读回，两侧口径不一致无法往返，
/// 已按产品缺陷修复为「每字节定长 3 位八进制」；其往返由 TextWatermarkHelperTests 经水印链路锁定，这里不重复覆盖。
/// </remarks>
public class EncodingExtensionsTests
{
    /// <summary>
    /// Base32 编码符合 RFC 4648 测试向量
    /// </summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Base32Encode_MatchesRfc4648Vectors(string input, string expected)
    {
        Assert.Equal(expected, input.Base32Encode());
    }

    /// <summary>
    /// Base32 编解码可往返，包括中文
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("hello world")]
    [InlineData("曦寒框架")]
    public void Base32EncodeAndDecode_RoundTrip(string input)
    {
        Assert.Equal(input, input.Base32Encode().Base32Decode());
    }

    /// <summary>
    /// Base64 编码与标准实现一致且可往返
    /// </summary>
    [Fact]
    public void Base64EncodeAndDecode_RoundTrip()
    {
        const string Input = "曦寒 XiHan";

        var encoded = Input.Base64Encode();

        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(Input)), encoded);
        Assert.Equal(Input, encoded.Base64Decode());
    }

    /// <summary>
    /// HTML 编码转义尖括号并可解码还原
    /// </summary>
    [Fact]
    public void HtmlEncodeAndDecode_RoundTrip()
    {
        const string Input = "<b>曦寒</b>";

        var encoded = Input.HtmlEncode();

        Assert.DoesNotContain("<b>", encoded);
        Assert.Equal(Input, encoded.HtmlDecode());
    }

    /// <summary>
    /// URL 编码把空格写成加号并可解码还原
    /// </summary>
    [Fact]
    public void UrlEncodeAndDecode_RoundTrip()
    {
        const string Input = "a b&c=d";

        var encoded = Input.UrlEncode();

        Assert.DoesNotContain(" ", encoded);
        Assert.Equal(Input, encoded.UrlDecode());
    }

    /// <summary>
    /// Unicode 编码输出四位十六进制转义并可解码还原
    /// </summary>
    [Fact]
    public void UnicodeEncodeAndDecode_RoundTrip()
    {
        // 拼接构造期望值，避免源码里直接出现四位十六进制转义序列
        var escapedA = "\\" + "u0041";

        Assert.Equal(escapedA, "A".UnicodeEncode());
        Assert.Equal("A", escapedA.UnicodeDecode());
        Assert.Equal("曦寒", "曦寒".UnicodeEncode().UnicodeDecode());
    }

    /// <summary>
    /// 未包含转义序列的文本解码后保持原样
    /// </summary>
    [Fact]
    public void UnicodeDecode_WhenNoEscapeSequence_ReturnsSameText()
    {
        Assert.Equal("plain text", "plain text".UnicodeDecode());
    }

    /// <summary>
    /// 二进制编解码走 UTF8 并可往返
    /// </summary>
    [Fact]
    public void BinaryEncodeAndDecode_RoundTrip()
    {
        const string Input = "曦寒";

        var bytes = Input.BinaryEncode();

        Assert.Equal(Encoding.UTF8.GetBytes(Input), bytes);
        Assert.Equal(Input, bytes.BinaryDecode());
    }

    /// <summary>
    /// 字符串转流后内容与 UTF8 字节一致
    /// </summary>
    [Fact]
    public void ToStream_ProducesUtf8Content()
    {
        const string Input = "曦寒";

        using var stream = Input.ToStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        Assert.Equal(Input, reader.ReadToEnd());
    }

    /// <summary>
    /// 转义处理反斜杠与常见控制字符
    /// </summary>
    [Fact]
    public void EscapeString_EscapesBackslashAndControlChars()
    {
        Assert.Equal(@"a\\b", @"a\b".EscapeString());
        Assert.Equal(@"a\nb", "a\nb".EscapeString());
        Assert.Equal(@"a\tb", "a\tb".EscapeString());
        Assert.Equal("a\\\"b", "a\"b".EscapeString());
    }

    /// <summary>
    /// 空串与 null 直接原样返回
    /// </summary>
    [Fact]
    public void EscapeStringAndUnescapeString_WhenEmpty_ReturnSameText()
    {
        Assert.Equal(string.Empty, string.Empty.EscapeString());
        Assert.Equal(string.Empty, string.Empty.UnescapeString());
    }

    /// <summary>
    /// 不含反斜杠的文本转义后可原样反转义回来
    /// </summary>
    [Fact]
    public void UnescapeString_RestoresEscapedText()
    {
        const string Input = "line1\nline2\tquote\"end";

        Assert.Equal(Input, Input.EscapeString().UnescapeString());
    }

    /// <summary>
    /// C# 风格转义把不可打印字符写成 \\uXXXX
    /// </summary>
    [Fact]
    public void EscapeForCSharp_EscapesQuotesAndControlChars()
    {
        Assert.Equal("a\\\"b", "a\"b".EscapeForCSharp());
        Assert.Equal(@"a\\b", @"a\b".EscapeForCSharp());
        Assert.Equal(@"a\nb", "a\nb".EscapeForCSharp());
        Assert.Equal(@"\u0001", "\u0001".EscapeForCSharp());
        Assert.Equal("曦寒", "曦寒".EscapeForCSharp());
    }

    /// <summary>
    /// JSON 风格转义额外处理正斜杠，且不转义单引号
    /// </summary>
    [Fact]
    public void EscapeForJson_EscapesSlashButNotSingleQuote()
    {
        Assert.Equal(@"a\/b", "a/b".EscapeForJson());
        Assert.Equal("a\\\"b", "a\"b".EscapeForJson());
        Assert.Equal(@"a\nb", "a\nb".EscapeForJson());
        Assert.Equal("a'b", "a'b".EscapeForJson());
        Assert.Equal(@"\u0001", "\u0001".EscapeForJson());
    }

    /// <summary>
    /// 空串转义后仍为空串
    /// </summary>
    [Fact]
    public void EscapeForCSharpAndJson_WhenEmpty_ReturnEmpty()
    {
        Assert.Equal(string.Empty, string.Empty.EscapeForCSharp());
        Assert.Equal(string.Empty, string.Empty.EscapeForJson());
    }
}
