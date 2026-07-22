// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.AI.Abstractions.Rag;

/// <summary>
/// 切片选项（v1 固定窗口）
/// </summary>
public sealed class ChunkingOptions
{
    /// <summary>
    /// 单切片最大字符数
    /// </summary>
    public int MaxChunkSize { get; init; } = 1000;

    /// <summary>
    /// 相邻切片重叠字符数（保留上下文连续性）
    /// </summary>
    public int Overlap { get; init; } = 100;
}
