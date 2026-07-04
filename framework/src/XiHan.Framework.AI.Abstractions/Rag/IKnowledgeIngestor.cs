#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IKnowledgeIngestor
// Guid:a1b2c3d4-e5f6-4a15-9c15-0a0b0c0d0e15
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.AI.Abstractions.Rag;

/// <summary>
/// 知识摄取器（解析→切片→embedding→写向量库）
/// </summary>
public interface IKnowledgeIngestor
{
    /// <summary>
    /// 摄取一篇文档，返回切片数
    /// </summary>
    Task<int> IngestAsync(KnowledgeIngestRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按文档移除已入库向量（重建/删除前清理；chunkCount 为该文档原切片数）
    /// </summary>
    Task RemoveDocumentAsync(string documentId, int chunkCount, CancellationToken cancellationToken = default);
}
