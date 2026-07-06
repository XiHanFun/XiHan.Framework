#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IChunkingStrategy
// Guid:a1b2c3d4-e5f6-4a13-9c13-0a0b0c0d0e13
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
