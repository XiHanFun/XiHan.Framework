// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Local;

/// <summary>
/// 本地事件消息
/// </summary>
public class LocalEventMessage
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="messageId"></param>
    /// <param name="eventData"></param>
    /// <param name="eventType"></param>
    public LocalEventMessage(Guid messageId, object eventData, Type eventType)
    {
        MessageId = messageId;
        EventData = eventData;
        EventType = eventType;
    }

    /// <summary>
    /// 消息唯一标识
    /// </summary>
    public Guid MessageId { get; }

    /// <summary>
    /// 事件数据
    /// </summary>
    public object EventData { get; }

    /// <summary>
    /// 事件类型
    /// </summary>
    public Type EventType { get; }
}
