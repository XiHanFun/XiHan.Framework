// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests.Distributed;

/// <summary>
/// 分布式事件接收信息测试
/// </summary>
/// <remarks>
/// 接收侧的诊断快照结构，与发送侧 <see cref="DistributedEventSent"/> 形状一致但语义相反：
/// <c>Source</c> 描述这条事件是直接收到的、还是从收件箱/发件箱回灌的。
/// </remarks>
public class DistributedEventReceivedTests
{
    /// <summary>
    /// 新建实例的来源默认为直接接收
    /// </summary>
    [Fact]
    public void Source_ByDefault_IsDirect()
    {
        var received = new DistributedEventReceived();

        Assert.Equal(DistributedEventSource.Direct, received.Source);
    }

    /// <summary>
    /// 事件名与事件数据在未赋值时为空
    /// </summary>
    [Fact]
    public void EventNameAndData_ByDefault_AreNull()
    {
        var received = new DistributedEventReceived();

        Assert.Null(received.EventName);
        Assert.Null(received.EventData);
    }

    /// <summary>
    /// 三种来源都能被记录
    /// </summary>
    [Theory]
    [InlineData(DistributedEventSource.Direct)]
    [InlineData(DistributedEventSource.Inbox)]
    [InlineData(DistributedEventSource.Outbox)]
    public void Source_AcceptsEveryDefinedSource(DistributedEventSource source)
    {
        var received = new DistributedEventReceived
        {
            Source = source,
            EventName = "sample.event",
            EventData = new SampleEvent()
        };

        Assert.Equal(source, received.Source);
    }

    /// <summary>
    /// 事件数据以引用方式持有，不做拷贝
    /// </summary>
    [Fact]
    public void EventData_HoldsSameReference()
    {
        var eventData = new SampleEvent { Payload = "received" };
        var received = new DistributedEventReceived
        {
            EventData = eventData
        };

        Assert.Same(eventData, received.EventData);
    }

    /// <summary>
    /// 往返序列化后来源与事件名保持不变
    /// </summary>
    [Fact]
    public void RoundTrip_KeepsSourceAndEventName()
    {
        var received = new DistributedEventReceived
        {
            Source = DistributedEventSource.Inbox,
            EventName = "sample.event",
            EventData = "payload"
        };

        var restored = JsonSerializer.Deserialize<DistributedEventReceived>(JsonSerializer.Serialize(received));

        Assert.NotNull(restored);
        Assert.Equal(DistributedEventSource.Inbox, restored.Source);
        Assert.Equal("sample.event", restored.EventName);

        var element = Assert.IsType<JsonElement>(restored.EventData);
        Assert.Equal("payload", element.GetString());
    }

    /// <summary>
    /// 接收信息与发送信息是两个独立类型，不存在继承关系
    /// </summary>
    /// <remarks>
    /// 两者形状相同容易被误当作可互换，这里明确它们没有共同基类，避免诊断代码写出错误的类型判断。
    /// </remarks>
    [Fact]
    public void ReceivedAndSent_AreUnrelatedTypes()
    {
        Assert.False(typeof(DistributedEventReceived).IsAssignableTo(typeof(DistributedEventSent)));
        Assert.False(typeof(DistributedEventSent).IsAssignableTo(typeof(DistributedEventReceived)));
        Assert.Equal(typeof(object), typeof(DistributedEventReceived).BaseType);
        Assert.Equal(typeof(object), typeof(DistributedEventSent).BaseType);
    }
}
