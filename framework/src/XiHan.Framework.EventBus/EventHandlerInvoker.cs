// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 事件处理器调用器
/// </summary>
public class EventHandlerInvoker : IEventHandlerInvoker, ISingletonDependency
{
    /// <summary>
    /// 缓存
    /// </summary>
    private readonly ConcurrentDictionary<string, EventHandlerInvokerCacheItem> _cache;

    /// <summary>
    /// 构造函数
    /// </summary>
    public EventHandlerInvoker()
    {
        _cache = new ConcurrentDictionary<string, EventHandlerInvokerCacheItem>();
    }

    /// <summary>
    /// 调用事件处理器
    /// </summary>
    /// <param name="eventHandler"></param>
    /// <param name="eventData"></param>
    /// <param name="eventType"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public async Task InvokeAsync(IEventHandler eventHandler, object eventData, Type eventType)
    {
        var cacheItem = _cache.GetOrAdd($"{eventHandler.GetType().FullName}-{eventType.FullName}", _ =>
        {
            var item = new EventHandlerInvokerCacheItem();

            if (typeof(ILocalEventHandler<>).MakeGenericType(eventType).IsInstanceOfType(eventHandler))
            {
                item.Local = (IEventHandlerMethodExecutor?)Activator.CreateInstance(typeof(LocalEventHandlerMethodExecutor<>).MakeGenericType(eventType));
            }

            if (typeof(IDistributedEventHandler<>).MakeGenericType(eventType).IsInstanceOfType(eventHandler))
            {
                item.Distributed = (IEventHandlerMethodExecutor?)Activator.CreateInstance(typeof(DistributedEventHandlerMethodExecutor<>).MakeGenericType(eventType));
            }

            return item;
        });

        if (cacheItem.Local != null)
        {
            await cacheItem.Local.ExecutorAsync(eventHandler, eventData);
        }

        if (cacheItem.Distributed != null)
        {
            await cacheItem.Distributed.ExecutorAsync(eventHandler, eventData);
        }

        if (cacheItem.Local == null && cacheItem.Distributed == null)
        {
            throw new XiHanException("该对象实例不是事件处理程序。对象类型：" + eventHandler.GetType().AssemblyQualifiedName);
        }
    }
}
