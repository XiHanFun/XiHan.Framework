// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Mapping;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 收件箱映射测试
/// </summary>
public class EventInboxMapperTests
{
    /// <summary>
    /// 事件标识、消息标识、名称与数据往返保真
    /// </summary>
    [Fact]
    public void 事件标识消息标识名称与数据往返保真()
    {
        var id = Guid.NewGuid();
        byte[] data = [1, 2, 3, 250];
        var info = new IncomingEventInfo(id, "msg-001", "Order.Paid", data, DateTime.UtcNow);

        var restored = EventInboxMapper.ToEventInfo(EventInboxMapper.ToEntity(info));

        Assert.Equal(id, restored.Id);
        Assert.Equal("msg-001", restored.MessageId);
        Assert.Equal("Order.Paid", restored.EventName);
        Assert.Equal(data, restored.EventData);
    }

    /// <summary>
    /// 有消息标识时去重键等于消息标识
    /// </summary>
    [Fact]
    public void 有消息标识时去重键等于消息标识()
    {
        var info = new IncomingEventInfo(Guid.NewGuid(), "msg-002", "Order.Paid", [1], DateTime.UtcNow);

        var entity = EventInboxMapper.ToEntity(info);

        Assert.Equal("msg-002", entity.MessageId);
        Assert.Equal("msg-002", entity.DedupKey);
    }

    /// <summary>
    /// 无消息标识时去重键由事件标识生成
    /// </summary>
    /// <param name="messageId">空白的消息标识</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 无消息标识时去重键由事件标识生成(string? messageId)
    {
        var id = Guid.NewGuid();
        var info = new IncomingEventInfo(id, messageId!, "Order.Paid", [1], DateTime.UtcNow);

        var entity = EventInboxMapper.ToEntity(info);

        Assert.Null(entity.MessageId);
        Assert.Equal($"{EventInboxMapper.NoMessageIdKeyPrefix}{id:N}", entity.DedupKey);
    }

    /// <summary>
    /// 两条无消息标识的事件去重键不同
    /// </summary>
    [Fact]
    public void 两条无消息标识的事件去重键不同()
    {
        var first = EventInboxMapper.ToEntity(
            new IncomingEventInfo(Guid.NewGuid(), null!, "Order.Paid", [1], DateTime.UtcNow));
        var second = EventInboxMapper.ToEntity(
            new IncomingEventInfo(Guid.NewGuid(), null!, "Order.Paid", [1], DateTime.UtcNow));

        Assert.NotEqual(first.DedupKey, second.DedupKey);
    }

    /// <summary>
    /// 无消息标识的记录还原为空字符串
    /// </summary>
    [Fact]
    public void 无消息标识的记录还原为空字符串()
    {
        var info = new IncomingEventInfo(Guid.NewGuid(), null!, "Order.Paid", [1], DateTime.UtcNow);

        var restored = EventInboxMapper.ToEventInfo(EventInboxMapper.ToEntity(info));

        Assert.Equal(string.Empty, restored.MessageId);
    }

    /// <summary>
    /// 协调世界时往返后时刻不变且类型为协调世界时
    /// </summary>
    [Fact]
    public void 协调世界时往返后时刻不变()
    {
        var created = new DateTime(2026, 9, 28, 10, 30, 0, DateTimeKind.Utc);
        var info = new IncomingEventInfo(Guid.NewGuid(), "msg-003", "Order.Paid", [1], created);

        var restored = EventInboxMapper.ToEventInfo(EventInboxMapper.ToEntity(info));

        Assert.Equal(DateTimeKind.Utc, restored.CreatedTime.Kind);
        Assert.Equal(created, restored.CreatedTime);
    }

    /// <summary>
    /// 未指定类型的时间按协调世界时处理
    /// </summary>
    [Fact]
    public void 未指定类型的时间按协调世界时处理()
    {
        var created = new DateTime(2026, 9, 28, 10, 30, 0, DateTimeKind.Unspecified);
        var info = new IncomingEventInfo(Guid.NewGuid(), "msg-004", "Order.Paid", [1], created);

        var entity = EventInboxMapper.ToEntity(info);

        Assert.Equal(TimeSpan.Zero, entity.CreatedTime.Offset);
        Assert.Equal(created.Ticks, entity.CreatedTime.UtcDateTime.Ticks);
    }

    /// <summary>
    /// 关联标识往返保真
    /// </summary>
    [Fact]
    public void 关联标识往返保真()
    {
        var info = new IncomingEventInfo(Guid.NewGuid(), "msg-005", "Order.Paid", [1], DateTime.UtcNow);
        info.SetCorrelationId("corr-inbox");

        var restored = EventInboxMapper.ToEventInfo(EventInboxMapper.ToEntity(info));

        Assert.Equal("corr-inbox", restored.GetCorrelationId());
    }

    /// <summary>
    /// 入库时状态为待处理且无领取与完结信息
    /// </summary>
    [Fact]
    public void 入库时状态为待处理且无领取与完结信息()
    {
        var info = new IncomingEventInfo(Guid.NewGuid(), "msg-006", "Order.Paid", [1], DateTime.UtcNow);

        var entity = EventInboxMapper.ToEntity(info);

        Assert.Equal(SysEventInbox.StatusPending, entity.Status);
        Assert.Equal(0, entity.RetryCount);
        Assert.Null(entity.NextRetryTime);
        Assert.Null(entity.ClaimToken);
        Assert.Null(entity.ClaimTime);
        Assert.Null(entity.HandledTime);
    }
}
