// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Events;
using XiHan.Framework.Domain.Tests.Samples;

namespace XiHan.Framework.Domain.Tests.Events;

/// <summary>
/// 领域事件记录测试
/// </summary>
/// <remarks>
/// 记录是「事件 + 顺序号」的不可变包装，两个属性都必须只读，
/// 否则事件缓冲区取出快照后仍可被外部改写顺序。
/// </remarks>
public class DomainEventRecordTests
{
    /// <summary>
    /// 构造函数原样保存事件数据与顺序号
    /// </summary>
    [Fact]
    public void Constructor_KeepsEventDataAndOrder()
    {
        var domainEvent = new SampleCreatedEvent("a");

        var record = new DomainEventRecord(domainEvent, 42);

        Assert.Same(domainEvent, record.EventData);
        Assert.Equal(42L, record.EventOrder);
    }

    /// <summary>
    /// 记录不做非空校验，允许负数顺序号
    /// </summary>
    [Fact]
    public void Constructor_WithNegativeOrder_KeepsValue()
    {
        var record = new DomainEventRecord(new SampleCreatedEvent("a"), -1);

        Assert.Equal(-1L, record.EventOrder);
    }

    /// <summary>
    /// 事件数据与顺序号是只读属性
    /// </summary>
    [Fact]
    public void Properties_AreReadOnly()
    {
        var type = typeof(DomainEventRecord);

        Assert.Null(type.GetProperty(nameof(DomainEventRecord.EventData))!.SetMethod);
        Assert.Null(type.GetProperty(nameof(DomainEventRecord.EventOrder))!.SetMethod);
    }
}
