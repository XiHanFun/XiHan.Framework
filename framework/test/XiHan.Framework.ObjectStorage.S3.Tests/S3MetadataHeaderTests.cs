// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 自定义元数据的值照 RFC 2047 编码之后都能解回原值（ADR-0333）
/// </summary>
/// <remarks>
/// 线上的写法与读法由 <see cref="S3MetadataWireTests"/> 逐条核对；这里核对编码与解码互为逆运算、写出的每个 encoded-word 都合 RFC 2047 的长度与字符边界，以及值的核对漏过的孤立代理项不被换成 U+FFFD
/// </remarks>
public sealed class S3MetadataHeaderTests
{
    /// <summary>
    /// 契约允许的值
    /// </summary>
    /// <remarks>
    /// 只有 ASCII 的、要编码的 ASCII、非 ASCII、平面以外的字符、字符正好落在 45 字节边界两侧的、2048 字节上限附近的
    /// </remarks>
    public static TheoryData<string> Values =>
    [
        "",
        "plain",
        "a  b",
        " ",
        "  both  ",
        "=?UTF-8?B?YQ==?=",
        "=?=",
        "2026 年度报告",
        "\U0001F600\U0001F601",
        new string('a', 44) + "中",
        new string('a', 42) + "中" + "文",
        new string('a', 41) + "\U0001F600" + "b",
        string.Concat(Enumerable.Repeat("中", 680)),
        string.Concat(Enumerable.Repeat("\U0001F600", 510)),
        "Ä中\U0001F600 x",
    ];

    [Theory]
    [MemberData(nameof(Values))]
    public void EncodedValues_DecodeBackToTheOriginal(string value)
    {
        var encoded = S3MetadataHeader.Encode(value);

        Assert.True(S3MetadataHeader.TryDecode(encoded, out var decoded));
        Assert.Equal(value, decoded);
        Assert.DoesNotContain(encoded, static character => character is < ' ' or > '~');
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void EncodedWords_FitRfc2047_AndKeepEveryCharacterWhole(string value)
    {
        var encoded = S3MetadataHeader.Encode(value);
        if (ReferenceEquals(encoded, value))
        {
            return;
        }

        // 一个空格隔开，每个至多 75 个字符，每个单独解出的字节都是完整的 UTF-8
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var words = encoded.Split(' ');
        Assert.All(words, word =>
        {
            Assert.InRange(word.Length, 13, 75);
            Assert.StartsWith("=?UTF-8?B?", word, StringComparison.Ordinal);
            Assert.EndsWith("?=", word, StringComparison.Ordinal);
            _ = strict.GetString(Convert.FromBase64String(word[10..^2]));
        });
        Assert.Equal(value, string.Concat(words.Select(word => strict.GetString(Convert.FromBase64String(word[10..^2])))));
    }

    [Fact]
    public void ALoneSurrogate_IsNotReplaced()
    {
        // 值的核对（BlobPutOptions.Metadata）本该已经拒收：编码不换成 U+FFFD 照写。
        // 数据在代码里按码元组出：理论数据经序列化之后，孤立的代理项已经被换成了 U+FFFD
        string[] values = [new(['a', (char)0xD800, 'b']), new([(char)0xDC00]), new([(char)0xDC00, (char)0xD800]), "tail" + (char)0xD83D];
        Assert.All(values, static value => Assert.Throws<ArgumentException>(() => S3MetadataHeader.Encode(value)));
    }
}
