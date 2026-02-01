#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AggregateRootBase
// Guid:375aea3a-e307-440c-b710-52af97f44ecf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/02/20 05:17:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Events;
using XiHan.Framework.Domain.Events.Abstracts;

namespace XiHan.Framework.Domain.Aggregates;

/// <summary>
/// 聚合根基类
/// </summary>
public abstract class AggregateRootBase : FullAuditedEntityBase, IAggregateRoot
{
    private readonly DomainEventsManagerBase _eventManager = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    protected AggregateRootBase() : base()
    {
    }

    /// <summary>
    /// 获取本地事件
    /// </summary>
    /// <returns>本地事件集合</returns>
    public virtual IEnumerable<DomainEventRecord> GetLocalEvents() => _eventManager.GetLocalEvents();

    /// <summary>
    /// 获取分布式事件
    /// </summary>
    /// <returns>分布式事件集合</returns>
    public virtual IEnumerable<DomainEventRecord> GetDistributedEvents() => _eventManager.GetDistributedEvents();

    /// <summary>
    /// 清空本地事件
    /// </summary>
    public virtual void ClearLocalEvents() => _eventManager.ClearLocalEvents();

    /// <summary>
    /// 清空分布式事件
    /// </summary>
    public virtual void ClearDistributedEvents() => _eventManager.ClearDistributedEvents();

    /// <summary>
    /// 添加本地事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    protected virtual void AddLocalEvent(IDomainEvent eventData) => _eventManager.AddLocalEvent(eventData);

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    protected virtual void AddDistributedEvent(IDomainEvent eventData) => _eventManager.AddDistributedEvent(eventData);

    /// <summary>
    /// 获取所有事件数量
    /// </summary>
    /// <returns>事件总数</returns>
    protected virtual int GetTotalEventCount() => _eventManager.GetTotalEventCount();

    /// <summary>
    /// 检查是否有待处理的事件
    /// </summary>
    /// <returns>如果有待处理事件返回 true，否则返回 false</returns>
    protected virtual bool HasPendingEvents() => _eventManager.HasPendingEvents();

    /// <summary>
    /// 标记事件为已提交
    /// </summary>
    protected virtual void MarkEventsAsCommitted() => _eventManager.MarkEventsAsCommitted();
}

/// <summary>
/// 泛型主键聚合根基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class AggregateRootBase<TKey> : FullAuditedEntityBase<TKey>, IAggregateRoot<TKey>
    where TKey : IEquatable<TKey>
{
    private readonly DomainEventsManagerBase _eventManager = new();

    /// <summary>
    /// 构造函数
    /// </summary>
    protected AggregateRootBase() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">聚合根主键</param>
    protected AggregateRootBase(TKey basicId)
        : base(basicId)
    {
    }

    /// <summary>
    /// 获取本地事件
    /// </summary>
    /// <returns>本地事件集合</returns>
    public virtual IEnumerable<DomainEventRecord> GetLocalEvents() => _eventManager.GetLocalEvents();

    /// <summary>
    /// 获取分布式事件
    /// </summary>
    /// <returns>分布式事件集合</returns>
    public virtual IEnumerable<DomainEventRecord> GetDistributedEvents() => _eventManager.GetDistributedEvents();

    /// <summary>
    /// 清空本地事件
    /// </summary>
    public virtual void ClearLocalEvents() => _eventManager.ClearLocalEvents();

    /// <summary>
    /// 清空分布式事件
    /// </summary>
    public virtual void ClearDistributedEvents() => _eventManager.ClearDistributedEvents();

    /// <summary>
    /// 添加本地事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    protected virtual void AddLocalEvent(IDomainEvent eventData) => _eventManager.AddLocalEvent(eventData);

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    protected virtual void AddDistributedEvent(IDomainEvent eventData) => _eventManager.AddDistributedEvent(eventData);

    /// <summary>
    /// 获取所有事件数量
    /// </summary>
    /// <returns>事件总数</returns>
    protected virtual int GetTotalEventCount() => _eventManager.GetTotalEventCount();

    /// <summary>
    /// 检查是否有待处理的事件
    /// </summary>
    /// <returns>如果有待处理事件返回 true，否则返回 false</returns>
    protected virtual bool HasPendingEvents() => _eventManager.HasPendingEvents();

    /// <summary>
    /// 标记事件为已提交
    /// </summary>
    protected virtual void MarkEventsAsCommitted() => _eventManager.MarkEventsAsCommitted();
}
