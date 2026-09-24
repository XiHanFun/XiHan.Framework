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
    /// <remarks>
    /// 检索恒限定在 <see cref="RetrievalFilter.TenantId"/> 这一个租户内；<paramref name="filter"/> 为空即平台（0 号租户）。
    /// </remarks>
    /// <param name="query">检索问题文本</param>
    /// <param name="topK">返回切片数量上限</param>
    /// <param name="filter">检索过滤条件，为空即平台作用域</param>
    /// <param name="provider">嵌入模型 provider 名，为空取默认 provider</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int topK = 5,
        RetrievalFilter? filter = null,
        string? provider = null,
        CancellationToken cancellationToken = default);
}
