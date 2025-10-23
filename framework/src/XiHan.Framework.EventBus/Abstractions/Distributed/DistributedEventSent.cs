#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedEventSent
// Guid:2083e09e-0145-427b-89d3-657f772d82a5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 5:04:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 分布式事件发送信息
/// </summary>
public class DistributedEventSent
{
    /// <summary>
    /// 事件来源
    /// </summary>
    public DistributedEventSource Source { get; set; }

    /// <summary>
    /// 事件名称
    /// </summary>
    public string EventName { get; set; } = default!;

    /// <summary>
    /// 事件数据
    /// </summary>
    public object EventData { get; set; } = default!;
}
