#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:KnowledgeIngestRequest
// Guid:a1b2c3d4-e5f6-4a14-9c14-0a0b0c0d0e14
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Abstractions.Rag;

/// <summary>
/// 知识摄取请求（一篇文档的原文 + 溯源 + 嵌入 provider 选择）
/// </summary>
public sealed class KnowledgeIngestRequest
{
    /// <summary>
    /// 文档 id（逻辑标识，用于按文档删除/重建）
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// 原文
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// 租户 id（0=平台全局）
    /// </summary>
    public long TenantId { get; init; }

    /// <summary>
    /// 文档标题（引用展示）
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 来源标识（文件名/路径）
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// 嵌入 provider（ConfigCode；null 用默认 provider）
    /// </summary>
    public string? Provider { get; init; }

    /// <summary>
    /// 切片选项（null 用默认）
    /// </summary>
    public ChunkingOptions? Chunking { get; init; }
}
