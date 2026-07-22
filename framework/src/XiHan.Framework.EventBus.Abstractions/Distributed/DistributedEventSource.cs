// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 分布式事件源
/// </summary>
public enum DistributedEventSource
{
    /// <summary>
    /// 直接发送
    /// </summary>
    Direct,

    /// <summary>
    /// 收件箱
    /// </summary>
    Inbox,

    /// <summary>
    /// 发件箱
    /// </summary>
    Outbox
}
