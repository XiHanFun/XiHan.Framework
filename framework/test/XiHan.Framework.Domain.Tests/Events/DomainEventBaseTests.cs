// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Events.Abstracts;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Events;

/// <summary>
/// 领域事件基类测试
/// </summary>
/// <remarks>
/// 事件标识与发生时间在构造时就固定下来且只读，这是幂等消费方去重的前提。
/// </remarks>
public class DomainEventBaseTests
{
    /// <summary>
    /// 构造时生成非空事件标识
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_AssignsNonEmptyEventId()
    {
        var domainEvent = new SampleCreatedEvent("a");

        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
    }

    /// <summary>
    /// 不同事件实例的标识互不相同
    /// </summary>
    [Fact]
    public void Constructor_ForDifferentInstances_AssignsDistinctEventIds()
    {
        var first = new SampleCreatedEvent("a");
        var second = new SampleCreatedEvent("a");

        Assert.NotEqual(first.EventId, second.EventId);
    }

    /// <summary>
    /// 事件标识在实例生命周期内保持不变
    /// </summary>
    [Fact]
    public void EventId_AcrossReads_IsStable()
    {
        var domainEvent = new SampleCreatedEvent("a");

        Assert.Equal(domainEvent.EventId, domainEvent.EventId);
    }

    /// <summary>
    /// 构造时写入 UTC 发生时间
    /// </summary>
    [Fact]
    public void Constructor_ByDefault_SetsOccurredOnToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var domainEvent = new SampleCreatedEvent("a");

        Assert.InRange(domainEvent.OccurredOn, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, domainEvent.OccurredOn.Offset);
    }

    /// <summary>
    /// 字符串表示包含运行时类型名与事件标识
    /// </summary>
    [Fact]
    public void ToString_ContainsRuntimeTypeNameAndEventId()
    {
        var domainEvent = new SampleCreatedEvent("a");

        var text = domainEvent.ToString();

        Assert.StartsWith(nameof(SampleCreatedEvent), text, StringComparison.Ordinal);
        Assert.Contains(domainEvent.EventId.ToString(), text, StringComparison.Ordinal);
        Assert.Contains("OccurredOn:", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// 领域事件实现领域事件契约
    /// </summary>
    [Fact]
    public void DomainEventBase_ImplementsDomainEventContract()
    {
        Assert.IsAssignableFrom<IDomainEvent>(new SampleCreatedEvent("a"));
    }

    /// <summary>
    /// 领域事件不实现值相等，按引用区分
    /// </summary>
    [Fact]
    public void Equals_ForDifferentInstancesWithSamePayload_ReturnsFalse()
    {
        var first = new SampleCreatedEvent("a");
        var second = new SampleCreatedEvent("a");

        // 事件是消息不是值对象，同载荷的两条消息必须是两条不同的消息
        Assert.False(first.Equals(second));
        Assert.True(first.Equals(first));
    }
}
