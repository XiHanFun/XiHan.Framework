// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
