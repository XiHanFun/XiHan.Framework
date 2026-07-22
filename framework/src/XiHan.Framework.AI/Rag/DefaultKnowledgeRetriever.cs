// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Rag;
using XiHan.Framework.AI.Abstractions.Rag.Models;

namespace XiHan.Framework.AI.Rag;

/// <summary>
/// 默认知识检索器（query 嵌入 → 向量检索 → 映射片段）
/// </summary>
public sealed class DefaultKnowledgeRetriever : IKnowledgeRetriever
{
    private readonly IAiEmbeddingGeneratorResolver _embeddingResolver;
    private readonly VectorStore _vectorStore;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultKnowledgeRetriever(IAiEmbeddingGeneratorResolver embeddingResolver, VectorStore vectorStore)
    {
        _embeddingResolver = embeddingResolver;
        _vectorStore = vectorStore;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int topK = 5,
        RetrievalFilter? filter = null,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (topK <= 0)
        {
            topK = 5;
        }

        var collection = _vectorStore.GetCollection<Guid, VectorStoreKnowledgeRecord>(VectorStoreKnowledgeRecord.CollectionName);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return [];
        }

        var generator = _embeddingResolver.Resolve(provider);
        var queryVector = await generator.GenerateVectorAsync(query, cancellationToken: cancellationToken);

        var options = BuildOptions(filter);
        var results = new List<RetrievedChunk>();
        await foreach (var result in collection.SearchAsync(queryVector, topK, options, cancellationToken))
        {
            var record = result.Record;
            results.Add(new RetrievedChunk
            {
                DocumentId = record.DocumentId,
                Index = record.ChunkIndex,
                Text = record.Text,
                Title = record.Title,
                Source = record.Source,
                Score = result.Score
            });
        }

        return results;
    }

    /// <summary>
    /// 由过滤条件构建向量检索选项（作用于已索引字段 TenantId/DocumentId）
    /// </summary>
    private static VectorSearchOptions<VectorStoreKnowledgeRecord>? BuildOptions(RetrievalFilter? filter)
    {
        if (filter is null || (filter.TenantId is null && string.IsNullOrEmpty(filter.DocumentId)))
        {
            return null;
        }

        Expression<Func<VectorStoreKnowledgeRecord, bool>> predicate;
        if (filter.TenantId is { } tenantId && !string.IsNullOrEmpty(filter.DocumentId))
        {
            var documentId = filter.DocumentId;
            predicate = record => record.TenantId == tenantId && record.DocumentId == documentId;
        }
        else if (filter.TenantId is { } tenantOnly)
        {
            predicate = record => record.TenantId == tenantOnly;
        }
        else
        {
            var documentId = filter.DocumentId!;
            predicate = record => record.DocumentId == documentId;
        }

        return new VectorSearchOptions<VectorStoreKnowledgeRecord> { Filter = predicate };
    }
}
