#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultKnowledgeIngestor
// Guid:b2c3d4e5-f6a7-4b13-9d13-1a1b1c1d1e13
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/05 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.AI.Abstractions.Rag;

namespace XiHan.Framework.AI.Rag;

/// <summary>
/// 默认知识摄取器（切片 → 批量 embedding → upsert 向量库）
/// </summary>
public sealed class DefaultKnowledgeIngestor : IKnowledgeIngestor
{
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly IAiEmbeddingGeneratorResolver _embeddingResolver;
    private readonly VectorStore _vectorStore;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultKnowledgeIngestor(
        IChunkingStrategy chunkingStrategy,
        IAiEmbeddingGeneratorResolver embeddingResolver,
        VectorStore vectorStore)
    {
        _chunkingStrategy = chunkingStrategy;
        _embeddingResolver = embeddingResolver;
        _vectorStore = vectorStore;
    }

    /// <inheritdoc />
    public async Task<int> IngestAsync(KnowledgeIngestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DocumentId);
        cancellationToken.ThrowIfCancellationRequested();

        var pieces = _chunkingStrategy.Chunk(request.Text, request.Chunking ?? new ChunkingOptions());
        if (pieces.Count == 0)
        {
            return 0;
        }

        // 批量嵌入（顺序与输入一致）
        var generator = _embeddingResolver.Resolve(request.Provider);
        var embeddings = await generator.GenerateAsync(pieces, cancellationToken: cancellationToken);

        var collection = _vectorStore.GetCollection<Guid, VectorStoreKnowledgeRecord>(VectorStoreKnowledgeRecord.CollectionName);
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        var records = new List<VectorStoreKnowledgeRecord>(pieces.Count);
        for (var i = 0; i < pieces.Count; i++)
        {
            records.Add(new VectorStoreKnowledgeRecord
            {
                Id = VectorStoreKnowledgeRecord.MakeId(request.DocumentId, i),
                DocumentId = request.DocumentId,
                TenantId = request.TenantId,
                ChunkIndex = i,
                Text = pieces[i],
                Title = request.Title,
                Source = request.Source,
                Embedding = embeddings[i].Vector
            });
        }

        await collection.UpsertAsync(records, cancellationToken);
        return records.Count;
    }

    /// <inheritdoc />
    public async Task RemoveDocumentAsync(string documentId, int chunkCount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        cancellationToken.ThrowIfCancellationRequested();

        if (chunkCount <= 0)
        {
            return;
        }

        var collection = _vectorStore.GetCollection<Guid, VectorStoreKnowledgeRecord>(VectorStoreKnowledgeRecord.CollectionName);
        if (!await collection.CollectionExistsAsync(cancellationToken))
        {
            return;
        }

        var keys = Enumerable.Range(0, chunkCount).Select(i => VectorStoreKnowledgeRecord.MakeId(documentId, i));
        await collection.DeleteAsync(keys, cancellationToken);
    }
}
