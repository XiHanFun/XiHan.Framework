// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// <see cref="IKnowledgeIngestor"/> 的实现（单例）
/// </summary>
/// <remarks>
/// 每篇文档一次嵌入批调用，受嵌入 Provider 的超时令牌约束（RFC-0022「超时与取消」）。重新摄入先写后删：新块写入成功之后才裁掉旧版本多出来的块，嵌入或写入失败时旧文档照常可检索
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KnowledgeIngestor : IKnowledgeIngestor
{
    /// <summary>
    /// AI 选项（实际的默认嵌入 Provider）
    /// </summary>
    private readonly AIOptions _ai;

    /// <summary>
    /// 选项
    /// </summary>
    private readonly KnowledgeOptions _options;

    /// <summary>
    /// Provider 目录
    /// </summary>
    private readonly IAIProviderCatalog _providers;

    /// <summary>
    /// 切块策略
    /// </summary>
    private readonly IChunkingStrategy _chunking;

    /// <summary>
    /// 时间源
    /// </summary>
    /// <remarks>
    /// 嵌入 Provider 的超时令牌按它计时
    /// </remarks>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 存储（没有 Provider 时为空）
    /// </summary>
    private readonly IKnowledgeStore? _store;

    /// <summary>
    /// 创建
    /// </summary>
    public KnowledgeIngestor(AIOptions options, IAIProviderCatalog providers, IChunkingStrategy chunking, TimeProvider timeProvider, IKnowledgeStore? store = null)
    {
        _ai = options ?? throw new ArgumentNullException(nameof(options));
        _options = options.Knowledge;
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _chunking = chunking ?? throw new ArgumentNullException(nameof(chunking));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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
    /// 摄入一篇
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
        if (chunks.Count == 0)
        {
            // 新文本切不出块：这篇文档不再有内容，整篇删除
            var removed = await _store.DeleteAsync(collection, [request.DocumentId], cancellationToken).ConfigureAwait(false);
            return removed.IsSuccess ? 0 : removed.Error!;
        }

        var provider = request.Provider ?? _options.EmbeddingProvider ?? _ai.ResolveDefaultEmbeddingProvider();
        var generated = await KnowledgeErrors.EmbedAsync(_providers, provider, chunks, _timeProvider, cancellationToken).ConfigureAwait(false);
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

        // 先写后删：同 ID 的块由写入覆盖，写入成功之后才裁掉旧版本多出来的块。之前任何一步失败都不碰旧块，旧文档照常可检索
        var written = await _store.UpsertAsync(collection, records, cancellationToken).ConfigureAwait(false);
        if (!written.IsSuccess)
        {
            return written.Error!;
        }

        var trimmed = await _store.TrimAsync(collection, request.DocumentId, records.Count, cancellationToken).ConfigureAwait(false);
        return trimmed.IsSuccess ? records.Count : trimmed.Error!;
    }
}
