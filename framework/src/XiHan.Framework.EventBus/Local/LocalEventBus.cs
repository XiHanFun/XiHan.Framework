#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalEventBus
// Guid:5962f337-006b-4887-9f88-7b706156ef2f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/3 1:46:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus.Local;

/// <summary>
/// LocalEventBus
/// </summary>
[ExposeServices(typeof(ILocalEventBus), typeof(LocalEventBus))]
public class LocalEventBus : EventBusBase, ILocalEventBus, ISingletonDependency
{
    public List<EventTypeWithEventHandlerFactories> GetEventHandlerFactories(Type eventType)
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

    public IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
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
