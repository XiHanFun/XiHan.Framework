// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Aggregates.Abstracts;
using XiHan.Framework.Domain.Entities.Abstracts;
using XiHan.Framework.Domain.Events.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Aggregates;

/// <summary>
/// 聚合根基类测试
/// </summary>
/// <remarks>
/// 聚合根的事件缓冲区是「本地」「分布式」两条独立队列：本地事件在同一事务内派发，
/// 分布式事件走出站消息。清空其中一条不能影响另一条，否则会丢消息或重复投递。
/// </remarks>
public class AggregateRootBaseTests
{
    /// <summary>
    /// 新建聚合根不携带任何事件
    /// </summary>
    [Fact]
    public void NewAggregate_HasNoEvents()
    {
        var aggregate = new SampleAggregateRoot(1);

        Assert.Empty(aggregate.GetLocalEvents());
        Assert.Empty(aggregate.GetDistributedEvents());
        Assert.Equal(0, aggregate.TotalEventCount());
        Assert.False(aggregate.HasPending());
    }

    /// <summary>
    /// 添加本地事件只进本地队列
    /// </summary>
    [Fact]
    public void AddLocalEvent_OnlyFillsLocalQueue()
    {
        var aggregate = new SampleAggregateRoot(1);

        aggregate.RaiseLocal(new SampleCreatedEvent("a"));

        Assert.Single(aggregate.GetLocalEvents());
        Assert.Empty(aggregate.GetDistributedEvents());
        Assert.Equal(1, aggregate.TotalEventCount());
        Assert.True(aggregate.HasPending());
    }

    /// <summary>
    /// 添加分布式事件只进分布式队列
    /// </summary>
    [Fact]
    public void AddDistributedEvent_OnlyFillsDistributedQueue()
    {
        var aggregate = new SampleAggregateRoot(1);

        aggregate.RaiseDistributed(new SampleCreatedEvent("a"));

        Assert.Empty(aggregate.GetLocalEvents());
        Assert.Single(aggregate.GetDistributedEvents());
        Assert.True(aggregate.HasPending());
    }

    /// <summary>
    /// 事件记录保存原始事件实例
    /// </summary>
    [Fact]
    public void AddLocalEvent_KeepsOriginalEventInstance()
    {
        var aggregate = new SampleAggregateRoot(1);
        var domainEvent = new SampleCreatedEvent("a");

        aggregate.RaiseLocal(domainEvent);

        var record = Assert.Single(aggregate.GetLocalEvents());
        Assert.Same(domainEvent, record.EventData);
    }

    /// <summary>
    /// 添加空事件抛出参数异常
    /// </summary>
    [Fact]
    public void AddLocalEvent_WhenEventIsNull_Throws()
    {
        var aggregate = new SampleAggregateRoot(1);

        Assert.Throws<ArgumentNullException>(() => aggregate.RaiseLocal(null!));
        Assert.Throws<ArgumentNullException>(() => aggregate.RaiseDistributed(null!));
    }

