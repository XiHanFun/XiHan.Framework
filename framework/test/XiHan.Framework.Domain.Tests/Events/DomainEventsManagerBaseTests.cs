// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Events;
using XiHan.Framework.Domain.Events.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Events;

/// <summary>
/// 领域事件管理器基类测试
/// </summary>
/// <remarks>
/// 内部用 ConcurrentQueue 承载，宣称线程安全，因此并发投递必须一条不丢。
/// </remarks>
public class DomainEventsManagerBaseTests
{
    /// <summary>
    /// 新建管理器两条队列都为空
    /// </summary>
    [Fact]
    public void NewManager_HasNoEvents()
    {
        var manager = new DomainEventsManagerBase();

        Assert.Empty(manager.GetLocalEvents());
        Assert.Empty(manager.GetDistributedEvents());
        Assert.Equal(0, manager.GetTotalEventCount());
        Assert.False(manager.HasPendingEvents());
    }

    /// <summary>
    /// 本地事件按先进先出顺序返回
    /// </summary>
    [Fact]
    public void AddLocalEvent_PreservesFifoOrder()
    {
        var manager = new DomainEventsManagerBase();
        var first = new SampleCreatedEvent("first");
        var second = new SampleUpdatedEvent("second");

        manager.AddLocalEvent(first);
        manager.AddLocalEvent(second);

        var records = manager.GetLocalEvents().ToList();

        Assert.Equal(2, records.Count);
        Assert.Same(first, records[0].EventData);
        Assert.Same(second, records[1].EventData);
        Assert.True(records[0].EventOrder < records[1].EventOrder);
    }

    /// <summary>
    /// 分布式事件独立成队
    /// </summary>
    [Fact]
    public void AddDistributedEvent_FillsSeparateQueue()
    {
        var manager = new DomainEventsManagerBase();

        manager.AddDistributedEvent(new SampleCreatedEvent("a"));

        Assert.Empty(manager.GetLocalEvents());
        Assert.Single(manager.GetDistributedEvents());
        Assert.Equal(1, manager.GetTotalEventCount());
        Assert.True(manager.HasPendingEvents());
    }

    /// <summary>
    /// 添加空的本地事件抛出参数异常
    /// </summary>
    [Fact]
    public void AddLocalEvent_WhenEventIsNull_Throws()
    {
        var manager = new DomainEventsManagerBase();

        Assert.Throws<ArgumentNullException>(() => manager.AddLocalEvent(null!));
    }

    /// <summary>
    /// 添加空的分布式事件抛出参数异常
    /// </summary>
    [Fact]
    public void AddDistributedEvent_WhenEventIsNull_Throws()
    {
        var manager = new DomainEventsManagerBase();

        Assert.Throws<ArgumentNullException>(() => manager.AddDistributedEvent(null!));
    }

    /// <summary>
    /// 事件总数是两条队列之和
    /// </summary>
    [Fact]
    public void GetTotalEventCount_SumsBothQueues()
    {
        var manager = new DomainEventsManagerBase();
        manager.AddLocalEvent(new SampleCreatedEvent("a"));
        manager.AddLocalEvent(new SampleUpdatedEvent("b"));
        manager.AddDistributedEvent(new SampleUpdatedEvent("c"));

        Assert.Equal(3, manager.GetTotalEventCount());
    }

    /// <summary>
    /// 清空本地事件后仅本地队列为空
    /// </summary>
    [Fact]
    public void ClearLocalEvents_OnlyEmptiesLocalQueue()
    {
        var manager = new DomainEventsManagerBase();
        manager.AddLocalEvent(new SampleCreatedEvent("a"));
        manager.AddDistributedEvent(new SampleUpdatedEvent("b"));

        manager.ClearLocalEvents();

        Assert.Empty(manager.GetLocalEvents());
        Assert.Single(manager.GetDistributedEvents());
        Assert.True(manager.HasPendingEvents());
    }

    /// <summary>
    /// 清空分布式事件后仅分布式队列为空
    /// </summary>
    [Fact]
    public void ClearDistributedEvents_OnlyEmptiesDistributedQueue()
    {
        var manager = new DomainEventsManagerBase();
        manager.AddLocalEvent(new SampleCreatedEvent("a"));
        manager.AddDistributedEvent(new SampleUpdatedEvent("b"));

        manager.ClearDistributedEvents();

        Assert.Single(manager.GetLocalEvents());
        Assert.Empty(manager.GetDistributedEvents());
    }

    /// <summary>
    /// 标记已提交后两条队列全空
    /// </summary>
    [Fact]
    public void MarkEventsAsCommitted_EmptiesBothQueues()
    {
        var manager = new DomainEventsManagerBase();
        manager.AddLocalEvent(new SampleCreatedEvent("a"));
        manager.AddDistributedEvent(new SampleUpdatedEvent("b"));

        manager.MarkEventsAsCommitted();

        Assert.Equal(0, manager.GetTotalEventCount());
        Assert.False(manager.HasPendingEvents());
    }

    /// <summary>
    /// 取出的事件集合是快照，后续入队不影响已取出的结果
    /// </summary>
    [Fact]
    public void GetLocalEvents_ReturnsDetachedSnapshot()
    {
        var manager = new DomainEventsManagerBase();
        manager.AddLocalEvent(new SampleCreatedEvent("a"));

        var snapshot = manager.GetLocalEvents();
        manager.AddLocalEvent(new SampleUpdatedEvent("b"));

        Assert.Single(snapshot);
        Assert.Equal(2, manager.GetLocalEvents().Count());
    }

    /// <summary>
    /// 并发投递本地事件一条不丢且顺序号互不重复
    /// </summary>
    [Fact]
    public void AddLocalEvent_UnderConcurrency_LosesNoEvent()
    {
        const int count = 500;
        var manager = new DomainEventsManagerBase();

        Parallel.For(0, count, index => manager.AddLocalEvent(new SampleCreatedEvent($"e{index}")));

        var records = manager.GetLocalEvents().ToList();

        Assert.Equal(count, records.Count);
        Assert.Equal(count, records.Select(record => record.EventOrder).Distinct().Count());
    }

    /// <summary>
    /// 并发同时投递两条队列时总数准确
    /// </summary>
    [Fact]
    public void AddEvents_UnderConcurrencyOnBothQueues_KeepsAccurateTotal()
    {
        const int count = 300;
        var manager = new DomainEventsManagerBase();

        Parallel.For(0, count, index =>
        {
            manager.AddLocalEvent(new SampleCreatedEvent($"l{index}"));
            manager.AddDistributedEvent(new SampleUpdatedEvent($"d{index}"));
        });

        Assert.Equal(count, manager.GetLocalEvents().Count());
        Assert.Equal(count, manager.GetDistributedEvents().Count());
        Assert.Equal(count * 2, manager.GetTotalEventCount());
    }

    /// <summary>
    /// 管理器实现领域事件管理契约
    /// </summary>
    [Fact]
    public void DomainEventsManagerBase_ImplementsManagerContract()
    {
        Assert.IsAssignableFrom<IDomainEventsManager>(new DomainEventsManagerBase());
    }
}
