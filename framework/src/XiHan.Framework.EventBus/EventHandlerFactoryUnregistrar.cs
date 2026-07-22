// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.EventBus.Abstractions;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 事件处理器工厂注销器
/// </summary>
public class EventHandlerFactoryUnregistrar : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly Type _eventType;
    private readonly IEventHandlerFactory _factory;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventBus">事件总线</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public EventHandlerFactoryUnregistrar(IEventBus eventBus, Type eventType, IEventHandlerFactory factory)
    {
        _eventBus = eventBus;
        _eventType = eventType;
        _factory = factory;
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        _eventBus.Unsubscribe(_eventType, _factory);
    }
}
