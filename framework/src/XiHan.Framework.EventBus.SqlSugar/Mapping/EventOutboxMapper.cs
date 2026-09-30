// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;

namespace XiHan.Framework.EventBus.SqlSugar.Mapping;

/// <summary>
/// 发件箱契约与实体的双向映射
/// </summary>
public static class EventOutboxMapper
{
    /// <summary>
    /// 把出站事件信息转换为实体
    /// </summary>
    /// <param name="info">出站事件信息</param>
    /// <returns>发件箱实体</returns>
    public static SysEventOutbox ToEntity(OutgoingEventInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return new SysEventOutbox(info.Id)
        {
            EventName = info.EventName,
            EventData = info.EventData,
            CreatedTime = ToOffset(info.CreatedTime),
            ExtraProperties = info.ExtraProperties.Count == 0
                ? null
                : JsonSerializer.Serialize(info.ExtraProperties),
            Status = SysEventOutbox.StatusPending,
            ClaimToken = null,
            ClaimTime = null
        };
    }

    /// <summary>
    /// 把实体转换为出站事件信息
    /// </summary>
    /// <param name="entity">发件箱实体</param>
    /// <returns>出站事件信息</returns>
    public static OutgoingEventInfo ToEventInfo(SysEventOutbox entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var info = new OutgoingEventInfo(
            entity.BasicId,
            entity.EventName,
            entity.EventData,
            entity.CreatedTime.UtcDateTime);

        if (string.IsNullOrWhiteSpace(entity.ExtraProperties))
        {
            return info;
        }

        var properties = JsonSerializer.Deserialize<Dictionary<string, object?>>(entity.ExtraProperties);
        if (properties is null)
        {
            return info;
        }

        foreach (var pair in properties)
        {
            info.ExtraProperties[pair.Key] = pair.Value;
        }

        return info;
    }

    /// <summary>
    /// 按时间类型转换为带偏移的时间，未指定类型的按协调世界时处理
    /// </summary>
    /// <param name="value">时间</param>
    /// <returns>带偏移的时间</returns>
    private static DateTimeOffset ToOffset(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(value),
            _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero)
        };
    }
}
