#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanDistributedEventBusOptions
// Guid:1239bbe5-2511-4f92-baf7-a24de026b93b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 04:47:15
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Collections;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 曦寒分布式事件总线配置选项
/// </summary>
/// <remarks>
/// 用于配置分布式事件总线的相关参数，包括事件处理器、发件箱和收件箱配置
/// </remarks>
public class XiHanDistributedEventBusOptions
{
    /// <summary>
    /// 默认构造函数
    /// </summary>
    public XiHanDistributedEventBusOptions()
    {
        Handlers = new TypeList<IEventHandler>();
        Outboxes = [];
        Inboxes = [];
    }

    /// <summary>
    /// 事件处理器类型列表
    /// </summary>
    /// <remarks>
    /// 存储所有注册的事件处理器类型，用于处理分布式事件
    /// </remarks>
    public ITypeList<IEventHandler> Handlers { get; }

    /// <summary>
    /// 发件箱配置字典
    /// </summary>
    /// <remarks>
    /// 发件箱模式配置，确保事件发布的可靠性和一致性，防止事件丢失
    /// </remarks>
    public OutboxConfigDictionary Outboxes { get; }

    /// <summary>
    /// 收件箱配置字典
    /// </summary>
    /// <remarks>
    /// 收件箱模式配置，确保事件消费的可靠性和幂等性，防止重复处理
    /// </remarks>
    public InboxConfigDictionary Inboxes { get; }
}
