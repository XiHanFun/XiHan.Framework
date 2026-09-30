// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Workflow.Abstractions.Runtime;
using XiHan.Framework.Workflow.Abstractions.Stores;

namespace XiHan.Framework.Workflow.SqlSugar.Tests;

/// <summary>
/// 让多个调用方对同一书签的删除在同一时刻放行的汇合点
/// </summary>
internal sealed class DeleteRendezvous
{
    private readonly int _parties;
    private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="parties">需要汇合的调用方数量</param>
    public DeleteRendezvous(int parties)
    {
        _parties = parties;
    }

    /// <summary>
    /// 需要汇合的书签标识，为空时不拦截任何删除
    /// </summary>
    public string? BookmarkId { get; set; }

    /// <summary>
    /// 到达汇合点，目标书签的删除要等全部调用方到达后才放行
    /// </summary>
    /// <param name="id">正在删除的书签标识</param>
    /// <returns>任务</returns>
    public async Task ArriveAsync(string id)
    {
        if (!string.Equals(id, BookmarkId, StringComparison.Ordinal))
        {
            return;
        }

        if (Interlocked.Increment(ref _arrived) >= _parties)
        {
            _allArrived.TrySetResult();
        }

        await _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}

/// <summary>
/// 在删除前经过汇合点的书签存储装饰器
/// </summary>
internal sealed class RendezvousBookmarkStore : IWorkflowBookmarkStore
{
    private readonly IWorkflowBookmarkStore _inner;
    private readonly DeleteRendezvous _rendezvous;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="inner">被装饰的书签存储</param>
    /// <param name="rendezvous">删除汇合点</param>
    public RendezvousBookmarkStore(IWorkflowBookmarkStore inner, DeleteRendezvous rendezvous)
    {
        _inner = inner;
        _rendezvous = rendezvous;
    }

    /// <summary>
    /// 按标识查找书签
    /// </summary>
    /// <param name="id">书签标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签</returns>
    public Task<WorkflowBookmark?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return _inner.FindAsync(id, cancellationToken);
    }

    /// <summary>
    /// 获取实例的全部书签
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表</returns>
    public Task<List<WorkflowBookmark>> GetByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        return _inner.GetByInstanceAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// 获取节点实例的全部书签
    /// </summary>
    /// <param name="nodeInstanceId">节点实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表</returns>
    public Task<List<WorkflowBookmark>> GetByNodeInstanceAsync(string nodeInstanceId, CancellationToken cancellationToken = default)
    {
        return _inner.GetByNodeInstanceAsync(nodeInstanceId, cancellationToken);
    }

    /// <summary>
    /// 获取到期的定时类书签
    /// </summary>
    /// <param name="now">当前时间</param>
    /// <param name="maxResultCount">最大返回条数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>到期书签列表</returns>
    public Task<List<WorkflowBookmark>> GetDueAsync(DateTime now, int maxResultCount, CancellationToken cancellationToken = default)
    {
        return _inner.GetDueAsync(now, maxResultCount, cancellationToken);
    }

    /// <summary>
    /// 按种类和索引键查询书签
    /// </summary>
    /// <param name="kind">书签种类</param>
    /// <param name="key">索引键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表</returns>
    public Task<List<WorkflowBookmark>> GetByKindAndKeyAsync(string kind, string key, CancellationToken cancellationToken = default)
    {
        return _inner.GetByKindAndKeyAsync(kind, key, cancellationToken);
    }

    /// <summary>
    /// 查询匹配信号的书签
    /// </summary>
    /// <param name="signalName">信号名称</param>
    /// <param name="correlationId">业务相关性标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>书签列表</returns>
    public Task<List<WorkflowBookmark>> GetBySignalAsync(string signalName, string? correlationId, CancellationToken cancellationToken = default)
    {
        return _inner.GetBySignalAsync(signalName, correlationId, cancellationToken);
    }

    /// <summary>
    /// 插入书签
    /// </summary>
    /// <param name="bookmark">书签</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task InsertAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        return _inner.InsertAsync(bookmark, cancellationToken);
    }

    /// <summary>
    /// 更新书签
    /// </summary>
    /// <param name="bookmark">书签</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task UpdateAsync(WorkflowBookmark bookmark, CancellationToken cancellationToken = default)
    {
        return _inner.UpdateAsync(bookmark, cancellationToken);
    }

    /// <summary>
    /// 经过汇合点后删除书签
    /// </summary>
    /// <param name="id">书签标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _rendezvous.ArriveAsync(id);
        await _inner.DeleteAsync(id, cancellationToken);
    }

    /// <summary>
    /// 删除实例的全部书签
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    public Task DeleteByInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        return _inner.DeleteByInstanceAsync(instanceId, cancellationToken);
    }
}
