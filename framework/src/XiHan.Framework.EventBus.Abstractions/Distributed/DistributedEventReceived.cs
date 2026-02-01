#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedEventReceived
// Guid:9c668681-a747-4b67-9094-c3a8aa2329b4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 05:06:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 分布式事件接收实体
/// </summary>
public class DistributedEventReceived
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
