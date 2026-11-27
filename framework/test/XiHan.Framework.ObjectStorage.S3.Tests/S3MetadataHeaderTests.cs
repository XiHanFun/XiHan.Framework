// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;

namespace XiHan.Framework.ObjectStorage.S3.Tests;

/// <summary>
/// 自定义元数据的值编码之后都能解回原值，原样的字节按 UTF-8 解出
/// </summary>
/// <remarks>
/// 照 RFC 2047 编解码（ADR-0333），读回的原样字节严格按 UTF-8 解出（ADR-0343）。线上的写法与读法由 <see cref="S3MetadataWireTests"/> 与 <see cref="S3MetadataRawBytesWireTests"/> 逐条核对；这里核对编码与解码互为逆运算、写出的每个 encoded-word 都合 RFC 2047 的长度与字符边界，值的核对漏过的孤立代理项不被换成 U+FFFD，写入时拒收的形状与读回时解码的形状是同一个，以及原样字节的还原
/// </remarks>
public sealed class S3MetadataHeaderTests
{
    /// <summary>
    /// 契约允许的值
    /// </summary>
    /// <remarks>
    /// 只有 ASCII 的、要编码的 ASCII、非 ASCII、平面以外的字符、字符正好落在 45 字节边界两侧的、2048 字节上限附近的。整个是 encoded-word 形状的值写入之前就拒收，不在此列（ADR-0343）
    /// </remarks>
    public static TheoryData<string> Values =>
    [
        "",
        "plain",
        "a  b",
        " ",
        "  both  ",
        " =?UTF-8?B?YQ==?=",
        "=?x?=",
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

    [Theory]
    [InlineData("=?UTF-8?B?YQ==?=", true)]
    [InlineData("=?UTF-8?B?5Lit?= =?UTF-8?Q?x?=", true)]
    [InlineData("=?UTF-8?B?5Lit?=\t=?UTF-8?B?5paH?=", true)]
    [InlineData("=?ISO-8859-1?Q?caf=E9?=", true)]
    [InlineData("=?x?y?z?=", true)]
    [InlineData("=?x?=", false)]
    [InlineData("=?UTF-8?B??=", false)]
    [InlineData(" =?UTF-8?B?YQ==?=", false)]
    [InlineData("=?UTF-8?B?YQ==?= ", false)]
    [InlineData("note =?UTF-8?B?YQ==?=", false)]
    [InlineData("=?UTF-8?B?5Lit?==?UTF-8?B?5paH?=", false)]
    [InlineData("", false)]
    public void TheRefusedShape_IsTheShapeThatIsDecodedOnRead(string value, bool shaped)
    {
        // 写入时拒收的与读回时解码的是同一个判定：拒收的都会被解码（解得了解不了都算），不拒收的读回原样交出（ADR-0343）
        Assert.Equal(shaped, S3MetadataHeader.IsEncodedWords(value));
        if (shaped)
        {
            Assert.Throws<ArgumentException>(() => S3MetadataHeader.Encode(value));
            Assert.True(!S3MetadataHeader.TryDecode(value, out var decoded) || decoded != value);
        }
        else
        {
            Assert.True(S3MetadataHeader.TryDecode(value, out var decoded));
            Assert.Equal(value, decoded);
        }
    }

    [Theory]
    [InlineData("中文 café")]
    [InlineData("\U0001F600 smile")]
    [InlineData("Ünïcödé")]
    [InlineData("说明 =?UTF-8?B?5Lit?=")]
    public void RawUtf8BytesReadAsLatin1_DecodeToTheOriginal(string original)
    {
        // .NET 的 HTTP 客户端把原样写进头的每个字节读成一个 U+0000–U+00FF 的字符：还原成字节再严格按 UTF-8 解出，夹着的 encoded-word 不再解（ADR-0343）
        var answered = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes(original));

        Assert.True(S3MetadataHeader.IsRaw(answered));
        Assert.True(S3MetadataHeader.TryDecode(answered, out var decoded));
        Assert.Equal(original, decoded);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0x41 })]
    [InlineData(new byte[] { 0xE4, 0xB8 })]
    [InlineData(new byte[] { 0x63, 0x61, 0x66, 0xE9 })]
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })]
    [InlineData(new byte[] { 0x61, 0xC2, 0x85, 0x62 })]
    [InlineData(new byte[] { 0xE4, 0xB8, 0xAD, 0x09, 0x61 })]
    public void RawBytesThatAreNotAValidValue_DoNotDecode(byte[] bytes)
    {
        // 不是合法的 UTF-8（截断、Latin-1 的 é、编成 UTF-8 的代理项），或解出控制字符（U+0085、制表符）：不交出乱码，也不换成 U+FFFD
        Assert.False(S3MetadataHeader.TryDecode(Encoding.Latin1.GetString(bytes), out var decoded));
        Assert.Null(decoded);
    }

    [Fact]
    public void ACharacterBeyondLatin1_CannotBeTurnedBackIntoBytes()
    {
        // 应用注册了按别的编码读头的客户端时，交来的已经不是一个字符一个字节：还原不出服务发来的字节，不猜
        Assert.False(S3MetadataHeader.TryDecode("中文", out _));
        Assert.False(S3MetadataHeader.TryDecode("café 中", out _));
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
