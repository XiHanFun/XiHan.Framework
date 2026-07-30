// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.AI.Abstractions.Rag.Models;

/// <summary>
/// 文本切片（入库前的一段可嵌入文本 + 溯源元信息）
/// </summary>
public sealed class TextChunk
{
    /// <summary>
    /// 所属文档 id（逻辑文档标识，如 SysKnowledgeDocument 主键字符串）
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// 切片在文档内的序号
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// 切片文本
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 租户 id（0=平台全局；用于向量库过滤隔离）
    /// </summary>
    public long TenantId { get; init; }

    /// <summary>
    /// 文档标题（用于检索结果引用展示）
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 来源标识（文件名/路径等，用于引用溯源）
    /// </summary>
    public string? Source { get; init; }
}
