// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// 按租户和文档移除已入库向量（重建/删除前清理；chunkCount 为该文档原切片数）
    /// </summary>
    /// <param name="documentId">文档标识</param>
    /// <param name="tenantId">租户标识，0 表示平台全局数据</param>
    /// <param name="chunkCount">该文档原切片数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="NotSupportedException">实现未支持非平台租户的隔离删除</exception>
    Task RemoveDocumentAsync(string documentId, long tenantId, int chunkCount, CancellationToken cancellationToken = default)
    {
        if (tenantId != 0)
        {
            return Task.FromException(new NotSupportedException(
                $"知识摄取器 {GetType().FullName} 未实现租户隔离删除，不能移除租户 {tenantId} 的文档向量。"));
        }

        return RemoveDocumentAsync(documentId, chunkCount, cancellationToken);
    }

    /// <summary>
    /// 移除平台全局文档的向量；新代码应使用带租户标识的重载。
    /// </summary>
    /// <param name="documentId">文档标识</param>
    /// <param name="chunkCount">该文档原切片数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步操作</returns>
    Task RemoveDocumentAsync(string documentId, int chunkCount, CancellationToken cancellationToken = default);
}
