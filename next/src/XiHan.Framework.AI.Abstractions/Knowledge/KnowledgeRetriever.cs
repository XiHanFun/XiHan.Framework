// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// <see cref="IKnowledgeRetriever"/> 的实现（单例）。
/// </summary>
/// <remarks>
/// 查询文本的嵌入受嵌入 Provider 的超时令牌约束（RFC-0022「超时与取消」）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class KnowledgeRetriever : IKnowledgeRetriever
{
    /// <summary>
    /// AI 选项（实际的默认嵌入 Provider）。
    /// </summary>
    private readonly AIOptions _ai;

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly KnowledgeOptions _options;

    /// <summary>
    /// Provider 目录。
    /// </summary>
    private readonly IAIProviderCatalog _providers;

    /// <summary>
    /// 时间源。
    /// </summary>
    /// <remarks>
    /// 嵌入 Provider 的超时令牌按它计时。
    /// </remarks>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 存储（没有 Provider 时为空）。
    /// </summary>
    private readonly IKnowledgeStore? _store;

    /// <summary>
    /// 创建。
    /// </summary>
    public KnowledgeRetriever(AIOptions options, IAIProviderCatalog providers, TimeProvider timeProvider, IKnowledgeStore? store = null)
    {
        _ai = options ?? throw new ArgumentNullException(nameof(options));
        _options = options.Knowledge;
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _store = store;
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<RetrievedChunk>>> RetrieveAsync(KnowledgeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return Error.Validation(AIErrors.RequestInvalid, AIMessages.RequestInvalid, MessageArguments.Of(("reason", "the query text is empty")));
        }

        if (_store is null)
        {
            return KnowledgeErrors.NoStore();
        }

        var provider = query.Provider ?? _options.EmbeddingProvider ?? _ai.ResolveDefaultEmbeddingProvider();
        var generated = await KnowledgeErrors.EmbedAsync(_providers, provider, [query.Text], _timeProvider, cancellationToken).ConfigureAwait(false);
        if (!generated.TryGetValue(out var vectors) || vectors.Count == 0)
        {
            return generated.Error ?? Error.Unavailable(AIErrors.ModelUnavailable, AIMessages.ModelUnavailable, MessageArguments.Of(("provider", provider ?? string.Empty), ("reason", "the provider returned no embedding")));
        }

        var vector = vectors[0].Vector;
        if (vector.Length != _options.Dimensions)
        {
            return KnowledgeErrors.Dimensions(vector.Length, _options.Dimensions);
        }

        var found = await _store.SearchAsync(query.Collection ?? _options.Collection, vector, query.Top ?? _options.Top, query.Filter, cancellationToken).ConfigureAwait(false);
        if (!found.TryGetValue(out var chunks))
        {
            return found.Error!;
        }

        return query.MinScore is { } minimum ? Result.Success<IReadOnlyList<RetrievedChunk>>(chunks.Where(chunk => chunk.Score >= minimum).ToList()) : found;
    }
}
