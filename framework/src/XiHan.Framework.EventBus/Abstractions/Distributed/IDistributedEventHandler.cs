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
/// </summary>
/// <typeparam name="TEvent"></typeparam>
public interface IDistributedEventHandler<in TEvent> : IEventHandler
{
    /// <summary>
    /// 事件处理器通过实现此方法来处理事件
    /// </summary>
    /// <param name="eventData">事件数据</param>
    Task HandleEventAsync(TEvent eventData);
}
