// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 按字符的固定窗口切块：换行统一为 <c>\n</c>，每块裁掉首尾空白，空块丢弃。
/// </summary>
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
        var overlap = Math.Min(options.Overlap, size - 1);
        var step = size - overlap;
        var chunks = new List<string>();
        for (var start = 0; start < normalized.Length; start += step)
        {
            var length = Math.Min(size, normalized.Length - start);
            var piece = normalized.AsSpan(start, length).Trim();
            if (!piece.IsEmpty)
            {
                chunks.Add(piece.ToString());
            }

            if (start + length >= normalized.Length)
            {
                break;
            }
        }

        return chunks;
    }
}
