// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.Abstractions.Local;

/// <summary>
/// 本地事件总线接口
/// </summary>
public interface ILocalEventBus : IEventBus
{
    /// <summary>
    /// 订阅事件
    /// 所有事件发生时都使用相同的(给定的)事件处理器实例
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">用于处理事件的对象</param>
    IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler)
        where TEvent : class;

    /// <summary>
    /// 获取指定事件类型对应的事件处理器工厂列表
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件类型及其对应的事件处理器工厂列表</returns>
    List<EventTypeWithEventHandlerFactories> GetEventHandlerFactories(Type eventType);
}
