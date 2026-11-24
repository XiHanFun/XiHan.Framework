// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 按字符的固定窗口切块：换行统一为 <c>\n</c>，每块裁掉首尾空白，空块丢弃
/// </summary>
/// <remarks>
/// 规范 RFC-0022「知识库」（ADR-0286）：窗口与重叠按 Unicode 码位计，代理对算一个字符，窗口的边界不会落在代理对中间；窗口在统一换行之后的文本上按「窗口 − 重叠」滑动，到达文本结尾的那个窗口是最后一个；每个窗口切出来之后再去掉首尾的空白（RFC-0001「空白字符」），去掉之后为空的不成块，去空白不改变下一个窗口的位置
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class FixedWindowChunkingStrategy : IChunkingStrategy
{
    /// <inheritdoc />
    public IReadOnlyList<string> Chunk(string text, ChunkingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var size = options.MaxChunkSize;
        var step = size - Math.Min(options.Overlap, size - 1);
        var chunks = new List<string>();

        // 窗口的两端各是一个只往前走的下标（UTF-16 码元），每次按码位数前移：代理对整个跨过去，切不出半个字符
        var start = 0;
        var end = Advance(normalized, 0, size);
        while (true)
        {
            // 去空白在切分之后：只裁这一块的两端，下一个窗口仍从原文的位置算起
            var piece = normalized.AsSpan(start, end - start).Trim();
            if (!piece.IsEmpty)
            {
                chunks.Add(piece.ToString());
            }

            // 到达文本结尾的窗口是最后一个：再往后滑只会得到它的后缀
            if (end >= normalized.Length)
            {
                return chunks;
            }

            start = Advance(normalized, start, step);
            end = Advance(normalized, end, step);
        }
    }

    /// <summary>
    /// 从 <paramref name="index"/> 起往前走 <paramref name="count"/> 个码位之后的下标
    /// </summary>
    /// <remarks>
    /// 成对的代理项算一个码位、占两个码元；落单的代理项各算一个。走到文本结尾为止
    /// </remarks>
    private static int Advance(string text, int index, int count)
    {
        while (count > 0 && index < text.Length)
        {
            index += char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
            count--;
        }

        return index;
    }
}
