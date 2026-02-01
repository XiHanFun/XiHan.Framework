#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuditedAggregateRoot
// Guid:11fa308d-9d2e-4554-b8d2-db9159911353
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 16:46:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Domain.Events;
using XiHan.Framework.Domain.Events.Abstracts;

namespace XiHan.Framework.Domain.Aggregates;

/// <summary>
/// 审计聚合根基类
/// </summary>
public abstract class AuditedAggregateRoot : FullAuditedEntityBase, IAggregateRoot
{
    private readonly ICollection<DomainEventRecord> _distributedEvents = [];
    private readonly ICollection<DomainEventRecord> _localEvents = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    protected AuditedAggregateRoot() : base()
    {
    }

    /// <summary>
    /// 获取本地事件
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<DomainEventRecord> GetLocalEvents()
    {
        return _localEvents;
    }

    /// <summary>
    /// 获取分布式事件
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<DomainEventRecord> GetDistributedEvents()
    {
        return _distributedEvents;
    }

    /// <summary>
    /// 清空本地事件
    /// </summary>
    public virtual void ClearLocalEvents()
    {
        _localEvents.Clear();
    }

    /// <summary>
    /// 清空分布式事件
    /// </summary>
    public virtual void ClearDistributedEvents()
    {
        _distributedEvents.Clear();
    }

    /// <summary>
    /// 添加本地事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    protected virtual void AddLocalEvent(IDomainEvent eventData)
    {
        _localEvents.Add(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    protected virtual void AddDistributedEvent(IDomainEvent eventData)
    {
        _distributedEvents.Add(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }
}

/// <summary>
/// 审计聚合根基类（带用户）
/// </summary>
/// <typeparam name="TKey">用户主键类型</typeparam>
public abstract class AuditedAggregateRoot<TKey> : FullAuditedEntityBase<TKey>, IAggregateRoot<TKey>
    where TKey : IEquatable<TKey>
{
    private readonly ICollection<DomainEventRecord> _distributedEvents = [];
    private readonly ICollection<DomainEventRecord> _localEvents = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    protected AuditedAggregateRoot() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId"></param>
    protected AuditedAggregateRoot(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 获取本地事件
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<DomainEventRecord> GetLocalEvents()
    {
        return _localEvents;
    }

    /// <summary>
    /// 获取分布式事件
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<DomainEventRecord> GetDistributedEvents()
    {
        return _distributedEvents;
    }

    /// <summary>
    /// 清空本地事件
    /// </summary>
    public virtual void ClearLocalEvents()
    {
        _localEvents.Clear();
    }

    /// <summary>
    /// 清空分布式事件
    /// </summary>
    public virtual void ClearDistributedEvents()
    {
        _distributedEvents.Clear();
    }

    /// <summary>
    /// 添加本地事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    protected virtual void AddLocalEvent(IDomainEvent eventData)
    {
        _localEvents.Add(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    protected virtual void AddDistributedEvent(IDomainEvent eventData)
    {
        _distributedEvents.Add(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }
}
