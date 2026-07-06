#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IKnowledgeRetriever
// Guid:a1b2c3d4-e5f6-4a17-9c17-0a0b0c0d0e17
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
