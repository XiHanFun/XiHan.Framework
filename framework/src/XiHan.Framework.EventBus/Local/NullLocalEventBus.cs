#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullLocalEventBus
// Guid:e0451b26-0495-4f41-b8a5-861f09ab4ef5
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 5:32:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.EventBus.Local;

/// <summary>
/// 空本地事件总线
/// </summary>
public sealed class NullLocalEventBus : ILocalEventBus
{
    /// <summary>
    /// 构造函数
    /// </summary>
    private NullLocalEventBus()
    {
    }

    /// <summary>
    /// 实例
    /// </summary>
    public static NullLocalEventBus Instance { get; } = new NullLocalEventBus();

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理动作</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 获取事件处理器工厂列表
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件处理器工厂列表</returns>
    public List<EventTypeWithEventHandlerFactories> GetEventHandlerFactories(Type eventType)
    {
        return [];
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <typeparam name="THandler">事件处理器类型</typeparam>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new()
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public IDisposable Subscribe(Type eventType, IEventHandler handler)
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理动作</param>
    public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public void Unsubscribe(Type eventType, IEventHandler handler)
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public void UnsubscribeAll<TEvent>() where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public void UnsubscribeAll(Type eventType)
    {
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在单元工作完成后发布</param>
    /// <returns>异步任务</returns>
    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true) where TEvent : class
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在单元工作完成后发布</param>
    /// <returns>异步任务</returns>
    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
    {
        return Task.CompletedTask;
    }
}
