// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// <see cref="IKnowledgeIngestor"/> 的实现（单例）
/// </summary>
/// <remarks>
/// 每篇文档一次嵌入批调用，受嵌入 Provider 的超时令牌约束（RFC-0022「超时与取消」）。重新摄入先写后删：新块写入成功之后才裁掉旧版本多出来的块，嵌入或写入失败时旧文档照常可检索
/// </remarks>
internal sealed class KnowledgeIngestor : IKnowledgeIngestor
{
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
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 当前调用：请求没有指明集合时据它的租户选集合
    /// </summary>
    private readonly IInvocationAccessor? _invocation;

    /// <summary>
    /// 创建
    /// </summary>
    public KnowledgeIngestor(AIOptions options, IAIProviderCatalog providers, IChunkingStrategy chunking, TimeProvider timeProvider, ILoggerFactory loggerFactory, IKnowledgeStore? store = null, IInvocationAccessor? invocation = null)
    {
        _invocation = invocation;
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<KnowledgeIngestor>();
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
        if (_store is null)
        {
            return ValueTask.FromResult(Result.Failure(KnowledgeErrors.NoStore()));
        }

        // 与摄入同一规则：没有指明集合时删的是这次调用的租户集合，删不到别的租户的文档（ADR-0210）
        var resolved = KnowledgeErrors.CollectionFor(collection, _options, _invocation);
        return resolved.TryGetValue(out var name)
            ? _store.DeleteAsync(name, documentIds, cancellationToken)
            : ValueTask.FromResult(Result.Failure(resolved.Error!));
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

        var resolved = KnowledgeErrors.CollectionFor(request.Collection, _options, _invocation);
        if (!resolved.TryGetValue(out var collection))
        {
            return resolved.Error!;
        }

        var chunks = _chunking.Chunk(request.Text, request.Chunking ?? _options.Chunking);
        if (chunks.Count == 0)
        {
            // 新文本切不出块：这篇文档不再有内容，整篇删除
            var removed = await _store.DeleteAsync(collection, [request.DocumentId], cancellationToken).ConfigureAwait(false);
            return removed.IsSuccess ? 0 : removed.Error!;
        }

        // 请求 → 知识库配置 → 目录的默认嵌入名；默认名问目录而不是读 AI 配置，应用替换的目录有自己的默认名
        var provider = request.Provider ?? _options.EmbeddingProvider ?? _providers.DefaultEmbeddingProvider;
        if (provider is null)
        {
            return AIProviderCatalog.DefaultEmbeddingMissing();
        }

        var generated = await KnowledgeErrors.EmbedAsync(_providers, provider, chunks, _timeProvider, _logger, cancellationToken).ConfigureAwait(false);
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

        var trimmed = await _store.TrimAsync(collection, [new KnowledgeTrim(request.DocumentId, records.Count)], cancellationToken).ConfigureAwait(false);
        return trimmed.IsSuccess ? records.Count : trimmed.Error!;
    }
}
