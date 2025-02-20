#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AggregateRootBase
// Guid:375aea3a-e307-440c-b710-52af97f44ecf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/20 5:17:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Ddd.Domain.Entities;
using XiHan.Framework.Ddd.Domain.Events;
using XiHan.Framework.Uow;

namespace XiHan.Framework.Ddd.Domain.Aggregates;

/// <summary>
/// 聚合根基类
/// </summary>
public abstract class AggregateRootBase : EntityBase, IAggregateRoot, IDomainEvents
{
    private readonly ICollection<DomainEventRecord> _distributedEvents = [];
    private readonly ICollection<DomainEventRecord> _localEvents = [];

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
    /// <param name="eventData"></param>
    protected virtual void AddLocalEvent(object eventData)
    {
        _localEvents.Add(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventData"></param>
    protected virtual void AddDistributedEvent(object eventData)
    {
        _distributedEvents.Add(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }
}

/// <summary>
/// 泛型主键聚合根基类
/// </summary>
/// <typeparam name="TKey"></typeparam>
public abstract class AggregateRootBase<TKey> : EntityBase<TKey>, IAggregateRoot<TKey>, IDomainEvents
    where TKey : IEquatable<TKey>
{
    private readonly ICollection<DomainEventRecord> _distributedEvents = [];
    private readonly ICollection<DomainEventRecord> _localEvents = [];

    /// <summary>
    /// 构造函数
    /// </summary>
    protected AggregateRootBase()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId"></param>
    protected AggregateRootBase(TKey basicId)
        : base(basicId)
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
    /// <param name="eventData"></param>
    protected virtual void AddLocalEvent(object eventData)
    {
        _localEvents.Add(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventData"></param>
    protected virtual void AddDistributedEvent(object eventData)
    {
        _distributedEvents.Add(new DomainEventRecord(eventData, EventOrderGenerator.GetNext()));
    }
}
