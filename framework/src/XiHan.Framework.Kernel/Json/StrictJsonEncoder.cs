// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// 遇到写不成合法 JSON 文本的字符串即抛出的编码器
/// </summary>
/// <remarks>
/// 包在序列化上下文自己的编码器外面，转义原样交给它做，写出的字节与原来相同；每个字符串交给它之前多一道核对：以 UTF-16 交来的有孤立的代理项、以 UTF-8 交来的不是合法的 UTF-8，即抛出 <see cref="ArgumentException"/>。
/// System.Text.Json 的写出器缺省把它们换成 U+FFFD 写成转义，得到的是另一份合法的 JSON（RFC-0001「写出与转交」、RFC-0011 规则 10「编码」，ADR-0318）。
/// 覆写 <see cref="TextEncoder"/> 带指针参数的抽象成员只能在 unsafe 上下文里：这里只把指针包成 span 或原样转交，不做指针运算
/// </remarks>
/// <param name="inner">序列化上下文的编码器；没有设置时传 <see cref="JavaScriptEncoder.Default"/></param>
internal sealed class StrictJsonEncoder(JavaScriptEncoder inner) : JavaScriptEncoder
{
    /// <summary>
    /// 写不出时异常的消息
    /// </summary>
    private const string UnwritableMessage = "The value has a string with a lone surrogate or invalid UTF-8, which cannot be written as JSON text.";

    /// <summary>
    /// 代理项的最小码元
    /// </summary>
    private const char MinSurrogate = (char)0xD800;

    /// <summary>
    /// 代理项的最大码元
    /// </summary>
    private const char MaxSurrogate = (char)0xDFFF;

    /// <summary>
    /// 被包住的编码器
    /// </summary>
    private readonly JavaScriptEncoder _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    public override int MaxOutputCharactersPerInputCharacter => _inner.MaxOutputCharactersPerInputCharacter;

    /// <inheritdoc />
    public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
    {
        // 写出器对每个以 UTF-16 交来的字符串（值与成员名、字典的键）先问这一句，再决定原样写还是转义：核对放在这里，哪个字符串都绕不过去
        if (HasLoneSurrogate(new ReadOnlySpan<char>(text, textLength)))
        {
            throw new ArgumentException(UnwritableMessage);
        }

        return _inner.FindFirstCharacterToEncode(text, textLength);
    }

    /// <inheritdoc />
    public override int FindFirstCharacterToEncodeUtf8(ReadOnlySpan<byte> utf8Text)
    {
        // 以 UTF-8 交来的字符串（转换器直接给的字节）同理：非法的字节序列缺省也换成 U+FFFD
        if (!Utf8.IsValid(utf8Text))
        {
            throw new ArgumentException(UnwritableMessage);
        }

        return _inner.FindFirstCharacterToEncodeUtf8(utf8Text);
    }

    /// <inheritdoc />
    public override OperationStatus Encode(ReadOnlySpan<char> source, Span<char> destination, out int charsConsumed, out int charsWritten, bool isFinalBlock = true)
        => _inner.Encode(source, destination, out charsConsumed, out charsWritten, isFinalBlock);

    /// <inheritdoc />
    public override OperationStatus EncodeUtf8(ReadOnlySpan<byte> utf8Source, Span<byte> utf8Destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock = true)
        => _inner.EncodeUtf8(utf8Source, utf8Destination, out bytesConsumed, out bytesWritten, isFinalBlock);

    /// <inheritdoc />
    public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        => _inner.TryEncodeUnicodeScalar(unicodeScalar, buffer, bufferLength, out numberOfCharactersWritten);

    /// <inheritdoc />
    public override bool WillEncode(int unicodeScalar) => _inner.WillEncode(unicodeScalar);

    /// <summary>
    /// 文本里是否有孤立的代理项
    /// </summary>
    /// <remarks>
    /// 高代理项后面紧跟低代理项才成对；单独的低代理项、反过来的一对与结尾的高代理项都是孤立的
    /// </remarks>
    private static bool HasLoneSurrogate(ReadOnlySpan<char> text)
    {
        // 先整段找代理项（向量化）：多数字符串里没有，一遍就完；找到才从那里逐个看是否成对
        var start = text.IndexOfAnyInRange(MinSurrogate, MaxSurrogate);
        if (start < 0)
        {
            return false;
        }

        for (var index = start; index < text.Length; index++)
        {
            var current = text[index];
            if (!char.IsSurrogate(current))
            {
                continue;
            }

            if (char.IsHighSurrogate(current) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
                continue;
            }

            return true;
        }

        return false;
    }
}
