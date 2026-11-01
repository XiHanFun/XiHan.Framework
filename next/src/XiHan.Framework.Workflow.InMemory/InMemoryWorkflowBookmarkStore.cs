// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Instances;
using XiHan.Framework.Workflow.Abstractions.Serialization;
using XiHan.Framework.Workflow.Abstractions.Storage;

namespace XiHan.Framework.Workflow.InMemory;

/// <summary>
/// 有界内存的书签存储。
/// </summary>
/// <remarks>
/// 按分区（平台或租户）隔离：键是分区加 ID，查询只看给定分区。保存与返回都是深拷贝；按实例建索引；容量按全部分区合计。没有事务：写入立即生效。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryWorkflowBookmarkStore : IWorkflowBookmarkStore
{
    /// <summary>
    /// 保护表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 分区与书签 ID → (书签, 写入序号)。
    /// </summary>
    private readonly Dictionary<PartitionKey, (WorkflowBookmark Bookmark, long Sequence)> _bookmarks = [];

    /// <summary>
    /// 分区与实例 ID → 它的书签 ID。
    /// </summary>
    private readonly Dictionary<PartitionKey, HashSet<string>> _byInstance = [];

    /// <summary>
    /// 容量。
    /// </summary>
    private readonly int _capacity;

    /// <summary>
    /// 写入序号。
    /// </summary>
    private long _sequence;

    /// <summary>
    /// 创建。
    /// </summary>
    public InMemoryWorkflowBookmarkStore(InMemoryWorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _capacity = options.MaxBookmarks;
    }

    /// <summary>
    /// 当前条数（全部分区）。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _bookmarks.Count;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Maybe<WorkflowBookmark>>> FindAsync(string? tenantId, string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_bookmarks.TryGetValue(PartitionKey.In(tenantId, id), out var entry) ? Maybe.Some(WorkflowJson.Clone(entry.Bookmark)) : Maybe.None<WorkflowBookmark>()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> FindManyAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            IReadOnlyList<WorkflowBookmark> found = ids.Distinct(StringComparer.Ordinal)
                .Select(id => _bookmarks.TryGetValue(PartitionKey.In(tenantId, id), out var entry) ? entry.Bookmark : null)
                .OfType<WorkflowBookmark>()
                .Select(WorkflowJson.Clone)
                .ToList();
            return ValueTask.FromResult(Result.Success(found));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByInstanceAsync(string? tenantId, string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<WorkflowBookmark>>(Ordered(_byInstance.TryGetValue(PartitionKey.In(tenantId, instanceId), out var ids) ? ids.Select(id => _bookmarks[PartitionKey.In(tenantId, id)]) : [])));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> DueAsync(string? tenantId, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IReadOnlyList<WorkflowBookmark> due = InPartition(tenantId)
                .Where(entry => entry.Bookmark.DueAt is { } dueAt && dueAt <= now)
                .OrderBy(static entry => entry.Bookmark.DueAt)
                .ThenBy(static entry => entry.Sequence)
                .Take(Math.Max(0, limit))
                .Select(static entry => WorkflowJson.Clone(entry.Bookmark))
                .ToList();
            return ValueTask.FromResult(Result.Success(due));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByKindAndKeyAsync(string? tenantId, string kind, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<WorkflowBookmark>>(Ordered(InPartition(tenantId).Where(entry => string.Equals(entry.Bookmark.Kind, kind, StringComparison.Ordinal) && string.Equals(entry.Bookmark.Key, key, StringComparison.Ordinal)))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> AvailableAsync(string? tenantId, string kind, string key, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            IReadOnlyList<WorkflowBookmark> available = Ordered(InPartition(tenantId).Where(entry =>
                    string.Equals(entry.Bookmark.Kind, kind, StringComparison.Ordinal)
                    && string.Equals(entry.Bookmark.Key, key, StringComparison.Ordinal)
                    && (entry.Bookmark.DueAt is not { } dueAt || dueAt <= now)), Math.Max(0, limit));
            return ValueTask.FromResult(Result.Success(available));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> BySignalAsync(string? tenantId, string signalName, string? correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signalName);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<WorkflowBookmark>>(Ordered(InPartition(tenantId).Where(entry =>
                string.Equals(entry.Bookmark.Kind, BookmarkKinds.Signal, StringComparison.Ordinal)
                && string.Equals(entry.Bookmark.Key, signalName, StringComparison.Ordinal)
                && (correlationId is null || entry.Bookmark.CorrelationId is null || string.Equals(entry.Bookmark.CorrelationId, correlationId, StringComparison.Ordinal))))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveAsync(string? tenantId, IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        foreach (var bookmark in bookmarks)
        {
            PartitionKey.EnsureOwned(tenantId, bookmark.TenantId, "bookmark", bookmark.Id);
        }

        lock (_lock)
        {
            var added = bookmarks.Select(bookmark => PartitionKey.In(tenantId, bookmark.Id)).Distinct().Count(key => !_bookmarks.ContainsKey(key));
            if (_bookmarks.Count + added > _capacity)
            {
                return ValueTask.FromResult(Result.Failure(Error.Unavailable(WorkflowErrors.StoreCapacity, WorkflowMessages.StoreCapacity, MessageArguments.Of(("store", "bookmarks"), ("limit", _capacity)))));
            }

            foreach (var bookmark in bookmarks)
            {
                var key = PartitionKey.In(tenantId, bookmark.Id);
                var sequence = _bookmarks.TryGetValue(key, out var existing) ? existing.Sequence : ++_sequence;
                _bookmarks[key] = (WorkflowJson.Clone(bookmark), sequence);
                var owner = PartitionKey.In(tenantId, bookmark.InstanceId);
                if (!_byInstance.TryGetValue(owner, out var ids))
                {
                    ids = new HashSet<string>(StringComparer.Ordinal);
                    _byInstance[owner] = ids;
                }

                ids.Add(bookmark.Id);
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> DeleteAsync(string? tenantId, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            foreach (var id in ids)
            {
                if (_bookmarks.Remove(PartitionKey.In(tenantId, id), out var entry))
                {
                    var owner = PartitionKey.In(tenantId, entry.Bookmark.InstanceId);
                    if (_byInstance.TryGetValue(owner, out var set))
                    {
                        set.Remove(id);
                        if (set.Count == 0)
                        {
                            _byInstance.Remove(owner);
                        }
                    }
                }
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> DeleteByInstanceAsync(string? tenantId, IReadOnlyList<string> instanceIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);
        lock (_lock)
        {
            foreach (var instanceId in instanceIds)
            {
                if (_byInstance.Remove(PartitionKey.In(tenantId, instanceId), out var ids))
                {
                    foreach (var id in ids)
                    {
                        _bookmarks.Remove(PartitionKey.In(tenantId, id));
                    }
                }
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <summary>
    /// 一个分区里的书签（调用方持锁）。
    /// </summary>
    private IEnumerable<(WorkflowBookmark Bookmark, long Sequence)> InPartition(string? tenantId)
    {
        var partition = PartitionKey.Of(tenantId);
        return _bookmarks.Where(entry => string.Equals(entry.Key.Tenant, partition, StringComparison.Ordinal)).Select(static entry => entry.Value);
    }

    /// <summary>
    /// 创建时间升序、同刻按写入顺序，深拷贝。
    /// </summary>
    private static List<WorkflowBookmark> Ordered(IEnumerable<(WorkflowBookmark Bookmark, long Sequence)> entries, int limit = int.MaxValue)
        => entries.OrderBy(static entry => entry.Bookmark.CreatedAt).ThenBy(static entry => entry.Sequence).Take(limit).Select(static entry => WorkflowJson.Clone(entry.Bookmark)).ToList();
}
