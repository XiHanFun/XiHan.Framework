#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AggregateRootExtensions
// Guid:def12f9d-8e3a-4b5c-9a2e-1234567890de
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/12 19:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Events;
using XiHan.Framework.Domain.Events.Abstracts;

namespace XiHan.Framework.Domain.Aggregates;

/// <summary>
/// 聚合根扩展方法
/// 提供聚合根的实用功能
/// </summary>
public static class AggregateRootExtensions
{
    /// <summary>
    /// 获取所有事件（本地事件 + 分布式事件）
    /// </summary>
    /// <param name="aggregateRoot">聚合根</param>
    /// <returns>所有事件记录</returns>
    /// <exception cref="ArgumentNullException">当聚合根为空时抛出</exception>
    public static IEnumerable<DomainEventRecord> GetAllEvents(this IAggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return aggregateRoot.GetLocalEvents()
            .Concat(aggregateRoot.GetDistributedEvents())
            .OrderBy(e => e.EventOrder);
    }

    /// <summary>
    /// 清空所有事件
    /// </summary>
    /// <param name="aggregateRoot">聚合根</param>
    /// <exception cref="ArgumentNullException">当聚合根为空时抛出</exception>
    public static void ClearAllEvents(this IAggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        aggregateRoot.ClearLocalEvents();
        aggregateRoot.ClearDistributedEvents();
    }

    /// <summary>
    /// 获取指定类型的事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="aggregateRoot">聚合根</param>
    /// <returns>指定类型的事件</returns>
    /// <exception cref="ArgumentNullException">当聚合根为空时抛出</exception>
    public static IEnumerable<TEvent> GetEventsOfType<TEvent>(this IAggregateRoot aggregateRoot)
        where TEvent : class, IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return aggregateRoot.GetAllEvents()
            .Select(record => record.EventData)
            .OfType<TEvent>();
    }

    /// <summary>
    /// 检查是否包含指定类型的事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="aggregateRoot">聚合根</param>
    /// <returns>如果包含指定类型的事件返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当聚合根为空时抛出</exception>
    public static bool HasEventOfType<TEvent>(this IAggregateRoot aggregateRoot)
        where TEvent : class, IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return aggregateRoot.GetEventsOfType<TEvent>().Any();
    }

    /// <summary>
    /// 获取事件数量统计
    /// </summary>
    /// <param name="aggregateRoot">聚合根</param>
    /// <returns>事件统计信息</returns>
    /// <exception cref="ArgumentNullException">当聚合根为空时抛出</exception>
    public static EventStatistics GetEventStatistics(this IAggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        var localEvents = aggregateRoot.GetLocalEvents().ToList();
        var distributedEvents = aggregateRoot.GetDistributedEvents().ToList();

        return new EventStatistics
        {
            LocalEventCount = localEvents.Count,
            DistributedEventCount = distributedEvents.Count,
            TotalEventCount = localEvents.Count + distributedEvents.Count,
            EventTypes = [.. localEvents.Concat(distributedEvents)
                .Select(e => e.EventData.GetType().Name)
                .Distinct()]
        };
    }

    /// <summary>
    /// 异步处理所有事件
    /// </summary>
    /// <param name="aggregateRoot">聚合根</param>
    /// <param name="eventHandler">事件处理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理任务</returns>
    /// <exception cref="ArgumentNullException">当聚合根或事件处理器为空时抛出</exception>
    public static async Task ProcessAllEventsAsync(
        this IAggregateRoot aggregateRoot,
        Func<IDomainEvent, Task> eventHandler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);
        ArgumentNullException.ThrowIfNull(eventHandler);

        var events = aggregateRoot.GetAllEvents();

        foreach (var eventRecord in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await eventHandler(eventRecord.EventData);
        }
    }

    /// <summary>
    /// 创建聚合根的快照信息
    /// </summary>
    /// <param name="aggregateRoot">聚合根</param>
    /// <returns>聚合根快照</returns>
    /// <exception cref="ArgumentNullException">当聚合根为空时抛出</exception>
    public static AggregateSnapshot CreateSnapshot(this IAggregateRoot aggregateRoot)
    {
        ArgumentNullException.ThrowIfNull(aggregateRoot);

        return new AggregateSnapshot
        {
            AggregateType = aggregateRoot.GetType().Name,
            SnapshotTime = DateTimeOffset.UtcNow,
            EventStatistics = aggregateRoot.GetEventStatistics(),
            RecentEvents = [.. aggregateRoot.GetAllEvents()
                .OrderByDescending(e => e.EventOrder)
                .Take(10)
                .Select(e => new EventSnapshot
                {
                    EventType = e.EventData.GetType().Name,
                    EventId = e.EventData.EventId,
                    OccurredOn = e.EventData.OccurredOn,
                    EventOrder = e.EventOrder
                })]
        };
    }
}

/// <summary>
/// 事件统计信息
/// </summary>
public class EventStatistics
{
    /// <summary>
    /// 本地事件数量
    /// </summary>
    public int LocalEventCount { get; set; }

    /// <summary>
    /// 分布式事件数量
    /// </summary>
    public int DistributedEventCount { get; set; }

    /// <summary>
    /// 总事件数量
    /// </summary>
    public int TotalEventCount { get; set; }

    /// <summary>
    /// 事件类型列表
    /// </summary>
    public List<string> EventTypes { get; set; } = [];

    /// <summary>
    /// 重写 ToString 方法
    /// </summary>
    /// <returns>统计信息的字符串表示</returns>
    public override string ToString()
    {
        return $"Local: {LocalEventCount}, Distributed: {DistributedEventCount}, Total: {TotalEventCount}, Types: {EventTypes.Count}";
    }
}

/// <summary>
/// 聚合根快照
/// </summary>
public class AggregateSnapshot
{
    /// <summary>
    /// 聚合类型
    /// </summary>
    public string AggregateType { get; set; } = string.Empty;

    /// <summary>
    /// 快照时间
    /// </summary>
    public DateTimeOffset SnapshotTime { get; set; }

    /// <summary>
    /// 事件统计
    /// </summary>
    public EventStatistics EventStatistics { get; set; } = new();

    /// <summary>
    /// 最近的事件
    /// </summary>
    public List<EventSnapshot> RecentEvents { get; set; } = [];
}

/// <summary>
/// 事件快照
/// </summary>
public class EventSnapshot
{
    /// <summary>
    /// 事件类型
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// 事件唯一标识
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// 发生时间
    /// </summary>
    public DateTimeOffset OccurredOn { get; set; }

    /// <summary>
    /// 事件顺序
    /// </summary>
    public long EventOrder { get; set; }
}
