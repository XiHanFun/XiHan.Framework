// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.SqlSugar.Entities;

namespace XiHan.Framework.EventBus.SqlSugar.Mapping;

/// <summary>
/// 收件箱契约与实体的双向映射
/// </summary>
public static class EventInboxMapper
{
    /// <summary>
    /// 无消息标识时去重键的前缀
    /// </summary>
    public const string NoMessageIdKeyPrefix = "no-message-id:";

    /// <summary>
    /// 把入站事件信息转换为实体
    /// </summary>
    /// <remarks>
    /// 有消息标识时去重键等于消息标识；消息标识为空白时存为空值，去重键由前缀与事件标识组成。
    /// </remarks>
    /// <param name="info">入站事件信息</param>
    /// <returns>收件箱实体</returns>
    public static SysEventInbox ToEntity(IncomingEventInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var messageId = string.IsNullOrWhiteSpace(info.MessageId) ? null : info.MessageId;

        return new SysEventInbox(info.Id)
        {
            MessageId = messageId,
            DedupKey = messageId ?? $"{NoMessageIdKeyPrefix}{info.Id:N}",
            EventName = info.EventName,
            EventData = info.EventData,
            CreatedTime = ToOffset(info.CreatedTime),
            ExtraProperties = info.ExtraProperties.Count == 0
                ? null
                : JsonSerializer.Serialize(info.ExtraProperties),
            Status = SysEventInbox.StatusPending,
            RetryCount = 0,
            NextRetryTime = null,
            ClaimToken = null,
            ClaimTime = null,
            HandledTime = null
        };
    }

    /// <summary>
    /// 把实体转换为入站事件信息
    /// </summary>
    /// <remarks>
    /// 消息标识为空值时还原为空字符串。
    /// </remarks>
    /// <param name="entity">收件箱实体</param>
    /// <returns>入站事件信息</returns>
    public static IncomingEventInfo ToEventInfo(SysEventInbox entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var info = new IncomingEventInfo(
            entity.BasicId,
            entity.MessageId ?? string.Empty,
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
    internal static DateTimeOffset ToOffset(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(value),
            _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero)
        };
    }
}
