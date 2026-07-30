// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Workflow.Abstractions;
using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;

namespace XiHan.Framework.Workflow.Stores;

/// <summary>
/// 内存流程书签存储（进程内单实例场景的默认实现）
/// </summary>
public class InMemoryWorkflowBookmarkStore : IWorkflowBookmarkStore
{
    private readonly ConcurrentDictionary<string, WorkflowBookmark> _bookmarks = new();

    /// <inheritdoc />
    public Task<WorkflowBookmark?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_bookmarks.GetValueOrDefault(id));
    }

    /// <inheritdoc />
    public Task<List<WorkflowBookmark>> GetByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.InstanceId == instanceId)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<List<WorkflowBookmark>> GetByNodeInstanceAsync(string nodeInstanceId, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.NodeInstanceId == nodeInstanceId)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<List<WorkflowBookmark>> GetDueAsync(DateTime now, int maxResultCount, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.DueTime.HasValue && item.DueTime.Value <= now)
            .OrderBy(item => item.DueTime)
            .Take(maxResultCount)
            .ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<List<WorkflowBookmark>> GetByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.Kind == kind && item.Key == key)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task<List<WorkflowBookmark>> GetBySignalAsync(string signalName, string? correlationId, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.Kind == WorkflowBookmarkKinds.Signal && item.Key == signalName)
            .Where(item => correlationId is null || item.CorrelationId is null || item.CorrelationId == correlationId)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <inheritdoc />
    public Task InsertAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _bookmarks[bookmark.Id] = bookmark;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _bookmarks[bookmark.Id] = bookmark;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _bookmarks.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        foreach (var bookmark in _bookmarks.Values.Where(item => item.InstanceId == instanceId).ToList())
        {
            _bookmarks.TryRemove(bookmark.Id, out _);
        }

        return Task.CompletedTask;
    }
}
