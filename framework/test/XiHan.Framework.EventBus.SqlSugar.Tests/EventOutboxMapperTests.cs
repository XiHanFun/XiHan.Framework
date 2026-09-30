// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;
using XiHan.Framework.EventBus.SqlSugar.Mapping;

namespace XiHan.Framework.EventBus.SqlSugar.Tests;

/// <summary>
/// 发件箱映射测试
/// </summary>
public class EventOutboxMapperTests
{
    /// <summary>
    /// 事件标识、名称与数据往返保真
    /// </summary>
    [Fact]
    public void 事件标识名称与数据往返保真()
    {
        var id = Guid.NewGuid();
        byte[] data = [1, 2, 3, 250];
        var info = new OutgoingEventInfo(id, "Order.Created", data, DateTime.UtcNow);

        var restored = EventOutboxMapper.ToEventInfo(EventOutboxMapper.ToEntity(info));

        Assert.Equal(id, restored.Id);
        Assert.Equal("Order.Created", restored.EventName);
        Assert.Equal(data, restored.EventData);
    }

    /// <summary>
    /// 协调世界时往返后时刻不变且类型为协调世界时
    /// </summary>
    [Fact]
    public void 协调世界时往返后时刻不变()
    {
        var created = new DateTime(2026, 9, 21, 10, 30, 0, DateTimeKind.Utc);
        var info = new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1], created);

        var restored = EventOutboxMapper.ToEventInfo(EventOutboxMapper.ToEntity(info));

        Assert.Equal(DateTimeKind.Utc, restored.CreatedTime.Kind);
        Assert.Equal(created, restored.CreatedTime);
    }

    /// <summary>
    /// 本地时间往返后归一为协调世界时且时刻等价
    /// </summary>
    [Fact]
    public void 本地时间往返后归一为协调世界时()
    {
        var created = new DateTime(2026, 9, 21, 10, 30, 0, DateTimeKind.Local);
        var info = new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1], created);

        var restored = EventOutboxMapper.ToEventInfo(EventOutboxMapper.ToEntity(info));

        Assert.Equal(DateTimeKind.Utc, restored.CreatedTime.Kind);
        Assert.Equal(created.ToUniversalTime(), restored.CreatedTime);
    }

    /// <summary>
    /// 关联标识往返保真
    /// </summary>
    [Fact]
    public void 关联标识往返保真()
    {
        var info = new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1], DateTime.UtcNow);
        info.SetCorrelationId("corr-123");

        var restored = EventOutboxMapper.ToEventInfo(EventOutboxMapper.ToEntity(info));

        Assert.Equal("corr-123", restored.GetCorrelationId());
    }

    /// <summary>
    /// 没有扩展属性时往返不抛异常
    /// </summary>
    [Fact]
    public void 没有扩展属性时往返不抛异常()
    {
        var info = new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1], DateTime.UtcNow);

        var restored = EventOutboxMapper.ToEventInfo(EventOutboxMapper.ToEntity(info));

        Assert.Null(restored.GetCorrelationId());
    }

    /// <summary>
    /// 入库时状态为待发送且未被领取
    /// </summary>
    [Fact]
    public void 入库时状态为待发送且未被领取()
    {
        var info = new OutgoingEventInfo(Guid.NewGuid(), "Order.Created", [1], DateTime.UtcNow);

        var entity = EventOutboxMapper.ToEntity(info);

        Assert.Equal(SysEventOutbox.StatusPending, entity.Status);
        Assert.Null(entity.ClaimToken);
        Assert.Null(entity.ClaimTime);
    }
}
