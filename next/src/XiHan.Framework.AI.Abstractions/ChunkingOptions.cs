// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 切块选项：块大小与相邻块的重叠（字符）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ChunkingOptions
{
    /// <summary>
    /// 块大小；默认 1000。
    /// </summary>
    public int MaxChunkSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The chunk size must be at least 1.");
    } = 1000;

    /// <summary>
    /// 重叠；默认 100，实际裁到 <c>[0, MaxChunkSize − 1]</c>。
    /// </summary>
    public int Overlap
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The overlap must not be negative.");
    } = 100;
}
