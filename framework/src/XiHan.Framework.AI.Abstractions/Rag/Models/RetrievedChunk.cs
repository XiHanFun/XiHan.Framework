#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RetrievedChunk
// Guid:a1b2c3d4-e5f6-4a11-9c11-0a0b0c0d0e11
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Abstractions.Rag.Models;

/// <summary>
/// 检索命中的切片（含相似度分数，供引用与排序）
/// </summary>
public sealed class RetrievedChunk
{
    /// <summary>
    /// 所属文档 id
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// 切片序号
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 切片文本
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 文档标题
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 来源标识
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// 相似度分数（向量库返回，越大越相近；连接器不同量纲可能不同）
    /// </summary>
    public double? Score { get; init; }
}
