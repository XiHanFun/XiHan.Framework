// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.VectorData;
using XiHan.Framework.AI.Abstractions;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.VectorData;

/// <summary>
/// <see cref="IKnowledgeStore"/> 到 <see cref="VectorStore"/> 的映射：每个知识库集合一个向量集合；<c>DocumentIds</c> 过滤下推，元数据过滤取回 <c>top × 4</c> 条后在进程内完成。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class VectorDataKnowledgeStore : IKnowledgeStore
{
    /// <summary>
    /// 按文档取键时的分页大小。
    /// </summary>
    private const int DeletePageSize = 500;

    /// <summary>
    /// 元数据过滤时多取的倍数。
    /// </summary>
    private const int MetadataOverfetch = 4;

    /// <summary>
    /// 向量存储。
    /// </summary>
    private readonly VectorStore _store;

    /// <summary>
    /// 集合定义。
    /// </summary>
    private readonly VectorStoreCollectionDefinition _definition;

    /// <summary>
    /// 创建。
    /// </summary>
    public VectorDataKnowledgeStore(VectorStore store, AIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _definition = KnowledgeVectorRecord.Definition(options.Knowledge.Dimensions);
    }

    /// <inheritdoc />
    public async ValueTask<Result> UpsertAsync(string collection, IReadOnlyList<KnowledgeRecord> records, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
        {
            return Result.Success();
        }

        try
        {
            var target = Collection(collection);
            await target.EnsureCollectionExistsAsync(cancellationToken).ConfigureAwait(false);
            await target.UpsertAsync(records.Select(ToVector).ToList(), cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result> DeleteAsync(string collection, IReadOnlyList<string> documentIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(documentIds);
        if (documentIds.Count == 0)
        {
            return Result.Success();
        }

        try
        {
            var target = Collection(collection);
            if (!await target.CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                return Result.Success();
            }

            foreach (var documentId in documentIds.Distinct(StringComparer.Ordinal))
            {
                // 连接器不一定支持按过滤删除：先分页取键再按键删，直到取不满一页
                var filter = DocumentFilter([documentId]);
                var retrieval = new FilteredRecordRetrievalOptions<KnowledgeVectorRecord> { IncludeVectors = false };
                while (true)
                {
                    var keys = new List<string>();
                    await foreach (var record in target.GetAsync(filter, DeletePageSize, retrieval, cancellationToken).ConfigureAwait(false))
                    {
                        keys.Add(record.Id);
                    }

                    if (keys.Count == 0)
                    {
                        break;
                    }

                    await target.DeleteAsync(keys, cancellationToken).ConfigureAwait(false);
                    if (keys.Count < DeletePageSize)
                    {
                        break;
                    }
                }
            }

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<RetrievedChunk>>> SearchAsync(string collection, ReadOnlyMemory<float> vector, int top, RetrievalFilter? filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentOutOfRangeException.ThrowIfLessThan(top, 1);
        try
        {
            var target = Collection(collection);
            if (!await target.CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                return Result.Success<IReadOnlyList<RetrievedChunk>>([]);
            }

            var byMetadata = filter?.Metadata is { Count: > 0 };
            var options = new VectorSearchOptions<KnowledgeVectorRecord>
            {
                IncludeVectors = false,
                Filter = filter?.DocumentIds is { Count: > 0 } ids ? DocumentFilter(ids) : null,
            };
            var hits = new List<RetrievedChunk>();
            await foreach (var result in target.SearchAsync(vector, byMetadata ? top * MetadataOverfetch : top, options, cancellationToken).ConfigureAwait(false))
            {
                var chunk = ToChunk(result.Record, result.Score ?? 0d);
                if (byMetadata && !filter!.Matches(chunk.DocumentId, chunk.Metadata))
                {
                    continue;
                }

                hits.Add(chunk);
            }

            return Result.Success<IReadOnlyList<RetrievedChunk>>(hits.OrderByDescending(static hit => hit.Score).Take(top).ToList());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 取集合。
    /// </summary>
    private VectorStoreCollection<string, KnowledgeVectorRecord> Collection(string name) => _store.GetCollection<string, KnowledgeVectorRecord>(name, _definition);

    /// <summary>
    /// <c>r.DocumentId == id1 || r.DocumentId == id2 …</c>（常量比较，连接器都能翻译）。
    /// </summary>
    private static Expression<Func<KnowledgeVectorRecord, bool>> DocumentFilter(IReadOnlyList<string> documentIds)
    {
        var record = Expression.Parameter(typeof(KnowledgeVectorRecord), "record");
        var property = Expression.Property(record, nameof(KnowledgeVectorRecord.DocumentId));
        Expression? body = null;
        foreach (var documentId in documentIds)
        {
            var equals = Expression.Equal(property, Expression.Constant(documentId, typeof(string)));
            body = body is null ? equals : Expression.OrElse(body, equals);
        }

        return Expression.Lambda<Func<KnowledgeVectorRecord, bool>>(body ?? Expression.Constant(false), record);
    }

    /// <summary>
    /// 知识记录 → 向量记录。
    /// </summary>
    private static KnowledgeVectorRecord ToVector(KnowledgeRecord record) => new()
    {
        Id = record.Id,
        DocumentId = record.DocumentId,
        ChunkIndex = record.ChunkIndex,
        Text = record.Text,
        Metadata = record.Metadata.Count == 0 ? null : JsonSerializer.Serialize(new Dictionary<string, string>(record.Metadata, StringComparer.Ordinal), KnowledgeVectorJsonContext.Default.DictionaryStringString),
        Vector = record.Vector,
    };

    /// <summary>
    /// 向量记录 → 命中。
    /// </summary>
    private static RetrievedChunk ToChunk(KnowledgeVectorRecord record, double score) => new()
    {
        Id = record.Id,
        DocumentId = record.DocumentId,
        ChunkIndex = record.ChunkIndex,
        Text = record.Text,
        Metadata = string.IsNullOrEmpty(record.Metadata)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize(record.Metadata, KnowledgeVectorJsonContext.Default.DictionaryStringString) ?? new Dictionary<string, string>(StringComparer.Ordinal),
        Score = score,
    };

    /// <summary>
    /// 存储不可达。
    /// </summary>
    private static Error Unavailable(Exception exception)
        => Error.Unavailable(AIErrors.KnowledgeUnavailable, AIMessages.KnowledgeUnavailable, MessageArguments.Of(("reason", exception.Message)));
}
