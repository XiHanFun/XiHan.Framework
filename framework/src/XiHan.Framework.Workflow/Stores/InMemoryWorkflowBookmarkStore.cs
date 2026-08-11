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

    /// <summary>
    /// 按标识查找书签
    /// </summary>
    /// <param name="id">书签标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签（不存在返回 null）</returns>
    public Task<WorkflowBookmark?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_bookmarks.GetValueOrDefault(id));
    }

    /// <summary>
    /// 获取实例的全部书签
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    public Task<List<WorkflowBookmark>> GetByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.InstanceId == instanceId)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <summary>
    /// 获取节点实例的全部书签
    /// </summary>
    /// <param name="nodeInstanceId">节点实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    public Task<List<WorkflowBookmark>> GetByNodeInstanceAsync(string nodeInstanceId, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.NodeInstanceId == nodeInstanceId)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <summary>
    /// 获取到期的定时类书签
    /// </summary>
    /// <param name="now">当前时间</param>
    /// <param name="maxResultCount">最大返回条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>到期书签列表（按到期时间升序）</returns>
    public Task<List<WorkflowBookmark>> GetDueAsync(DateTime now, int maxResultCount, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.DueTime.HasValue && item.DueTime.Value <= now)
            .OrderBy(item => item.DueTime)
            .Take(maxResultCount)
            .ToList();
        return Task.FromResult(list);
    }

    /// <summary>
    /// 按种类和索引键查询书签
    /// </summary>
    /// <param name="kind">书签种类</param>
    /// <param name="key">索引键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    public Task<List<WorkflowBookmark>> GetByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.Kind == kind && item.Key == key)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <summary>
    /// 查询匹配信号的书签（相关性为空表示广播，不按相关性过滤）
    /// </summary>
    /// <param name="signalName">信号名称</param>
    /// <param name="correlationId">业务相关性标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表（按创建时间升序）</returns>
    public Task<List<WorkflowBookmark>> GetBySignalAsync(string signalName, string? correlationId, CancellationToken cancellationToken = default)
    {
        var list = _bookmarks.Values
            .Where(item => item.Kind == WorkflowBookmarkKinds.Signal && item.Key == signalName)
            .Where(item => correlationId is null || item.CorrelationId is null || item.CorrelationId == correlationId)
            .OrderBy(item => item.CreationTime)
            .ToList();
        return Task.FromResult(list);
    }

    /// <summary>
    /// 插入书签
    /// </summary>
    /// <param name="bookmark">书签</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task InsertAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _bookmarks[bookmark.Id] = bookmark;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新书签
    /// </summary>
    /// <param name="bookmark">书签</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task UpdateAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        _bookmarks[bookmark.Id] = bookmark;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除书签
    /// </summary>
    /// <param name="id">书签标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _bookmarks.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 删除实例的全部书签
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task DeleteByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        foreach (var bookmark in _bookmarks.Values.Where(item => item.InstanceId == instanceId).ToList())
        {
            _bookmarks.TryRemove(bookmark.Id, out _);
        }

        return Task.CompletedTask;
    }
}
