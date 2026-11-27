// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// 自定义元数据的值在 <c>x-amz-meta-*</c> 头里的写法
/// </summary>
/// <remarks>
/// S3 的用户元数据经 HTTP 头存取，头值只能是可打印 ASCII 与空格，首尾的空白不算在值里（RFC 9110 §5.5）；.NET 的 HTTP 客户端不发出含非 ASCII 字符的请求头。头写不出原样的值照 RFC 2047 编成 <c>=?UTF-8?B?…?=</c>，读回时解码（RFC-0012「S3 Provider」，ADR-0333）。整个是 encoded-word 形状的只有 ASCII 的值存不成原样（Amazon S3 先解码再存，读回时又被解码），由 <see cref="IsEncodedWords"/> 认出、写入之前拒收；别的写入方原样写进头的 UTF-8 字节读回时还原成字节、严格按 UTF-8 解出（ADR-0343）。不用 SDK 的 <c>AWSConfigsS3.EnableUnicodeEncodingForObjectMetadata</c>：它是进程级的全局开关，且逐个 UTF-16 码元做百分号编码、拆开成对的代理项
/// </remarks>
internal static class S3MetadataHeader
{
    /// <summary>
    /// encoded-word 的开头
    /// </summary>
    private const string WordStart = "=?";

    /// <summary>
    /// encoded-word 的结尾
    /// </summary>
    private const string WordEnd = "?=";

    /// <summary>
    /// 写出的 encoded-word 在 base64 之前的部分
    /// </summary>
    private const string WrittenPrefix = "=?UTF-8?B?";

    /// <summary>
    /// 一个写出的 encoded-word 至多带的字节数
    /// </summary>
    /// <remarks>
    /// RFC 2047 §2 的 encoded-word 至多 75 个字符：<c>=?UTF-8?B?</c> 与 <c>?=</c> 占 12 个，base64 至多 63 个、取 4 的倍数是 60 个，即 45 个字节
    /// </remarks>
    private const int MaxWordBytes = 45;

    /// <summary>
    /// RFC 2047 的 especials：token 里不能有这些字符
    /// </summary>
    private static readonly SearchValues<char> _especials = SearchValues.Create("()<>@,;:\\\"/[]?.=");

    /// <summary>
    /// 严格的 UTF-8：编码遇到孤立的代理项、解码遇到非法字节即抛出，不换成 U+FFFD
    /// </summary>
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// 写进请求头的值
    /// </summary>
    /// <remarks>
    /// 值里有 U+0020–U+007E 之外的字符、以空格开头或结尾，或以 <c>=?</c> 开头且以 <c>?=</c> 结尾时，编成以一个空格隔开的若干 encoded-word，每个至多 45 个字节、在 UTF-8 字符的边界处拆开；其余原样。值已过 <c>BlobPutOptions.Metadata</c> 的核对（不含控制字符与孤立的代理项），整个是 encoded-word 形状的值也已由调用方按 <see cref="IsEncodedWords"/> 拒收
    /// </remarks>
    public static string Encode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        // 整个是 encoded-word 形状的值原样写出读回时被解码，编码写出 Amazon S3 又先解码再存：调用方在写入之前拒收（ADR-0343），走到这里是核对被绕过
        if (IsEncodedWords(value))
        {
            throw new ArgumentException("The metadata value is shaped as RFC 2047 encoded-words, which the S3 service would not keep as given.", nameof(value));
        }

        if (!NeedsEncoding(value))
        {
            return value;
        }

        var words = new StringBuilder();
        var chunk = new byte[MaxWordBytes];
        var filled = 0;
        Span<byte> encoded = stackalloc byte[4];
        var rest = value.AsSpan();
        while (!rest.IsEmpty)
        {
            // 孤立的代理项编不成 UTF-8：值的核对本该已经拒收，这里不换成 U+FFFD 照写（ADR-0322）
            if (Rune.DecodeFromUtf16(rest, out var character, out var consumed) != OperationStatus.Done)
            {
                throw new ArgumentException("The metadata value contains lone surrogates.", nameof(value));
            }

            // 按 Unicode 标量值累积字节：下一个字符放不进这个 encoded-word 就先写出已有的，一个字符（至多 4 个字节）不拆开
            var length = character.EncodeToUtf8(encoded);
            if (filled + length > MaxWordBytes)
            {
                AppendWord(words, chunk.AsSpan(0, filled));
                filled = 0;
            }

            encoded[..length].CopyTo(chunk.AsSpan(filled));
            filled += length;
            rest = rest[consumed..];
        }

