// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Aggregates;
using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Events.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Aggregates;

/// <summary>
/// 聚合根扩展方法测试
/// </summary>
/// <remarks>
/// 合并两条队列时必须按全局事件序号排序：事件序号是同一执行流内的因果顺序，
/// 先本地后分布式地拼接会把因果关系弄反。
/// </remarks>
public class AggregateRootExtensionsTests
{
    /// <summary>
    /// 合并所有事件时按事件序号排序，而不是先本地后分布式
    /// </summary>
    [Fact]
    public void GetAllEvents_MergesQueuesOrderedByEventOrder()
    {
        var aggregate = new SampleAggregateRoot(1);
        var first = new SampleCreatedEvent("first");
        var second = new SampleUpdatedEvent("second");
        var third = new SampleUpdatedEvent("third");

        aggregate.RaiseLocal(first);
        aggregate.RaiseDistributed(second);
        aggregate.RaiseLocal(third);

        var all = aggregate.GetAllEvents().Select(record => record.EventData).ToList();

        Assert.Equal(3, all.Count);
        Assert.Same(first, all[0]);
        Assert.Same(second, all[1]);
        Assert.Same(third, all[2]);
    }

    /// <summary>
    /// 空聚合根合并事件返回空集合
    /// </summary>
    [Fact]
    public void GetAllEvents_WhenNoEvents_ReturnsEmpty()
    {
        var aggregate = new SampleAggregateRoot(1);

        Assert.Empty(aggregate.GetAllEvents());
    }

    /// <summary>
    /// 聚合根为空时合并事件抛出参数异常
    /// </summary>
    [Fact]
    public void GetAllEvents_WhenAggregateIsNull_Throws()
    {
        IAggregateRoot? aggregate = null;

        Assert.Throws<ArgumentNullException>(() => { _ = aggregate!.GetAllEvents(); });
    }

