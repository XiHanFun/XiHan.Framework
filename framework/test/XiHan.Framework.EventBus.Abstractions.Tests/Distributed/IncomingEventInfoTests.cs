// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.ObjectMapping.Extensions.Data;

namespace XiHan.Framework.EventBus.Abstractions.Tests;

/// <summary>
/// 入站事件记录测试
/// </summary>
/// <remarks>
/// 这是收件箱的持久化记录：主键、消息标识（幂等去重用）、事件名（路由用）、事件字节、创建时间。
/// 事件名会落到有长度上限的列上，所以构造期就要拦住超长值；关联标识则借道扩展属性透传，
/// 键必须是 <see cref="EventBusConsts.CorrelationIdHeaderName"/>，否则跨进程链路会断。
/// </remarks>
public class IncomingEventInfoTests
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

        var info = new IncomingEventInfo(id, "message-1", "sample.event", payload, createdTime);

        Assert.Equal(id, info.Id);
        Assert.Equal("message-1", info.MessageId);
        Assert.Equal("sample.event", info.EventName);
        Assert.Same(payload, info.EventData);
        Assert.Equal(createdTime, info.CreatedTime);
    }

    /// <summary>
    /// 构造后扩展属性容器可用且为空
    /// </summary>
    [Fact]
    public void Ctor_InitializesExtraProperties()
    {
        var info = EventInfoFactory.CreateIncoming();

        Assert.NotNull(info.ExtraProperties);
        Assert.Null(info.GetCorrelationId());
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
            _ = new IncomingEventInfo(
                Guid.NewGuid(),
                "message-1",
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
        var tooLong = new string('a', IncomingEventInfo.MaxEventNameLength + 1);

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            _ = new IncomingEventInfo(
                Guid.NewGuid(),
                "message-1",
                tooLong,
                Encoding.UTF8.GetBytes("payload"),
                EventInfoFactory.FixedCreatedTime);
        });

        Assert.Equal("eventName", exception.ParamName);
        Assert.Contains(IncomingEventInfo.MaxEventNameLength.ToString(), exception.Message);
    }

    /// <summary>
    /// 事件名恰好等于上限时可以构造
    /// </summary>
    [Fact]
    public void Ctor_WhenEventNameAtMaxLength_IsAccepted()
    {
        var atLimit = new string('a', IncomingEventInfo.MaxEventNameLength);

        var info = new IncomingEventInfo(
            Guid.NewGuid(),
            "message-1",
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
            _ = new IncomingEventInfo(
                Guid.NewGuid(),
                "message-1",
                "sample.event",
                null!,
                EventInfoFactory.FixedCreatedTime);
        });

        Assert.Equal("eventData", exception.ParamName);
    }

    /// <summary>
    /// 空字节数组是合法载荷，表示无正文事件
    /// </summary>
    [Fact]
    public void Ctor_WhenEventDataIsEmptyArray_IsAccepted()
    {
        var info = new IncomingEventInfo(
            Guid.NewGuid(),
            "message-1",
            "sample.event",
            [],
            EventInfoFactory.FixedCreatedTime);

        Assert.Empty(info.EventData);
    }

    /// <summary>
    /// 事件名长度上限默认为 256
    /// </summary>
    [Fact]
    public void MaxEventNameLength_DefaultsTo256()
    {
        Assert.Equal(256, IncomingEventInfo.MaxEventNameLength);
    }

    /// <summary>
    /// 关联标识写入后可读回，且落在约定的扩展属性键上
    /// </summary>
    [Fact]
    public void SetCorrelationId_StoresUnderCorrelationIdHeaderKey()
    {
        var info = EventInfoFactory.CreateIncoming();

        info.SetCorrelationId("trace-1");

        Assert.Equal("trace-1", info.GetCorrelationId());
        Assert.Equal("trace-1", info.ExtraProperties[EventBusConsts.CorrelationIdHeaderName]);
    }

    /// <summary>
    /// 重复写入关联标识以最后一次为准
    /// </summary>
    [Fact]
    public void SetCorrelationId_CalledTwice_KeepsLatestValue()
    {
        var info = EventInfoFactory.CreateIncoming();

        info.SetCorrelationId("trace-1");
        info.SetCorrelationId("trace-2");

        Assert.Equal("trace-2", info.GetCorrelationId());
    }

    /// <summary>
    /// 关联标识为 null 时按空引用拒绝
    /// </summary>
    [Fact]
    public void SetCorrelationId_WhenNull_ThrowsArgumentNullException()
    {
        var info = EventInfoFactory.CreateIncoming();

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
        var info = EventInfoFactory.CreateIncoming();

        Assert.Throws<ArgumentException>(() =>
        {
            info.SetCorrelationId(correlationId);
        });
    }

    /// <summary>
    /// 未写入关联标识时读取返回空
    /// </summary>
    [Fact]
    public void GetCorrelationId_WhenNotSet_ReturnsNull()
    {
        Assert.Null(EventInfoFactory.CreateIncoming().GetCorrelationId());
    }

    /// <summary>
    /// 记录实现入站事件契约与扩展属性契约
    /// </summary>
    [Fact]
    public void IncomingEventInfo_ImplementsContracts()
    {
        var info = EventInfoFactory.CreateIncoming();

        Assert.IsAssignableFrom<IIncomingEventInfo>(info);
        Assert.IsAssignableFrom<IHasExtraProperties>(info);
    }

    /// <summary>
    /// 往返序列化后各字段保持不变
    /// </summary>
    /// <remarks>
    /// 收件箱记录会以 JSON 形式在中间件与存储之间流转，字节载荷按 Base64 编码往返，
    /// 只读属性由唯一的公开构造函数回填，这条链路断了会导致事件在重放时丢字段。
    /// </remarks>
    [Fact]
    public void RoundTrip_KeepsAllFields()
    {
        var original = EventInfoFactory.CreateIncoming("message-42", "sample.event", "payload");

        var restored = JsonSerializer.Deserialize<IncomingEventInfo>(JsonSerializer.Serialize(original));

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.MessageId, restored.MessageId);
        Assert.Equal(original.EventName, restored.EventName);
        Assert.Equal(original.EventData, restored.EventData);
        Assert.Equal(original.CreatedTime, restored.CreatedTime);
    }
}
