// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 分布式事件发送信息测试
/// </summary>
/// <remarks>
/// 这是发送侧的诊断快照结构：事件名 + 原始事件对象 + 来源。
/// 它会被序列化外传给诊断/追踪链路，所以字段名与来源的数值形式属于对外契约。
/// </remarks>
public class DistributedEventSentTests
{
    /// <summary>
    /// 新建实例的来源默认为直接发送
    /// </summary>
    [Fact]
    public void Source_ByDefault_IsDirect()
    {
        var sent = new DistributedEventSent();

        Assert.Equal(DistributedEventSource.Direct, sent.Source);
    }

    /// <summary>
    /// 事件名与事件数据在未赋值时为空，调用方必须自行填充
    /// </summary>
    [Fact]
    public void EventNameAndData_ByDefault_AreNull()
    {
        var sent = new DistributedEventSent();

        Assert.Null(sent.EventName);
        Assert.Null(sent.EventData);
    }

    /// <summary>
    /// 三个属性都可写，构造后由发送侧逐一填充
    /// </summary>
    [Fact]
    public void Properties_AreAssignable()
    {
        var eventData = new SampleEvent { Payload = "sent" };
        var sent = new DistributedEventSent
        {
            Source = DistributedEventSource.Outbox,
            EventName = "sample.event",
            EventData = eventData
        };

        Assert.Equal(DistributedEventSource.Outbox, sent.Source);
        Assert.Equal("sample.event", sent.EventName);
        Assert.Same(eventData, sent.EventData);
    }

    /// <summary>
    /// 事件数据以 object 承载，任意事件类型都能装入
    /// </summary>
    [Fact]
    public void EventData_AcceptsAnyEventType()
    {
        var sent = new DistributedEventSent
        {
            EventData = new AnotherSampleEvent()
        };

        Assert.IsType<AnotherSampleEvent>(sent.EventData);
    }

    /// <summary>
    /// 序列化字段名与来源数值形式锁定
    /// </summary>
    [Fact]
    public void Serialize_WritesPinnedMemberNames()
    {
        var sent = new DistributedEventSent
        {
            Source = DistributedEventSource.Outbox,
            EventName = "sample.event",
            EventData = "payload"
        };

        var json = JsonSerializer.Serialize(sent);

        Assert.Contains("\"Source\":2", json);
        Assert.Contains("\"EventName\":\"sample.event\"", json);
        Assert.Contains("\"EventData\":\"payload\"", json);
    }

    /// <summary>
    /// 往返序列化后来源与事件名保持不变
    /// </summary>
    /// <remarks>
    /// 事件数据声明为 object，往返后必然退化为 <see cref="JsonElement"/>，
    /// 这正是诊断消费方需要预期的形状，因此按 JsonElement 断言而不是原始类型。
    /// </remarks>
    [Fact]
    public void RoundTrip_KeepsSourceAndEventName()
    {
        var sent = new DistributedEventSent
        {
            Source = DistributedEventSource.Inbox,
            EventName = "sample.event",
            EventData = "payload"
        };

        var restored = JsonSerializer.Deserialize<DistributedEventSent>(JsonSerializer.Serialize(sent));

        Assert.NotNull(restored);
        Assert.Equal(DistributedEventSource.Inbox, restored.Source);
        Assert.Equal("sample.event", restored.EventName);

        var element = Assert.IsType<JsonElement>(restored.EventData);
        Assert.Equal(JsonValueKind.String, element.ValueKind);
        Assert.Equal("payload", element.GetString());
    }
}
