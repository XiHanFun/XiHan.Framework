// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus;

/// <summary>
/// 事件处理器调用缓存项
/// </summary>
public class EventHandlerInvokerCacheItem
{
    /// <summary>
    /// 本地事件处理器执行器
    /// </summary>
    public IEventHandlerMethodExecutor? Local { get; set; }

    /// <summary>
    /// 分布式事件处理器执行器
    /// </summary>
    public IEventHandlerMethodExecutor? Distributed { get; set; }
}
