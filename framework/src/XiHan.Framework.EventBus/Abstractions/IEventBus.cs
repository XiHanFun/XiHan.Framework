#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IEventBus
// Guid:4e4a6c35-fce9-4351-8fdb-fcfaedf076dd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 6:50:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus.Abstractions;

/// <summary>
/// IEventBus
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// 触发事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件相关数据</param>
    /// <param name="onUnitOfWorkComplete">为 true 表示在当前工作单元完成后再发布事件 如果有工作单元</param>
    /// <returns>表示异步操作的任务</returns>
    Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true)
        where TEvent : class;

    /// <summary>
    /// 触发事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件相关数据</param>
    /// <param name="onUnitOfWorkComplete">为 true 表示在当前工作单元完成后再发布事件 如果有工作单元</param>
    /// <returns>表示异步操作的任务</returns>
    Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true);

    /// <summary>
    /// 注册事件监听器
    /// 每次事件发生时都会调用指定的处理方法
    /// </summary>
    /// <param name="action">处理事件的方法</param>
    /// <typeparam name="TEvent">事件类型</typeparam>
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> action)
        where TEvent : class;

    /// <summary>
    /// 注册事件监听器
    /// 每次事件发生时都会创建一个新的 THandler 实例来处理事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <typeparam name="THandler">事件处理器类型</typeparam>
    IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IEventHandler, new();

    /// <summary>
    /// 注册事件监听器
    /// 所有事件发生时都使用同一个处理器实例
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">用于处理事件的对象</param>
    IDisposable Subscribe(Type eventType, IEventHandler handler);

    /// <summary>
    /// 注册事件监听器
    /// 使用工厂创建和释放事件处理器
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">用于创建和释放事件处理器的工厂</param>
    IDisposable Subscribe<TEvent>(IEventHandlerFactory factory)
        where TEvent : class;

    /// <summary>
    /// 注册事件监听器
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">用于创建和释放事件处理器的工厂</param>
    IDisposable Subscribe(Type eventType, IEventHandlerFactory factory);

    /// <summary>
    /// 注销事件监听器
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">要注销的方法</param>
    void Unsubscribe<TEvent>(Func<TEvent, Task> action)
        where TEvent : class;

    /// <summary>
    /// 注销事件监听器
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">之前注册的处理器对象</param>
    void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler)
        where TEvent : class;

    /// <summary>
    /// 注销事件监听器
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">之前注册的处理器对象</param>
    void Unsubscribe(Type eventType, IEventHandler handler);

    /// <summary>
    /// 注销事件监听器
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">之前注册的工厂对象</param>
    void Unsubscribe<TEvent>(IEventHandlerFactory factory)
        where TEvent : class;

    /// <summary>
    /// 注销事件监听器
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">之前注册的工厂对象</param>
    void Unsubscribe(Type eventType, IEventHandlerFactory factory);

    /// <summary>
    /// 注销指定事件类型的所有事件处理器
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    void UnsubscribeAll<TEvent>()
        where TEvent : class;

    /// <summary>
    /// 注销指定事件类型的所有事件处理器
    /// </summary>
    /// <param name="eventType">事件类型</param>
    void UnsubscribeAll(Type eventType);
}
