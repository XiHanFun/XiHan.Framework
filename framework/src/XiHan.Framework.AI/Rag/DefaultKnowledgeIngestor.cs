// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
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
    private readonly KnowledgeVectorOptions _vectorOptions;
    private readonly VectorStoreCollectionDefinition _definition;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultKnowledgeIngestor(
        IChunkingStrategy chunkingStrategy,
        IAiEmbeddingGeneratorResolver embeddingResolver,
        VectorStore vectorStore,
        IOptions<KnowledgeVectorOptions> vectorOptions)
    {
        ArgumentNullException.ThrowIfNull(vectorOptions);

        _chunkingStrategy = chunkingStrategy;
        _embeddingResolver = embeddingResolver;
        _vectorStore = vectorStore;
        _vectorOptions = vectorOptions.Value;
        _definition = VectorStoreKnowledgeRecord.CreateDefinition(_vectorOptions.Dimensions);
    }

    /// <summary>
    /// 摄取一篇文档：切片、批量嵌入后写入向量库，返回切片数
    /// </summary>
    /// <param name="request">摄取请求，含文档标识、正文、租户与切片参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>写入的切片数，文本切不出片段时为 0</returns>
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

        // 批量嵌入（顺序与输入一致）。嵌入排在向量库调用之前，失败必须能与向量库故障区分开。
        var generator = _embeddingResolver.Resolve(request.Provider);
        var embeddings = await EmbeddingOperation.ExecuteAsync(
            () => generator.GenerateAsync(pieces, cancellationToken: cancellationToken),
            request.Provider,
            generator.GetService<EmbeddingGeneratorMetadata>()?.DefaultModelId);

        // 维度不符时向量库只会报驱动层错误，这里先拦下并指明是模型与集合配置不一致。
        VectorStoreKnowledgeRecord.EnsureDimensions(embeddings[0].Vector.Length, _vectorOptions.Dimensions);

        var collection = GetCollection();
        await VectorStoreOperation.ExecuteAsync(() => collection.EnsureCollectionExistsAsync(cancellationToken));

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

        await VectorStoreOperation.ExecuteAsync(() => collection.UpsertAsync(records, cancellationToken));
        return records.Count;
    }

    /// <summary>
    /// 按文档移除已入库向量，集合不存在时直接返回
    /// </summary>
    /// <param name="documentId">文档标识</param>
    /// <param name="chunkCount">该文档原切片数，非正数时不做任何处理</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RemoveDocumentAsync(string documentId, int chunkCount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        cancellationToken.ThrowIfCancellationRequested();

        if (chunkCount <= 0)
        {
            return;
        }

        var collection = GetCollection();
        if (!await VectorStoreOperation.ExecuteAsync(() => collection.CollectionExistsAsync(cancellationToken)))
        {
            return;
        }

        var keys = Enumerable.Range(0, chunkCount).Select(i => VectorStoreKnowledgeRecord.MakeId(documentId, i));
        await VectorStoreOperation.ExecuteAsync(() => collection.DeleteAsync(keys, cancellationToken));
    }

    /// <summary>
    /// 取知识切片集合（字段模型走运行期定义，维度随配置）
    /// </summary>
    private VectorStoreCollection<Guid, VectorStoreKnowledgeRecord> GetCollection()
    {
        return _vectorStore.GetCollection<Guid, VectorStoreKnowledgeRecord>(_vectorOptions.CollectionName, _definition);
    }
}
