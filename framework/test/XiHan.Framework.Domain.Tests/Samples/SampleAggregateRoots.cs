// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Aggregates;
using XiHan.Framework.Domain.Events.Abstracts;

namespace XiHan.Framework.Domain.Tests.Samples;

/// <summary>
/// 无主键聚合根基类的最小具体子类
/// </summary>
/// <remarks>
/// 事件相关成员在基类中是 protected，这里统一开放为 public 以便测试直接驱动。
/// </remarks>
public sealed class SampleKeylessAggregateRoot : AggregateRootBase
{
    /// <summary>
    /// 添加本地事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void RaiseLocal(IDomainEvent eventData)
    {
        AddLocalEvent(eventData);
    }

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void RaiseDistributed(IDomainEvent eventData)
    {
        AddDistributedEvent(eventData);
    }

    /// <summary>
    /// 获取事件总数
    /// </summary>
    /// <returns>事件总数</returns>
    public int TotalEventCount()
    {
        return GetTotalEventCount();
    }

    /// <summary>
    /// 是否存在待处理事件
    /// </summary>
    /// <returns>存在返回 true</returns>
    public bool HasPending()
    {
        return HasPendingEvents();
    }

    /// <summary>
    /// 标记事件为已提交
    /// </summary>
    public void CommitEvents()
    {
        MarkEventsAsCommitted();
    }
}

/// <summary>
/// long 主键聚合根基类的最小具体子类
/// </summary>
public class SampleAggregateRoot : AggregateRootBase<long>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SampleAggregateRoot()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SampleAggregateRoot(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 添加本地事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void RaiseLocal(IDomainEvent eventData)
    {
        AddLocalEvent(eventData);
    }

    /// <summary>
    /// 添加分布式事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void RaiseDistributed(IDomainEvent eventData)
    {
        AddDistributedEvent(eventData);
    }

    /// <summary>
    /// 获取事件总数
    /// </summary>
    /// <returns>事件总数</returns>
    public int TotalEventCount()
    {
        return GetTotalEventCount();
    }

    /// <summary>
    /// 是否存在待处理事件
    /// </summary>
    /// <returns>存在返回 true</returns>
    public bool HasPending()
    {
        return HasPendingEvents();
    }

    /// <summary>
    /// 标记事件为已提交
    /// </summary>
    public void CommitEvents()
    {
        MarkEventsAsCommitted();
    }
}

/// <summary>
/// 多租户聚合根基类的最小具体子类
/// </summary>
public sealed class SampleMultiTenantAggregateRoot : MultiTenantAggregateRootBase<long>
{
    /// <summary>
    /// 添加本地事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    public void RaiseLocal(IDomainEvent eventData)
    {
        AddLocalEvent(eventData);
    }

    /// <summary>
    /// 暴露受保护的主键写入口
    /// </summary>
    /// <param name="basicId">主键</param>
    public void AssignBasicId(long basicId)
    {
        BasicId = basicId;
    }
}
