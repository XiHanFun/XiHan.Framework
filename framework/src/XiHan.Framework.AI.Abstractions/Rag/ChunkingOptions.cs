#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ChunkingOptions
// Guid:a1b2c3d4-e5f6-4a12-9c12-0a0b0c0d0e12
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
