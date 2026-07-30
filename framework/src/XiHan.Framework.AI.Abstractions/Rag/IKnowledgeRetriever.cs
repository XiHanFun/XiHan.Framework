// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.AI.Abstractions.Rag.Models;

namespace XiHan.Framework.AI.Abstractions.Rag;

/// <summary>
/// 知识检索器（query→embedding→向量检索→返回片段）
/// </summary>
public interface IKnowledgeRetriever
{
    /// <summary>
    /// 检索与 query 最相近的 topK 个切片
    /// </summary>
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int topK = 5,
        RetrievalFilter? filter = null,
        string? provider = null,
        CancellationToken cancellationToken = default);
}
