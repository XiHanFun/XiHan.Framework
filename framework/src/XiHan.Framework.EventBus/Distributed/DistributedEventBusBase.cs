#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DistributedEventBusBase
// Guid:e45e6722-1a00-4483-bdb7-27a86c84e71b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/3 1:48:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// DistributedEventBusBase
/// </summary>
public abstract class DistributedEventBusBase : EventBusBase, IDistributedEventBus, ISupportsEventBoxes
{
    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true) where TEvent : class
    {
        throw new NotImplementedException();
    }

    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
    {
        throw new NotImplementedException();
    }

    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true) where TEvent : class
    {
        throw new NotImplementedException();
    }

    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
    {
        throw new NotImplementedException();
    }

    public IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler) where TEvent : class
    {
        throw new NotImplementedException();
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        throw new NotImplementedException();
    }

    public IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IEventHandler, new()
    {
        throw new NotImplementedException();
    }

    public IDisposable Subscribe(Type eventType, IEventHandler handler)
    {
        throw new NotImplementedException();
    }

    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        throw new NotImplementedException();
    }

    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        throw new NotImplementedException();
    }

    public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        throw new NotImplementedException();
    }

    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        throw new NotImplementedException();
    }

    public void Unsubscribe(Type eventType, IEventHandler handler)
    {
        throw new NotImplementedException();
    }

    public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        throw new NotImplementedException();
    }

    public void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        throw new NotImplementedException();
    }

    public void UnsubscribeAll<TEvent>() where TEvent : class
    {
        throw new NotImplementedException();
    }

    public void UnsubscribeAll(Type eventType)
    {
        throw new NotImplementedException();
    }
}
