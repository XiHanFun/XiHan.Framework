// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// <see cref="IKnowledgeIngestor"/> 的实现（单例）。
/// </summary>
/// <remarks>
/// 每篇文档一次嵌入批调用；重新摄入先删旧块。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KnowledgeIngestor : IKnowledgeIngestor
{
    /// <summary>
    /// 选项。
    /// </summary>
    private readonly KnowledgeOptions _options;

    /// <summary>
    /// Provider 目录。
    /// </summary>
    private readonly IAIProviderCatalog _providers;

    /// <summary>
    /// 切块策略。
    /// </summary>
    private readonly IChunkingStrategy _chunking;

    /// <summary>
    /// 存储（没有 Provider 时为空）。
    /// </summary>
    private readonly IKnowledgeStore? _store;

    /// <summary>
    /// 创建。
    /// </summary>
    public KnowledgeIngestor(AIOptions options, IAIProviderCatalog providers, IChunkingStrategy chunking, IKnowledgeStore? store = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Knowledge;
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _chunking = chunking ?? throw new ArgumentNullException(nameof(chunking));
        _store = store;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<Result<int>>> IngestAsync(IReadOnlyList<KnowledgeIngestRequest> requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var results = new List<Result<int>>(requests.Count);
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await IngestOneAsync(request, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    /// <inheritdoc />
    public ValueTask<Result> RemoveAsync(string? collection, IReadOnlyList<string> documentIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentIds);
        return _store is null
            ? ValueTask.FromResult(Result.Failure(KnowledgeErrors.NoStore()))
            : _store.DeleteAsync(collection ?? _options.Collection, documentIds, cancellationToken);
    }

    /// <summary>
    /// 摄入一篇。
    /// </summary>
    private async ValueTask<Result<int>> IngestOneAsync(KnowledgeIngestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentId))
        {
            return Error.Validation(AIErrors.RequestInvalid, AIMessages.RequestInvalid, MessageArguments.Of(("reason", "the document id is empty")));
        }

        if (_store is null)
        {
            return KnowledgeErrors.NoStore();
        }

        var collection = request.Collection ?? _options.Collection;
        var chunks = _chunking.Chunk(request.Text, request.Chunking ?? _options.Chunking);
        var deleted = await _store.DeleteAsync(collection, [request.DocumentId], cancellationToken).ConfigureAwait(false);
        if (!deleted.IsSuccess)
        {
            return deleted.Error!;
        }

        if (chunks.Count == 0)
        {
            return 0;
        }

        var generator = _providers.ResolveEmbeddingGenerator(request.Provider ?? _options.EmbeddingProvider);
        if (!generator.TryGetValue(out var embeddings))
        {
            return generator.Error!;
        }

        var provider = request.Provider ?? _options.EmbeddingProvider ?? string.Empty;
        var generated = await KnowledgeErrors.EmbedAsync(embeddings, chunks, provider, cancellationToken).ConfigureAwait(false);
        if (!generated.TryGetValue(out var vectors))
        {
            return generated.Error!;
        }

        var records = new List<KnowledgeRecord>(chunks.Count);
        for (var index = 0; index < chunks.Count; index++)
        {
            var vector = vectors[index].Vector;
            if (vector.Length != _options.Dimensions)
            {
                return KnowledgeErrors.Dimensions(vector.Length, _options.Dimensions);
            }

            records.Add(new KnowledgeRecord
            {
                Id = KnowledgeRecord.IdOf(request.DocumentId, index),
                DocumentId = request.DocumentId,
                ChunkIndex = index,
                Text = chunks[index],
                Metadata = request.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
                Vector = vector,
            });
        }

        var written = await _store.UpsertAsync(collection, records, cancellationToken).ConfigureAwait(false);
        return written.IsSuccess ? records.Count : written.Error!;
    }
}
