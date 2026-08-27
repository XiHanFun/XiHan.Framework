// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 分布式事件源枚举测试
/// </summary>
/// <remarks>
/// 该枚举会随诊断/追踪数据序列化外传，System.Text.Json 默认以数值形式写出，
/// 因此数值分配属于对外契约，插入新成员时必须追加在末尾而不能改动既有序号。
/// </remarks>
public class DistributedEventSourceTests
{
    /// <summary>
    /// 成员数值锁定
    /// </summary>
    [Theory]
    [InlineData(DistributedEventSource.Direct, 0)]
    [InlineData(DistributedEventSource.Inbox, 1)]
    [InlineData(DistributedEventSource.Outbox, 2)]
    public void Members_HavePinnedNumericValues(DistributedEventSource source, int expected)
    {
        Assert.Equal(expected, (int)source);
    }

    /// <summary>
    /// 成员集合锁定，防止悄悄增删
    /// </summary>
    [Fact]
    public void Members_AreExactlyThree()
    {
        var names = Enum.GetNames<DistributedEventSource>();

        Assert.Equal(3, names.Length);
        Assert.Contains(nameof(DistributedEventSource.Direct), names);
        Assert.Contains(nameof(DistributedEventSource.Inbox), names);
        Assert.Contains(nameof(DistributedEventSource.Outbox), names);
    }

    /// <summary>
    /// 默认值是直接发送，未显式赋值的事件不会被误判为来自事件盒
    /// </summary>
    [Fact]
    public void Default_IsDirect()
    {
        Assert.Equal(DistributedEventSource.Direct, default(DistributedEventSource));
    }

    /// <summary>
    /// 以数值形式序列化
    /// </summary>
    [Fact]
    public void Serialize_WritesNumericValue()
    {
        Assert.Equal("1", JsonSerializer.Serialize(DistributedEventSource.Inbox));
        Assert.Equal("2", JsonSerializer.Serialize(DistributedEventSource.Outbox));
    }

    /// <summary>
    /// 数值可反序列化回对应成员
    /// </summary>
    [Fact]
    public void Deserialize_FromNumericValue_RestoresMember()
    {
        Assert.Equal(DistributedEventSource.Outbox, JsonSerializer.Deserialize<DistributedEventSource>("2"));
    }
}
