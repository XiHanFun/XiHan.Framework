// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.AI.Abstractions.Rag;

/// <summary>
/// 文本切片策略
/// </summary>
public interface IChunkingStrategy
{
    /// <summary>
    /// 将长文本切成若干片段
    /// </summary>
    IReadOnlyList<string> Chunk(string text, ChunkingOptions options);
}
