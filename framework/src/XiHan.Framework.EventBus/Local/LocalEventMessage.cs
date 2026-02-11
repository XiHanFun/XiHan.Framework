#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalEventMessage
// Guid:6d3fa130-62fa-4b4e-9bdb-434bd4c3dd83
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 05:20:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
