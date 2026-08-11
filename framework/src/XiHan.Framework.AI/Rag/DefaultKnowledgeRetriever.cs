// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
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
    private readonly KnowledgeVectorOptions _vectorOptions;
    private readonly VectorStoreCollectionDefinition _definition;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultKnowledgeRetriever(
        IAiEmbeddingGeneratorResolver embeddingResolver,
        VectorStore vectorStore,
        IOptions<KnowledgeVectorOptions> vectorOptions)
    {
        ArgumentNullException.ThrowIfNull(vectorOptions);

        _embeddingResolver = embeddingResolver;
        _vectorStore = vectorStore;
        _vectorOptions = vectorOptions.Value;
        _definition = VectorStoreKnowledgeRecord.CreateDefinition(_vectorOptions.Dimensions);
    }

    /// <summary>
    /// 检索与 query 最相近的 topK 个切片
    /// </summary>
    /// <param name="query">检索问题文本</param>
    /// <param name="topK">返回切片数量上限，非正数时按 5 处理</param>
    /// <param name="filter">检索过滤条件，按租户与文档限定范围</param>
    /// <param name="provider">嵌入模型 provider 名，为空取默认 provider</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命中的知识片段列表，集合不存在时返回空列表</returns>
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

        var collection = _vectorStore.GetCollection<Guid, VectorStoreKnowledgeRecord>(_vectorOptions.CollectionName, _definition);
        // 集合不存在返回空结果是合法语义（尚未摄取任何文档）；连不上向量库则是故障，由翻译层区分。
        if (!await VectorStoreOperation.ExecuteAsync(() => collection.CollectionExistsAsync(cancellationToken)))
        {
            return [];
        }

        var generator = _embeddingResolver.Resolve(provider);
        var queryVector = await EmbeddingOperation.ExecuteAsync(
            () => generator.GenerateVectorAsync(query, cancellationToken: cancellationToken),
            provider,
            generator.GetService<EmbeddingGeneratorMetadata>()?.DefaultModelId);

        VectorStoreKnowledgeRecord.EnsureDimensions(queryVector.Length, _vectorOptions.Dimensions);

        var options = BuildOptions(filter);
        var results = new List<RetrievedChunk>();
        var matches = VectorStoreOperation.ExecuteStreamAsync(
            collection.SearchAsync(queryVector, topK, options, cancellationToken),
            cancellationToken);
        await foreach (var result in matches)
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
