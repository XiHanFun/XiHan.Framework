#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FixedWindowChunkingStrategy
// Guid:b2c3d4e5-f6a7-4b11-9d11-1a1b1c1d1e11
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.AI.Abstractions.Rag;

namespace XiHan.Framework.AI.Rag;

/// <summary>
/// 固定窗口切片策略（按字符窗口 + 重叠切分；v1 的唯一切片器）
/// </summary>
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

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var size = Math.Max(1, options.MaxChunkSize);
        var overlap = Math.Clamp(options.Overlap, 0, size - 1);
        var step = size - overlap;

        var chunks = new List<string>();
        for (var start = 0; start < normalized.Length; start += step)
        {
            var length = Math.Min(size, normalized.Length - start);
            var piece = normalized.Substring(start, length).Trim();
            if (piece.Length > 0)
            {
                chunks.Add(piece);
            }

            if (start + length >= normalized.Length)
            {
                break;
            }
        }

        return chunks;
    }
}