        AppendWord(words, chunk.AsSpan(0, filled));
        return words.ToString();
    }

    /// <summary>
    /// 解出应答头里的值
    /// </summary>
    /// <remarks>
    /// 有 U+0080 及以上字符的值（<see cref="IsRaw"/>）是别的写入方原样写进头的字节：.NET 的 HTTP 客户端把每个字节读成一个 U+0000–U+00FF 的字符（Latin-1），还原成字节再严格按 UTF-8 解码（ADR-0343）。只有 ASCII 的值整个是以空格或制表符隔开的一个或多个 encoded-word 时解码：相邻 encoded-word 之间的空白忽略（RFC 2047 §6.2），各个解出的字节连起来按 UTF-8 解码；不是这样的值原样交出。解不了时为假：charset 不是 UTF-8（可带 RFC 2231 的 <c>*语言</c> 后缀）、encoding 不是 B 或 Q、B 不是带补齐的 base64、Q 的 <c>=</c> 之后不是两位十六进制数字、字节不是合法的 UTF-8、解出的值含控制字符；原样的字节里有 U+00FF 之外的字符（不是按 Latin-1 交来的，还原不了）同此。单个 encoded-word 超过 75 个字符照常解码：别的写入方不一定拆开
    /// </remarks>
    public static bool TryDecode(string value, [NotNullWhen(true)] out string? decoded)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (IsRaw(value))
        {
            // 每个字符就是服务发来的一个字节：U+00FF 之外的字符不是这样交来的（应用注册了按别的编码读头的客户端），还原不出字节，不猜（ADR-0343）
            if (value.AsSpan().ContainsAnyExceptInRange('\u0000', (char)0xFF))
            {
                decoded = null;
                return false;
            }

            return TryDecodeUtf8(Encoding.Latin1.GetBytes(value), out decoded);
        }

        decoded = value;
        if (!IsEncodedWords(value))
        {
            return true;
        }

        var bytes = new List<byte>(value.Length);
        foreach (var word in value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = word[WordStart.Length..^WordEnd.Length].Split('?');
            var charset = parts[0];
            // RFC 2231 §5 允许 charset 之后带「*语言」，语言不影响字节
            var language = charset.IndexOf('*', StringComparison.Ordinal);
            if (language >= 0)
            {
                charset = charset[..language];
            }

            if (!string.Equals(charset, "UTF-8", StringComparison.OrdinalIgnoreCase) || !TryAppendBytes(parts[1], parts[2], bytes))
            {
                decoded = null;
                return false;
            }
        }

        return TryDecodeUtf8([.. bytes], out decoded);
    }

    /// <summary>
    /// 应答头里的值是不是别的写入方原样写进头的字节
    /// </summary>
    /// <remarks>
    /// 有 U+0080 及以上的字符即是：Provider 写出的总是只有 ASCII 的值，encoded-word 也只有 ASCII（ADR-0343）
    /// </remarks>
    public static bool IsRaw(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.AsSpan().ContainsAnyExceptInRange('\u0000', '\u007F');
    }

    /// <summary>
    /// 值是否整个是以空格或制表符隔开的一个或多个 RFC 2047 encoded-word
    /// </summary>
    /// <remarks>
    /// 读回时解码的就是这个形状，写入时拒收的也是它，两处同一个判定（ADR-0333 备选 7、ADR-0343）：为空、首尾有空白、有一部分不是 encoded-word 的都不是。encoded-word 只有 ASCII，这样的值因此只有 ASCII
    /// </remarks>
    public static bool IsEncodedWords(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        // 首尾有空白的不算：HTTP 本来就去掉首尾的空白，读回时原样交出
        if (value.Length == 0 || IsLinearWhiteSpace(value[0]) || IsLinearWhiteSpace(value[^1]))
        {
            return false;
        }

        return value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).All(IsEncodedWord);
    }

    /// <summary>
    /// 值是否要编码才能原样存取
    /// </summary>
    /// <remarks>
    /// 非 ASCII 的头写不出，首尾的空格被 HTTP 去掉。以 <c>=?</c> 开头且以 <c>?=</c> 结尾的值（整个是 encoded-word 形状的已由调用方拒收）原样写出时，RustFS 1.0.6 会当作 encoded-word 去解、解不了即以 400 <c>InvalidArgument</c> 拒收（ADR-0343 实测），所以照旧编码
    /// </remarks>
    private static bool NeedsEncoding(string value)
        => value.AsSpan().ContainsAnyExceptInRange(' ', '~')
            || value.StartsWith(' ')
            || value.EndsWith(' ')
            || (value.StartsWith(WordStart, StringComparison.Ordinal) && value.EndsWith(WordEnd, StringComparison.Ordinal));

    /// <summary>
    /// 严格按 UTF-8 解出字节，并核对解出的值没有控制字符
    /// </summary>
    /// <remarks>
    /// 字节不是合法的 UTF-8（含编成 UTF-8 的代理项）或解出控制字符时为假。解出的值也要合元数据值的文法：头里写不出的控制字符经编码或原样的字节就能带进来，交出去调用方原样写回即被拒绝
    /// </remarks>
    private static bool TryDecodeUtf8(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out string? decoded)
    {
        try
        {
            decoded = _strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            decoded = null;
            return false;
        }

        if (decoded.AsSpan().ContainsAnyInRange('\u0000', '\u001F') || decoded.AsSpan().ContainsAnyInRange('\u007F', '\u009F'))
        {
            decoded = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 是否是线性空白（空格或制表符）
    /// </summary>
    private static bool IsLinearWhiteSpace(char character) => character is ' ' or '\t';

    /// <summary>
    /// 追加一个写出的 encoded-word，与前一个以一个空格隔开
    /// </summary>
    private static void AppendWord(StringBuilder words, ReadOnlySpan<byte> bytes)
    {
        if (words.Length > 0)
        {
            words.Append(' ');
        }

        words.Append(WrittenPrefix).Append(Convert.ToBase64String(bytes)).Append(WordEnd);
    }

    /// <summary>
    /// 是否合 RFC 2047 §2 的 encoded-word 文法
    /// </summary>
    /// <remarks>
    /// <c>=?charset?encoding?encoded-text?=</c>：charset 与 encoding 是不含空格、控制字符与 especials 的 token，encoded-text 是 <c>?</c> 与空格之外的可打印 ASCII、至少一个字符。只看文法，不看 charset 与 encoding 认不认得：认不得的是解不了，不是原样交出
    /// </remarks>
    private static bool IsEncodedWord(string word)
    {
        if (word.Length < WordStart.Length + WordEnd.Length || !word.StartsWith(WordStart, StringComparison.Ordinal) || !word.EndsWith(WordEnd, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = word[WordStart.Length..^WordEnd.Length].Split('?');
        return parts.Length == 3
            && IsToken(parts[0])
            && IsToken(parts[1])
            && parts[2].Length > 0
            && !parts[2].AsSpan().ContainsAnyExceptInRange('!', '~');
    }

    /// <summary>
    /// 是否是 RFC 2047 的 token
    /// </summary>
    private static bool IsToken(string text)
        => text.Length > 0 && !text.AsSpan().ContainsAnyExceptInRange('!', '~') && !text.AsSpan().ContainsAny(_especials);

    /// <summary>
    /// 按 encoding 解出 encoded-text 的字节，追加到 <paramref name="bytes"/>
    /// </summary>
    /// <remarks>
    /// encoding 不区分大小写；不是 B 或 Q、文本解不了时为假
    /// </remarks>
    private static bool TryAppendBytes(string encoding, string text, List<byte> bytes)
    {
        if (string.Equals(encoding, "B", StringComparison.OrdinalIgnoreCase))
        {
            // 文本里没有空白（文法已排除），TryFromBase64String 因此只认长度为 4 的倍数、带补齐的标准 base64
            var buffer = new byte[text.Length / 4 * 3];
            if (!Convert.TryFromBase64String(text, buffer, out var written))
            {
                return false;
            }

            bytes.AddRange(buffer.AsSpan(0, written));
            return true;
        }

        if (!string.Equals(encoding, "Q", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // RFC 2047 §4.2：_ 是空格，=XX 是一个字节（十六进制不区分大小写），其余字符就是它自己
        for (var index = 0; index < text.Length; index++)
        {
            switch (text[index])
            {
                case '_':
                    bytes.Add(0x20);
                    break;
                case '=' when index + 2 < text.Length && Uri.IsHexDigit(text[index + 1]) && Uri.IsHexDigit(text[index + 2]):
                    bytes.Add(byte.Parse(text.AsSpan(index + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                    index += 2;
                    break;
                case '=':
                    return false;
                default:
                    bytes.Add((byte)text[index]);
                    break;
            }
        }

        return true;
    }
}