    /// <summary>
    /// 清空所有事件会同时清空两条队列
    /// </summary>
    [Fact]
    public void ClearAllEvents_ClearsBothQueues()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));
        aggregate.RaiseDistributed(new SampleUpdatedEvent("b"));

        aggregate.ClearAllEvents();

        Assert.Empty(aggregate.GetLocalEvents());
        Assert.Empty(aggregate.GetDistributedEvents());
    }

    /// <summary>
    /// 聚合根为空时清空事件抛出参数异常
    /// </summary>
    [Fact]
    public void ClearAllEvents_WhenAggregateIsNull_Throws()
    {
        IAggregateRoot? aggregate = null;

        Assert.Throws<ArgumentNullException>(() => aggregate!.ClearAllEvents());
    }

    /// <summary>
    /// 按类型筛选事件跨越本地与分布式两条队列
    /// </summary>
    [Fact]
    public void GetEventsOfType_FiltersAcrossBothQueues()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));
        aggregate.RaiseDistributed(new SampleUpdatedEvent("b"));
        aggregate.RaiseLocal(new SampleUpdatedEvent("c"));

        var updated = aggregate.GetEventsOfType<SampleUpdatedEvent>().ToList();

        Assert.Equal(2, updated.Count);
        Assert.Equal(new[] { "b", "c" }, updated.Select(item => item.Name).ToArray());
    }

    /// <summary>
    /// 不存在指定类型事件时筛选结果为空
    /// </summary>
    [Fact]
    public void GetEventsOfType_WhenTypeAbsent_ReturnsEmpty()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));

        Assert.Empty(aggregate.GetEventsOfType<SampleUpdatedEvent>());
    }

    /// <summary>
    /// 类型存在性判断与筛选结果一致
    /// </summary>
    [Fact]
    public void HasEventOfType_ReflectsPresence()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));

        Assert.True(aggregate.HasEventOfType<SampleCreatedEvent>());
        Assert.False(aggregate.HasEventOfType<SampleUpdatedEvent>());
    }

    /// <summary>
    /// 事件统计分别记录两条队列数量并对事件类型去重
    /// </summary>
    [Fact]
    public void GetEventStatistics_CountsQueuesAndDistinctTypes()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));
        aggregate.RaiseLocal(new SampleUpdatedEvent("b"));
        aggregate.RaiseDistributed(new SampleUpdatedEvent("c"));

        var statistics = aggregate.GetEventStatistics();

        Assert.Equal(2, statistics.LocalEventCount);
        Assert.Equal(1, statistics.DistributedEventCount);
        Assert.Equal(3, statistics.TotalEventCount);
        Assert.Equal(2, statistics.EventTypes.Count);
        Assert.Contains(nameof(SampleCreatedEvent), statistics.EventTypes);
        Assert.Contains(nameof(SampleUpdatedEvent), statistics.EventTypes);
    }

    /// <summary>
    /// 事件统计的字符串表示锁定为可读格式
    /// </summary>
    [Fact]
    public void EventStatistics_ToString_UsesFixedFormat()
    {
        var statistics = new EventStatistics
        {
            LocalEventCount = 2,
            DistributedEventCount = 1,
            TotalEventCount = 3,
            EventTypes = ["A", "B"]
        };

        Assert.Equal("Local: 2, Distributed: 1, Total: 3, Types: 2", statistics.ToString());
    }

    /// <summary>
    /// 空聚合根的事件统计全为零
    /// </summary>
    [Fact]
    public void GetEventStatistics_WhenNoEvents_ReturnsZeros()
    {
        var aggregate = new SampleAggregateRoot(1);

        var statistics = aggregate.GetEventStatistics();

        Assert.Equal(0, statistics.LocalEventCount);
        Assert.Equal(0, statistics.DistributedEventCount);
        Assert.Equal(0, statistics.TotalEventCount);
        Assert.Empty(statistics.EventTypes);
    }

    /// <summary>
    /// 异步处理按合并顺序回调每一个事件
    /// </summary>
    [Fact]
    public async Task ProcessAllEventsAsync_InvokesHandlerForEveryEventInOrder()
    {
        var aggregate = new SampleAggregateRoot(1);
        var first = new SampleCreatedEvent("first");
        var second = new SampleUpdatedEvent("second");
        aggregate.RaiseLocal(first);
        aggregate.RaiseDistributed(second);

        var handled = new List<IDomainEvent>();
        await aggregate.ProcessAllEventsAsync(
            domainEvent =>
            {
                handled.Add(domainEvent);
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, handled.Count);
        Assert.Same(first, handled[0]);
        Assert.Same(second, handled[1]);
    }

    /// <summary>
    /// 异步处理不会清空事件缓冲区
    /// </summary>
    [Fact]
    public async Task ProcessAllEventsAsync_DoesNotConsumeEvents()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));

        await aggregate.ProcessAllEventsAsync(_ => Task.CompletedTask, TestContext.Current.CancellationToken);

        Assert.Single(aggregate.GetLocalEvents());
    }

    /// <summary>
    /// 处理器为空时抛出参数异常
    /// </summary>
    [Fact]
    public async Task ProcessAllEventsAsync_WhenHandlerIsNull_Throws()
    {
        var aggregate = new SampleAggregateRoot(1);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => aggregate.ProcessAllEventsAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 取消令牌已取消时中断处理
    /// </summary>
    [Fact]
    public async Task ProcessAllEventsAsync_WhenCancelled_Throws()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => aggregate.ProcessAllEventsAsync(_ => Task.CompletedTask, cancellation.Token));
    }

    /// <summary>
    /// 快照记录聚合类型、统计信息与最近事件
    /// </summary>
    [Fact]
    public void CreateSnapshot_CapturesTypeStatisticsAndRecentEvents()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));
        aggregate.RaiseDistributed(new SampleUpdatedEvent("b"));

        var before = DateTimeOffset.UtcNow;
        var snapshot = aggregate.CreateSnapshot();

        Assert.Equal(nameof(SampleAggregateRoot), snapshot.AggregateType);
        Assert.InRange(snapshot.SnapshotTime, before, DateTimeOffset.UtcNow);
        Assert.Equal(2, snapshot.EventStatistics.TotalEventCount);
        Assert.Equal(2, snapshot.RecentEvents.Count);
        Assert.Equal(nameof(SampleUpdatedEvent), snapshot.RecentEvents[0].EventType);
        Assert.Equal(nameof(SampleCreatedEvent), snapshot.RecentEvents[1].EventType);
    }

    /// <summary>
    /// 快照最多保留最近的十条事件且按事件序号倒序
    /// </summary>
    [Fact]
    public void CreateSnapshot_KeepsAtMostTenMostRecentEvents()
    {
        var aggregate = new SampleAggregateRoot(1);
        for (var index = 0; index < 12; index++)
        {
            aggregate.RaiseLocal(new SampleCreatedEvent($"e{index}"));
        }

        var snapshot = aggregate.CreateSnapshot();

        Assert.Equal(10, snapshot.RecentEvents.Count);
        Assert.Equal(12, snapshot.EventStatistics.TotalEventCount);

        var orders = snapshot.RecentEvents.Select(item => item.EventOrder).ToList();
        Assert.Equal(orders.OrderByDescending(order => order).ToList(), orders);
    }

    /// <summary>
    /// 快照中的事件标识与发生时间来自原始事件
    /// </summary>
    [Fact]
    public void CreateSnapshot_CopiesEventIdentityFromSourceEvent()
    {
        var aggregate = new SampleAggregateRoot(1);
        var domainEvent = new SampleCreatedEvent("a");
        aggregate.RaiseLocal(domainEvent);

        var snapshot = aggregate.CreateSnapshot();

        var eventSnapshot = Assert.Single(snapshot.RecentEvents);
        Assert.Equal(domainEvent.EventId, eventSnapshot.EventId);
        Assert.Equal(domainEvent.OccurredOn, eventSnapshot.OccurredOn);
    }

    /// <summary>
    /// 聚合根为空时创建快照抛出参数异常
    /// </summary>
    [Fact]
    public void CreateSnapshot_WhenAggregateIsNull_Throws()
    {
        IAggregateRoot? aggregate = null;

        Assert.Throws<ArgumentNullException>(() => { _ = aggregate!.CreateSnapshot(); });
        Assert.Throws<ArgumentNullException>(() => { _ = aggregate!.GetEventStatistics(); });
        Assert.Throws<ArgumentNullException>(() => { _ = aggregate!.GetEventsOfType<SampleCreatedEvent>(); });
        Assert.Throws<ArgumentNullException>(() => { _ = aggregate!.HasEventOfType<SampleCreatedEvent>(); });
    }
}
