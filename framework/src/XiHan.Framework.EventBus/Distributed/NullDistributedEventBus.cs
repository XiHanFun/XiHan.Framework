#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullDistributedEventBus
// Guid:dfec0a93-b7dd-4ccb-88fc-90cfb4efbaf7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 5:34:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.Utils.Threading;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 分布式事件总线空实现
/// </summary>
public sealed class NullDistributedEventBus : IDistributedEventBus
{
    /// <summary>
    /// 构造函数
    /// </summary>
    private NullDistributedEventBus()
    {
    }

    /// <summary>
    /// 单例实例
    /// </summary>
    public static NullDistributedEventBus Instance { get; } = new NullDistributedEventBus();

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理函数</param>
    /// <returns>空订阅</returns>
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理函数</param>
    /// <returns>空订阅</returns>
    public IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler) where TEvent : class
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <typeparam name="THandler">事件处理函数</typeparam>
    /// <returns>空订阅</returns>
    public IDisposable Subscribe<TEvent, THandler>() where TEvent : class where THandler : IEventHandler, new()
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理函数</param>
    /// <returns>空订阅</returns>
    public IDisposable Subscribe(Type eventType, IEventHandler handler)
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理函数工厂</param>
    /// <returns>空订阅</returns>
    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理函数工厂</param>
    /// <returns></returns>
    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        return NullDisposable.Instance;
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理函数</param>
    public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理函数</param>
    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理函数</param>
    public void Unsubscribe(Type eventType, IEventHandler handler)
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="factory">事件处理函数工厂</param>
    public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理函数工厂</param>
    public void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    public void UnsubscribeAll<TEvent>() where TEvent : class
    {
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public void UnsubscribeAll(Type eventType)
    {
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在单元工作完成后发布</param>
    /// <returns></returns>
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
    /// <returns></returns>
    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在单元工作完成后发布</param>
    /// <param name="useOutbox">是否使用收件箱模式进行事件持久化与可靠投递</param>
    /// <returns></returns>
    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true) where TEvent : class
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在单元工作完成后发布</param>
    /// <param name="useOutbox">是否使用收件箱模式进行事件持久化与可靠投递</param>
    /// <returns></returns>
    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
    {
        return Task.CompletedTask;
    }
}
