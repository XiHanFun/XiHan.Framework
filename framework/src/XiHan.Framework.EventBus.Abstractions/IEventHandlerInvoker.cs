// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
