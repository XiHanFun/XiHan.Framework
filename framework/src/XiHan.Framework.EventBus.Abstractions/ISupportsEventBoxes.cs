#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISupportsEventBoxes
// Guid:6d5ae3e8-8879-4abc-bf4c-0d475e410ded
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/03 01:53:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 支持事件盒接口
/// 用于声明某个对象(通常是实体)支持事件盒(Event Boxes)机制，
/// 实现可靠的事件发布与处理能力(支持出站事件收件箱和入站事件 Inbox)
/// </summary>
public interface ISupportsEventBoxes
{
    /// <summary>
    /// 从出站事件盒中发布一个事件
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <param name="outboxConfig">出站事件盒配置</param>
    /// <returns>表示异步操作的任务</returns>
    Task PublishFromOutboxAsync(OutgoingEventInfo outgoingEvent, OutboxConfig outboxConfig);

    /// <summary>
    /// 从出站事件盒中批量发布多个事件
    /// </summary>
    /// <param name="outgoingEvents">出站事件集合</param>
    /// <param name="outboxConfig">出站事件盒配置</param>
    /// <returns>表示异步操作的任务</returns>
    Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig);

    /// <summary>
    /// 处理从入站事件盒接收到的事件
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    /// <param name="inboxConfig">入站事件盒配置</param>
    /// <returns>表示异步操作的任务</returns>
    Task ProcessFromInboxAsync(IncomingEventInfo incomingEvent, InboxConfig inboxConfig);
}
