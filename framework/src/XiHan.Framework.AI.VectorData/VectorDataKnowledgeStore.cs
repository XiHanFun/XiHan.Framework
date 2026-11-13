// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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
/// 连接器抛出时，存储操作与健康项 <see cref="VectorDataKnowledgeFeature.HealthName"/> 的探针报告同一个错误：<c>reason</c> 只写异常类型，完整异常进日志
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial class VectorDataKnowledgeStore : IKnowledgeStore
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
    /// 日志
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// 上一次探测失败的异常类型
    /// </summary>
    /// <remarks>
    /// 探测成功后为空。探测由健康循环逐次执行，这个字段不需要同步：万一并发，最多多记一条日志
    /// </remarks>
    private Type? _lastProbeFailure;

    /// <summary>
    /// 创建
    /// </summary>
    public VectorDataKnowledgeStore(VectorStore store, AIOptions options, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _logger = loggerFactory.CreateLogger<VectorDataKnowledgeStore>();
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
            return OperationFailed(nameof(UpsertAsync), collection, exception);
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
            return OperationFailed(nameof(DeleteAsync), collection, exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result> TrimAsync(string collection, IReadOnlyList<KnowledgeTrim> trims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(trims);
        if (trims.Count == 0)
        {
            return Result.Success();
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var target = Collection(collection);
            if (!await target.CollectionExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                return Result.Success();
            }

            // 向量库没有按多个条件一次删除的统一接口：同一个集合句柄上逐篇删除
            foreach (var trim in trims)
            {
                await DeleteMatchingAsync(target, TrimFilter(trim.DocumentId, trim.ChunkCount), cancellationToken).ConfigureAwait(false);
            }

            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return OperationFailed(nameof(TrimAsync), collection, exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<RetrievedChunk>>> SearchAsync(string collection, KnowledgeSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(search);
        // 用到了本实现不认识的条件即失败：不读不认识的条件，也不给出放宽了的结果（ADR-0228）
        if (search.Validate(KnowledgeSearch.Revision).Error is { } unsupported)
        {
            return unsupported;
        }

        var vector = search.Vector;
        var top = search.Top;
        var filter = search.Filter;
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

                // 连接器交回的顺序不可靠（下面还要重排），逐条判断而不是遇到第一条不合格就停
                if (search.MinScore is { } minimum && chunk.Score < minimum)
                {
                    continue;
                }

                hits.Add(chunk);
            }

            return Result.Success<IReadOnlyList<RetrievedChunk>>(hits.OrderByDescending(static hit => hit.Score).Take(top).ToList());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return OperationFailed(nameof(SearchAsync), collection, exception);
        }
    }

    /// <summary>
    /// 健康探针：向量存储是否应答
    /// </summary>
    /// <remarks>
    /// 只问默认集合在不在：一次轻量往返，存在与否都说明连得上；不列集合（按租户分集合时集合很多）、不建集合、不读写记录
    /// 连接器抛出 → <see cref="AIErrors.KnowledgeUnavailable"/>，<c>reason</c> 是异常类型的全名；取消（含探针宿主的超时）原样抛出，由探针宿主记成超时
    /// </remarks>
    internal async ValueTask<Result> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 连接器不一定理会取消令牌：按令牌结束等待，卡住的连接也受探针宿主的时限约束，不拖住健康循环与启动
            await _store.CollectionExistsAsync(_probeCollection, cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            _lastProbeFailure = null;
            return Result.Success();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 同一种失败只记一次 Warning，持续时记 Debug：向量库停着时，每个探测周期一条带堆栈的 Warning 会淹没日志
            var failure = exception.GetType();
            if (failure == _lastProbeFailure)
            {
                LogProbeStillFailing(_logger, _probeCollection, exception);
            }
            else
            {
                LogProbeFailed(_logger, _probeCollection, exception);
            }

            _lastProbeFailure = failure;

            return Unavailable(exception);
        }
    }

    /// <summary>
    /// 取集合
    /// </summary>
    private VectorStoreCollection<Guid, KnowledgeVectorRecord> Collection(string name) => _store.GetCollection<Guid, KnowledgeVectorRecord>(name, _definition);

    /// <summary>
    /// 删除满足过滤条件的全部记录
    /// </summary>
    /// <remarks>
    /// 连接器不一定支持按过滤删除：先分页取键再按键删，直到取不满一页。删掉的记录不再出现在下一页里，所以不需要跳过
    /// </remarks>
    private static async ValueTask DeleteMatchingAsync(VectorStoreCollection<Guid, KnowledgeVectorRecord> target, Expression<Func<KnowledgeVectorRecord, bool>> filter, CancellationToken cancellationToken)
    {
        var retrieval = new FilteredRecordRetrievalOptions<KnowledgeVectorRecord> { IncludeVectors = false };
        while (true)
        {
            var keys = new List<Guid>();
            await foreach (var record in target.GetAsync(filter, DeletePageSize, retrieval, cancellationToken).ConfigureAwait(false))
            {
                keys.Add(record.Key);
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
    /// <remarks>
    /// 拼成平衡的或树：连接器翻译表达式是递归的，逐个左接的链有多少个文档就有多深（ADR-0190）
    /// </remarks>
    private static Expression<Func<KnowledgeVectorRecord, bool>> DocumentFilter(IReadOnlyList<string> documentIds)
    {
        var record = Expression.Parameter(typeof(KnowledgeVectorRecord), "record");
        var property = Expression.Property(record, nameof(KnowledgeVectorRecord.DocumentId));
        Expression Range(int start, int count)
            => count == 1
                ? Expression.Equal(property, Expression.Constant(documentIds[start], typeof(string)))
                : Expression.OrElse(Range(start, count / 2), Range(start + (count / 2), count - (count / 2)));

        return Expression.Lambda<Func<KnowledgeVectorRecord, bool>>(documentIds.Count == 0 ? Expression.Constant(false) : Range(0, documentIds.Count), record);
    }

    /// <summary>
    /// 知识记录 → 向量记录
    /// </summary>
    private static KnowledgeVectorRecord ToVector(KnowledgeRecord record) => new()
    {
        Key = KnowledgeVectorRecord.KeyOf(record.Id),
        RecordId = record.Id,
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
        Id = record.RecordId,
        DocumentId = record.DocumentId,
        ChunkIndex = record.ChunkIndex,
        Text = record.Text,
        Metadata = string.IsNullOrEmpty(record.Metadata)
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize(record.Metadata, KnowledgeVectorJsonContext.Default.DictionaryStringString) ?? new Dictionary<string, string>(StringComparer.Ordinal),
        Score = score,
    };

    /// <summary>
    /// 存储操作失败：记下完整异常，返回存储不可达
    /// </summary>
    private Error OperationFailed(string operation, string collection, Exception exception)
    {
        LogOperationFailed(_logger, operation, collection, exception);
        return Unavailable(exception);
    }

    /// <summary>
    /// 存储不可达
    /// </summary>
    /// <remarks>
    /// 这个错误会返回给调用方、写进健康快照，而这个 Provider 接任意连接器，异常消息可能带地址或连接串：<c>reason</c> 只写类型，调用方先把完整异常记进日志
    /// </remarks>
    private static Error Unavailable(Exception exception)
    {
        var type = exception.GetType();
        return Error.Unavailable(AIErrors.KnowledgeUnavailable, AIMessages.KnowledgeUnavailable, MessageArguments.Of(("reason", type.FullName ?? type.Name)));
    }

    /// <summary>
    /// 存储操作失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Knowledge store operation '{Operation}' on collection '{Collection}' failed.")]
    private static partial void LogOperationFailed(ILogger logger, string operation, string collection, Exception exception);

    /// <summary>
    /// 探测失败（这一种失败第一次）
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The vector store did not answer the health probe of collection '{Collection}'.")]
    private static partial void LogProbeFailed(ILogger logger, string collection, Exception exception);

    /// <summary>
    /// 探测持续失败
    /// </summary>
    [LoggerMessage(Level = LogLevel.Debug, Message = "The vector store still does not answer the health probe of collection '{Collection}'.")]
    private static partial void LogProbeStillFailing(ILogger logger, string collection, Exception exception);
}
