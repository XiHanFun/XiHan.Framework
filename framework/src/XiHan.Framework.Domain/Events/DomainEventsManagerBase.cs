#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DomainEventsManagerBase
// Guid:a60e271e-6353-43f9-980f-1d6019d355f8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/13 04:08:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using XiHan.Framework.Domain.Events.Abstracts;

namespace XiHan.Framework.Domain.Events;

/// <summary>
/// 领域事件管理器基类
/// </summary>
public class DomainEventsManagerBase : IDomainEventsManager
{
    private readonly ConcurrentQueue<DomainEventRecord> _localEvents = new();
    private readonly ConcurrentQueue<DomainEventRecord> _distributedEvents = new();

    /// <summary>
    /// 获取本地事件
    /// </summary>
    /// <returns>本地事件集合</returns>
    public virtual IEnumerable<DomainEventRecord> GetLocalEvents()
    {
        return [.. _localEvents];
    }

    /// <summary>
    /// 获取分布式事件
    /// </summary>
    /// <returns>分布式事件集合</returns>
    public virtual IEnumerable<DomainEventRecord> GetDistributedEvents()
    {
        return [.. _distributedEvents];
    }

    /// <summary>
    /// 清空本地事件
    /// </summary>
    public virtual void ClearLocalEvents()
    {
        while (_localEvents.TryDequeue(out _)) { }
    }

    /// <summary>
    /// 清空分布式事件
    /// </summary>
    public virtual void ClearDistributedEvents()
    {
        while (_distributedEvents.TryDequeue(out _)) { }
    }

    /// <summary>
    /// 添加本地事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <exception cref="ArgumentNullException">当事件数据为空时抛出</exception>
    public virtual void AddLocalEvent(IDomainEvent eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        _localEvents.Enqueue(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <exception cref="ArgumentNullException">当事件数据为空时抛出</exception>
    public virtual void AddDistributedEvent(IDomainEvent eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        _distributedEvents.Enqueue(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }

    /// <summary>
    /// 获取所有事件数量
    /// </summary>
    /// <returns>事件总数</returns>
    public virtual int GetTotalEventCount()
    {
        return _localEvents.Count + _distributedEvents.Count;
    }

    /// <summary>
    /// 检查是否有待处理的事件
    /// </summary>
    /// <returns>如果有待处理事件返回 true，否则返回 false</returns>
    public virtual bool HasPendingEvents()
    {
        return !_localEvents.IsEmpty || !_distributedEvents.IsEmpty;
    }

    /// <summary>
    /// 标记事件为已提交
    /// </summary>
    public virtual void MarkEventsAsCommitted()
    {
        while (_localEvents.TryDequeue(out _)) { }
        while (_distributedEvents.TryDequeue(out _)) { }
    }
}
