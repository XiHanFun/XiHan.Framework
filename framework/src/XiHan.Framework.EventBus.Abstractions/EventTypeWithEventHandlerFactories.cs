// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// 事件类型及其对应的事件处理器工厂列表
/// </summary>
public class EventTypeWithEventHandlerFactories
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventHandlerFactories">事件处理器工厂列表</param>
    public EventTypeWithEventHandlerFactories(Type eventType, List<IEventHandlerFactory> eventHandlerFactories)
    {
        EventType = eventType;
        EventHandlerFactories = eventHandlerFactories;
    }

    /// <summary>
    /// 事件类型
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// 事件处理器工厂列表
    /// </summary>
    public List<IEventHandlerFactory> EventHandlerFactories { get; }
}
