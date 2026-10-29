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
/// 保存与返回都是深拷贝；按实例建索引。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class InMemoryWorkflowBookmarkStore : IWorkflowBookmarkStore
{
    /// <summary>
    /// 保护表的锁。
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// 书签 ID → (书签, 写入序号)。
    /// </summary>
    private readonly Dictionary<string, (WorkflowBookmark Bookmark, long Sequence)> _bookmarks = new(StringComparer.Ordinal);

    /// <summary>
    /// 实例 ID → 它的书签 ID。
    /// </summary>
    private readonly Dictionary<string, HashSet<string>> _byInstance = new(StringComparer.Ordinal);

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
    /// 当前条数。
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
    public ValueTask<Result<Maybe<WorkflowBookmark>>> FindAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success(_bookmarks.TryGetValue(id, out var entry) ? Maybe.Some(WorkflowJson.Clone(entry.Bookmark)) : Maybe.None<WorkflowBookmark>()));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> FindManyAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            IReadOnlyList<WorkflowBookmark> found = ids.Distinct(StringComparer.Ordinal).Where(_bookmarks.ContainsKey).Select(id => WorkflowJson.Clone(_bookmarks[id].Bookmark)).ToList();
            return ValueTask.FromResult(Result.Success(found));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByInstanceAsync(string instanceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<WorkflowBookmark>>(Ordered(_byInstance.TryGetValue(instanceId, out var ids) ? ids.Select(id => _bookmarks[id]) : [])));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> DueAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            IReadOnlyList<WorkflowBookmark> due = _bookmarks.Values
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
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> ByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<WorkflowBookmark>>(Ordered(_bookmarks.Values.Where(entry => string.Equals(entry.Bookmark.Kind, kind, StringComparison.Ordinal) && string.Equals(entry.Bookmark.Key, key, StringComparison.Ordinal)))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<WorkflowBookmark>>> BySignalAsync(string signalName, string? correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signalName);
        lock (_lock)
        {
            return ValueTask.FromResult(Result.Success<IReadOnlyList<WorkflowBookmark>>(Ordered(_bookmarks.Values.Where(entry =>
                string.Equals(entry.Bookmark.Kind, BookmarkKinds.Signal, StringComparison.Ordinal)
                && string.Equals(entry.Bookmark.Key, signalName, StringComparison.Ordinal)
                && (correlationId is null || entry.Bookmark.CorrelationId is null || string.Equals(entry.Bookmark.CorrelationId, correlationId, StringComparison.Ordinal))))));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> SaveAsync(IReadOnlyList<WorkflowBookmark> bookmarks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        lock (_lock)
        {
            var added = bookmarks.Select(static bookmark => bookmark.Id).Distinct(StringComparer.Ordinal).Count(id => !_bookmarks.ContainsKey(id));
            if (_bookmarks.Count + added > _capacity)
            {
                return ValueTask.FromResult(Result.Failure(Error.Unavailable(WorkflowErrors.StoreCapacity, WorkflowMessages.StoreCapacity, MessageArguments.Of(("store", "bookmarks"), ("limit", _capacity)))));
            }

            foreach (var bookmark in bookmarks)
            {
                var sequence = _bookmarks.TryGetValue(bookmark.Id, out var existing) ? existing.Sequence : ++_sequence;
                _bookmarks[bookmark.Id] = (WorkflowJson.Clone(bookmark), sequence);
                if (!_byInstance.TryGetValue(bookmark.InstanceId, out var ids))
                {
                    ids = new HashSet<string>(StringComparer.Ordinal);
                    _byInstance[bookmark.InstanceId] = ids;
                }

                ids.Add(bookmark.Id);
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        lock (_lock)
        {
            foreach (var id in ids)
            {
                if (_bookmarks.Remove(id, out var entry) && _byInstance.TryGetValue(entry.Bookmark.InstanceId, out var set))
                {
                    set.Remove(id);
                    if (set.Count == 0)
                    {
                        _byInstance.Remove(entry.Bookmark.InstanceId);
                    }
                }
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <inheritdoc />
    public ValueTask<Result> DeleteByInstanceAsync(IReadOnlyList<string> instanceIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);
        lock (_lock)
        {
            foreach (var instanceId in instanceIds)
            {
                if (_byInstance.Remove(instanceId, out var ids))
                {
                    foreach (var id in ids)
                    {
                        _bookmarks.Remove(id);
                    }
                }
            }

            return ValueTask.FromResult(Result.Success());
        }
    }

    /// <summary>
    /// 创建时间升序、同刻按写入顺序，深拷贝。
    /// </summary>
    private static List<WorkflowBookmark> Ordered(IEnumerable<(WorkflowBookmark Bookmark, long Sequence)> entries)
        => entries.OrderBy(static entry => entry.Bookmark.CreatedAt).ThenBy(static entry => entry.Sequence).Select(static entry => WorkflowJson.Clone(entry.Bookmark)).ToList();
}
