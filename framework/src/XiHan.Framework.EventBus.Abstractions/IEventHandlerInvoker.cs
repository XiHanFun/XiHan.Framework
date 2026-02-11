#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEventHandlerInvoker
// Guid:afe6ac75-ab71-41c6-9753-640ca973813e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 04:52:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 事件处理器调用接口
/// </summary>
public interface IEventHandlerInvoker
{
    /// <summary>
    /// 调用事件处理器
    /// </summary>
    /// <param name="eventHandler">事件处理器实例</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="eventType">事件类型</param>
    /// <returns>表示异步操作的任务</returns>
    Task InvokeAsync(IEventHandler eventHandler, object eventData, Type eventType);
}
