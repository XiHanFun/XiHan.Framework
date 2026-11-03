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
/// <see cref="IKnowledgeStore"/> 到 <see cref="VectorStore"/> 的映射
/// </summary>
/// <remarks>
/// 每个知识库集合一个向量集合；<c>DocumentIds</c> 过滤下推，元数据过滤取回 <c>top × 4</c> 条后在进程内完成；删除与裁剪按过滤条件分页取键再按键删
/// 健康项 <see cref="VectorDataKnowledgeFeature.HealthName"/> 的探针也在这里，连接器抛出时与存储操作报告同一个错误
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class VectorDataKnowledgeStore : IKnowledgeStore
{
    /// <summary>
    /// 按过滤条件取键删除时的分页大小
    /// </summary>
    private const int DeletePageSize = 500;

    /// <summary>
    /// 元数据过滤时多取的倍数
    /// </summary>
    private const int MetadataOverfetch = 4;

    /// <summary>
    /// 向量存储
    /// </summary>
    private readonly VectorStore _store;

    /// <summary>
    /// 集合定义
    /// </summary>
    private readonly VectorStoreCollectionDefinition _definition;

    /// <summary>
    /// 健康探针询问的集合：知识库的默认集合
    /// </summary>
    private readonly string _probeCollection;

    /// <summary>
    /// 创建
    /// </summary>
    public VectorDataKnowledgeStore(VectorStore store, AIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _definition = KnowledgeVectorRecord.Definition(options.Knowledge.Dimensions);
        _probeCollection = options.Knowledge.Collection;
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
                await DeleteMatchingAsync(target, DocumentFilter([documentId]), cancellationToken).ConfigureAwait(false);
            }

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result> TrimAsync(string collection, string documentId, int chunkCount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkCount);
        try
        {
            var target = Collection(collection);
            if (!await target.CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                return Result.Success();
            }

            await DeleteMatchingAsync(target, TrimFilter(documentId, chunkCount), cancellationToken).ConfigureAwait(false);
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
    /// 健康探针：向量存储是否应答
    /// </summary>
    /// <remarks>
    /// 只问默认集合在不在：一次轻量往返，存在与否都说明连得上；不列集合（按租户分集合时集合很多）、不建集合、不读写记录
    /// 连接器抛出 → <see cref="AIErrors.KnowledgeUnavailable"/>，与存储操作报告的错误相同；取消（含探针宿主的超时）原样抛出，由探针宿主记成超时
    /// </remarks>
    internal async ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 连接器不一定理会取消令牌：按令牌结束等待，卡住的连接也受探针宿主的时限约束，不拖住健康循环与启动
            await _store.CollectionExistsAsync(_probeCollection, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 取集合
    /// </summary>
    private VectorStoreCollection<string, KnowledgeVectorRecord> Collection(string name) => _store.GetCollection<string, KnowledgeVectorRecord>(name, _definition);

    /// <summary>
    /// 删除满足过滤条件的全部记录
    /// </summary>
    /// <remarks>
    /// 连接器不一定支持按过滤删除：先分页取键再按键删，直到取不满一页。删掉的记录不再出现在下一页里，所以不需要跳过
    /// </remarks>
    private static async ValueTask DeleteMatchingAsync(VectorStoreCollection<string, KnowledgeVectorRecord> target, Expression<Func<KnowledgeVectorRecord, bool>> filter, CancellationToken cancellationToken)
    {
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
                return;
            }

            await target.DeleteAsync(keys, cancellationToken).ConfigureAwait(false);
            if (keys.Count < DeletePageSize)
            {
                return;
            }
        }
    }

    /// <summary>
    /// <c>r.DocumentId == id &amp;&amp; r.ChunkIndex &gt;= count</c>（常量比较，连接器都能翻译）
    /// </summary>
    private static Expression<Func<KnowledgeVectorRecord, bool>> TrimFilter(string documentId, int chunkCount)
    {
        var record = Expression.Parameter(typeof(KnowledgeVectorRecord), "record");
        var document = Expression.Equal(Expression.Property(record, nameof(KnowledgeVectorRecord.DocumentId)), Expression.Constant(documentId, typeof(string)));
        var beyond = Expression.GreaterThanOrEqual(Expression.Property(record, nameof(KnowledgeVectorRecord.ChunkIndex)), Expression.Constant(chunkCount, typeof(int)));
        return Expression.Lambda<Func<KnowledgeVectorRecord, bool>>(Expression.AndAlso(document, beyond), record);
    }

    /// <summary>
    /// <c>r.DocumentId == id1 || r.DocumentId == id2 …</c>（常量比较，连接器都能翻译）
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
    /// 知识记录 → 向量记录
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
    /// 向量记录 → 命中
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
    /// 存储不可达
    /// </summary>
    private static Error Unavailable(Exception exception)
        => Error.Unavailable(AIErrors.KnowledgeUnavailable, AIMessages.KnowledgeUnavailable, MessageArguments.Of(("reason", exception.Message)));
}
