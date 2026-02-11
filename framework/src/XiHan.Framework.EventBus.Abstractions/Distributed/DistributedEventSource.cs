#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedEventSource
// Guid:0e930458-9c05-49e9-bffa-a21cff8ee05e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 05:05:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