    /// <summary>
    /// 清空本地事件不影响分布式事件
    /// </summary>
    [Fact]
    public void ClearLocalEvents_LeavesDistributedEventsIntact()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));
        aggregate.RaiseDistributed(new SampleUpdatedEvent("b"));

        aggregate.ClearLocalEvents();

        Assert.Empty(aggregate.GetLocalEvents());
        Assert.Single(aggregate.GetDistributedEvents());
    }

    /// <summary>
    /// 清空分布式事件不影响本地事件
    /// </summary>
    [Fact]
    public void ClearDistributedEvents_LeavesLocalEventsIntact()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));
        aggregate.RaiseDistributed(new SampleUpdatedEvent("b"));

        aggregate.ClearDistributedEvents();

        Assert.Single(aggregate.GetLocalEvents());
        Assert.Empty(aggregate.GetDistributedEvents());
    }

    /// <summary>
    /// 标记已提交会同时清空两条队列
    /// </summary>
    [Fact]
    public void MarkEventsAsCommitted_ClearsBothQueues()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));
        aggregate.RaiseDistributed(new SampleUpdatedEvent("b"));

        aggregate.CommitEvents();

        Assert.Empty(aggregate.GetLocalEvents());
        Assert.Empty(aggregate.GetDistributedEvents());
        Assert.False(aggregate.HasPending());
        Assert.Equal(0, aggregate.TotalEventCount());
    }

    /// <summary>
    /// 同一队列内的事件按添加顺序保持单调递增的事件序号
    /// </summary>
    [Fact]
    public void EventOrder_WithinQueue_IsIncreasing()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));
        aggregate.RaiseLocal(new SampleUpdatedEvent("b"));
        aggregate.RaiseLocal(new SampleUpdatedEvent("c"));

        var orders = aggregate.GetLocalEvents().Select(record => record.EventOrder).ToList();

        Assert.Equal(3, orders.Count);
        Assert.True(orders[0] < orders[1]);
        Assert.True(orders[1] < orders[2]);
    }

    /// <summary>
    /// 两个聚合根实例的事件缓冲区互相独立
    /// </summary>
    [Fact]
    public void EventBuffers_AcrossInstances_AreIsolated()
    {
        var first = new SampleAggregateRoot(1);
        var second = new SampleAggregateRoot(2);

        first.RaiseLocal(new SampleCreatedEvent("a"));

        Assert.Single(first.GetLocalEvents());
        Assert.Empty(second.GetLocalEvents());
    }

    /// <summary>
    /// 获取事件返回快照，之后再加事件不会影响已取出的集合
    /// </summary>
    [Fact]
    public void GetLocalEvents_ReturnsSnapshot()
    {
        var aggregate = new SampleAggregateRoot(1);
        aggregate.RaiseLocal(new SampleCreatedEvent("a"));

        var snapshot = aggregate.GetLocalEvents().ToList();
        aggregate.RaiseLocal(new SampleUpdatedEvent("b"));

        Assert.Single(snapshot);
        Assert.Equal(2, aggregate.GetLocalEvents().Count());
    }

    /// <summary>
    /// 无主键聚合根同样具备完整的事件缓冲能力
    /// </summary>
    [Fact]
    public void KeylessAggregate_SupportsEventBuffering()
    {
        var aggregate = new SampleKeylessAggregateRoot();

        aggregate.RaiseLocal(new SampleCreatedEvent("a"));
        aggregate.RaiseDistributed(new SampleUpdatedEvent("b"));

        Assert.Equal(2, aggregate.TotalEventCount());

        aggregate.CommitEvents();

        Assert.Equal(0, aggregate.TotalEventCount());
    }

    /// <summary>
    /// 聚合根继承完整审计与实体相等性语义
    /// </summary>
    [Fact]
    public void Aggregate_InheritsFullAuditAndEntitySemantics()
    {
        var before = DateTimeOffset.UtcNow;

        var aggregate = new SampleAggregateRoot(1);

        Assert.InRange(aggregate.CreatedTime, before, DateTimeOffset.UtcNow);
        Assert.False(aggregate.IsDeleted);
        Assert.Equal(0L, aggregate.RowVersion);
        Assert.True(aggregate.Equals(new SampleAggregateRoot(1)));
        Assert.False(aggregate.Equals(new SampleAggregateRoot(2)));
    }

    /// <summary>
    /// 聚合根接口同时聚合完整审计与领域事件管理契约
    /// </summary>
    [Fact]
    public void IAggregateRoot_Aggregates_AuditAndEventContracts()
    {
        Assert.True(typeof(IFullAuditedEntity).IsAssignableFrom(typeof(IAggregateRoot)));
        Assert.True(typeof(IDomainEventsManager).IsAssignableFrom(typeof(IAggregateRoot)));
        Assert.True(typeof(IAggregateRoot).IsAssignableFrom(typeof(IAggregateRoot<long>)));
        Assert.True(typeof(IFullAuditedEntity<long>).IsAssignableFrom(typeof(IAggregateRoot<long>)));

        Assert.IsAssignableFrom<IAggregateRoot<long>>(new SampleAggregateRoot(1));
        Assert.IsAssignableFrom<IAggregateRoot>(new SampleKeylessAggregateRoot());
    }
}
