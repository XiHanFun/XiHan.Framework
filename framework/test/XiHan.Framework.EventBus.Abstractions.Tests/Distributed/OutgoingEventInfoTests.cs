// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.ObjectMapping.Extensions.Data;

namespace XiHan.Framework.EventBus.Abstractions.Tests.Distributed;

/// <summary>
/// 出站事件记录测试
/// </summary>
/// <remarks>
/// 发件箱的持久化记录。与入站记录的唯一结构差异是没有消息标识：
/// 出站侧由本地事务保证只写一次，去重责任在消费端而不在这里。
/// </remarks>
public class OutgoingEventInfoTests
{
    /// <summary>
    /// 构造函数写入的字段原样暴露
    /// </summary>
    [Fact]
    public void Ctor_WithValidArguments_ExposesAllFields()
    {
        var id = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("payload");
        var createdTime = EventInfoFactory.FixedCreatedTime;

        var info = new OutgoingEventInfo(id, "sample.event", payload, createdTime);

        Assert.Equal(id, info.Id);
        Assert.Equal("sample.event", info.EventName);
        Assert.Same(payload, info.EventData);
        Assert.Equal(createdTime, info.CreatedTime);
    }

    /// <summary>
    /// 构造后扩展属性容器可用且不含关联标识
    /// </summary>
    [Fact]
    public void Ctor_InitializesExtraProperties()
    {
        var info = EventInfoFactory.CreateOutgoing();

        Assert.NotNull(info.ExtraProperties);
        Assert.Null(info.GetCorrelationId());
    }

    /// <summary>
    /// 出站记录不携带消息标识
    /// </summary>
    [Fact]
    public void OutgoingEventInfo_HasNoMessageId()
    {
        Assert.Null(typeof(OutgoingEventInfo).GetProperty("MessageId"));
        Assert.Null(typeof(IOutgoingEventInfo).GetProperty("MessageId"));
    }

    /// <summary>
    /// 事件名为空时构造失败
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_WhenEventNameIsBlank_ThrowsArgumentException(string? eventName)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            _ = new OutgoingEventInfo(
                Guid.NewGuid(),
                eventName!,
                Encoding.UTF8.GetBytes("payload"),
                EventInfoFactory.FixedCreatedTime);
        });

        Assert.Equal("eventName", exception.ParamName);
    }

    /// <summary>
    /// 事件名超过上限时构造失败
    /// </summary>
    [Fact]
    public void Ctor_WhenEventNameTooLong_ThrowsArgumentException()
    {
        var tooLong = new string('a', OutgoingEventInfo.MaxEventNameLength + 1);

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            _ = new OutgoingEventInfo(
                Guid.NewGuid(),
                tooLong,
                Encoding.UTF8.GetBytes("payload"),
                EventInfoFactory.FixedCreatedTime);
        });

        Assert.Equal("eventName", exception.ParamName);
        Assert.Contains(OutgoingEventInfo.MaxEventNameLength.ToString(), exception.Message);
    }

    /// <summary>
    /// 事件名恰好等于上限时可以构造
    /// </summary>
    [Fact]
    public void Ctor_WhenEventNameAtMaxLength_IsAccepted()
    {
        var atLimit = new string('a', OutgoingEventInfo.MaxEventNameLength);

        var info = new OutgoingEventInfo(
            Guid.NewGuid(),
            atLimit,
            Encoding.UTF8.GetBytes("payload"),
            EventInfoFactory.FixedCreatedTime);

        Assert.Equal(atLimit, info.EventName);
    }

    /// <summary>
    /// 事件字节为空引用时构造失败
    /// </summary>
    [Fact]
    public void Ctor_WhenEventDataIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new OutgoingEventInfo(
                Guid.NewGuid(),
                "sample.event",
                null!,
                EventInfoFactory.FixedCreatedTime);
        });

        Assert.Equal("eventData", exception.ParamName);
    }

    /// <summary>
    /// 事件名长度上限默认为 256，与入站记录一致
    /// </summary>
    [Fact]
    public void MaxEventNameLength_DefaultsTo256AndMatchesIncoming()
    {
        Assert.Equal(256, OutgoingEventInfo.MaxEventNameLength);
        Assert.Equal(IncomingEventInfo.MaxEventNameLength, OutgoingEventInfo.MaxEventNameLength);
    }

    /// <summary>
    /// 关联标识写入后可读回，且落在约定的扩展属性键上
    /// </summary>
    [Fact]
    public void SetCorrelationId_StoresUnderCorrelationIdHeaderKey()
    {
        var info = EventInfoFactory.CreateOutgoing();

        info.SetCorrelationId("trace-1");

        Assert.Equal("trace-1", info.GetCorrelationId());
        Assert.Equal("trace-1", info.ExtraProperties[EventBusConsts.CorrelationIdHeaderName]);
    }

    /// <summary>
    /// 关联标识为 null 时按空引用拒绝
    /// </summary>
    [Fact]
    public void SetCorrelationId_WhenNull_ThrowsArgumentNullException()
    {
        var info = EventInfoFactory.CreateOutgoing();

        Assert.Throws<ArgumentNullException>(() =>
        {
            info.SetCorrelationId(null!);
        });
    }

    /// <summary>
    /// 关联标识为空白时按非法参数拒绝
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetCorrelationId_WhenBlank_ThrowsArgumentException(string correlationId)
    {
        var info = EventInfoFactory.CreateOutgoing();

        Assert.Throws<ArgumentException>(() =>
        {
            info.SetCorrelationId(correlationId);
        });
    }

    /// <summary>
    /// 出站与入站记录使用同一个关联标识键，链路才能对上
    /// </summary>
    [Fact]
    public void SetCorrelationId_UsesSameKeyAsIncoming()
    {
        var outgoing = EventInfoFactory.CreateOutgoing();
        var incoming = EventInfoFactory.CreateIncoming();

        outgoing.SetCorrelationId("trace-1");
        incoming.SetCorrelationId("trace-1");

        Assert.Equal(outgoing.GetCorrelationId(), incoming.GetCorrelationId());
        Assert.True(outgoing.ExtraProperties.ContainsKey(EventBusConsts.CorrelationIdHeaderName));
        Assert.True(incoming.ExtraProperties.ContainsKey(EventBusConsts.CorrelationIdHeaderName));
    }

    /// <summary>
    /// 记录实现出站事件契约与扩展属性契约
    /// </summary>
    [Fact]
    public void OutgoingEventInfo_ImplementsContracts()
    {
        var info = EventInfoFactory.CreateOutgoing();

        Assert.IsAssignableFrom<IOutgoingEventInfo>(info);
        Assert.IsAssignableFrom<IHasExtraProperties>(info);
    }

    /// <summary>
    /// 往返序列化后各字段保持不变
    /// </summary>
    [Fact]
    public void RoundTrip_KeepsAllFields()
    {
        var original = EventInfoFactory.CreateOutgoing("sample.event", "payload");

        var restored = JsonSerializer.Deserialize<OutgoingEventInfo>(JsonSerializer.Serialize(original));

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.EventName, restored.EventName);
        Assert.Equal(original.EventData, restored.EventData);
        Assert.Equal(original.CreatedTime, restored.CreatedTime);
    }
}
