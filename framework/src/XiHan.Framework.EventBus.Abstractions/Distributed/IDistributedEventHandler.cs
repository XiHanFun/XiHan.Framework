#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IDistributedEventHandler
// Guid:87ee9427-3404-479d-b61b-e34841844b25
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 7:04:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Abstractions.Distributed;

/// <summary>
/// 分布式事件处理器接口
/// 定义分布式事件处理器的契约，用于处理跨服务、跨进程的事件
/// </summary>
/// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
public interface IDistributedEventHandler<in TEvent> : IEventHandler
{
    /// <summary>
    /// 异步处理分布式事件
    /// 事件处理器通过实现此方法来处理来自分布式事件总线的事件
    /// </summary>
    /// <param name="eventData">事件数据对象</param>
    /// <returns>表示异步操作的任务</returns>
    /// <exception cref="ArgumentNullException">当 eventData 为 null 时</exception>
    Task HandleEventAsync(TEvent eventData);
}
