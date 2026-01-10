#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EventBusBase
// Guid:f866d4b5-e2ce-49b0-94c3-47943e8869bf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/3 1:47:43
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.CompilerServices;
using XiHan.Framework.Core.Collections;
using XiHan.Framework.Core.Extensions.Exceptions;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;

namespace XiHan.Framework.EventBus;

/// <summary>
/// 事件总线基类
/// 提供事件总线的基础实现，包括事件订阅、取消订阅、发布等核心功能
/// 支持多种订阅方式、工作单元集成和多租户场景
/// </summary>
/// <remarks>
/// 此抽象基类为具体的事件总线实现提供了统一的基础功能，
/// 子类需要实现具体的事件存储和处理机制
/// </remarks>
public abstract class EventBusBase : IEventBus
{
    /// <summary>
    /// 初始化 EventBusBase 类的新实例
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂，用于创建依赖注入作用域</param>
    /// <param name="currentTenant">当前租户访问器</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="eventHandlerInvoker">事件处理器调用器</param>
    /// <exception cref="ArgumentNullException">当任何参数为 null 时</exception>
    protected EventBusBase(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IEventHandlerInvoker eventHandlerInvoker)
    {
        ServiceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        CurrentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
        UnitOfWorkManager = unitOfWorkManager ?? throw new ArgumentNullException(nameof(unitOfWorkManager));
        EventHandlerInvoker = eventHandlerInvoker ?? throw new ArgumentNullException(nameof(eventHandlerInvoker));
    }

    /// <summary>
    /// 获取服务作用域工厂
    /// 用于创建独立的依赖注入作用域
    /// </summary>
    /// <value>服务作用域工厂实例</value>
    protected IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 获取当前租户访问器
    /// 用于在多租户环境中管理租户上下文
    /// </summary>
    /// <value>当前租户访问器实例</value>
    protected ICurrentTenant CurrentTenant { get; }

    /// <summary>
    /// 获取工作单元管理器
    /// 用于管理事务和数据一致性
    /// </summary>
    /// <value>工作单元管理器实例</value>
    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    /// <summary>
    /// 获取事件处理器调用器
    /// 用于调用具体的事件处理器方法
    /// </summary>
    /// <value>事件处理器调用器实例</value>
    protected IEventHandlerInvoker EventHandlerInvoker { get; }

    /// <summary>
    /// 使用委托方法订阅事件
    /// 提供最简单的事件订阅方式，适合简单的事件处理逻辑
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
    /// <param name="action">处理事件的委托方法</param>
    /// <returns>用于取消订阅的释放器对象</returns>
    /// <exception cref="ArgumentNullException">当 action 为 null 时</exception>
    public virtual IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(action);
        return Subscribe(typeof(TEvent), new ActionEventHandler<TEvent>(action));
    }

    /// <summary>
    /// 使用泛型处理器类型订阅事件
    /// 每次处理事件时都会创建新的处理器实例
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
    /// <typeparam name="THandler">事件处理器类型，必须实现 IEventHandler 接口并具有无参构造函数</typeparam>
    /// <returns>用于取消订阅的释放器对象</returns>
    public virtual IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class
        where THandler : IEventHandler, new()
    {
        return Subscribe(typeof(TEvent), new TransientEventHandlerFactory<THandler>());
    }

    /// <summary>
    /// 使用处理器实例订阅事件
    /// 所有事件都将由同一个处理器实例处理
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器实例</param>
    /// <returns>用于取消订阅的释放器对象</returns>
    /// <exception cref="ArgumentNullException">当 eventType 或 handler 为 null 时</exception>
    public virtual IDisposable Subscribe(Type eventType, IEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(eventType, new SingleInstanceHandlerFactory(handler));
    }

    /// <summary>
    /// 使用工厂对象订阅事件
    /// 提供最灵活的订阅方式，可以自定义处理器的创建和生命周期管理
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>用于取消订阅的释放器对象</returns>
    /// <exception cref="ArgumentNullException">当 factory 为 null 时</exception>
    public virtual IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        return Subscribe(typeof(TEvent), factory);
    }

    /// <summary>
    /// 订阅指定类型的事件（抽象方法）
    /// 子类必须实现此方法来提供具体的订阅机制
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>用于取消订阅的释放器对象</returns>
    public abstract IDisposable Subscribe(Type eventType, IEventHandlerFactory factory);

    /// <summary>
    /// 取消委托方法的事件订阅（抽象方法）
    /// 子类必须实现此方法来提供具体的取消订阅机制
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
    /// <param name="action">要取消订阅的委托方法</param>
    public abstract void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class;

    /// <summary>
    /// 取消本地事件处理器的订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
    /// <param name="handler">要取消订阅的本地事件处理器</param>
    /// <exception cref="ArgumentNullException">当 handler 为 null 时</exception>
    public virtual void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        Unsubscribe(typeof(TEvent), handler);
    }

    /// <summary>
    /// 取消事件处理器的订阅（抽象方法）
    /// 子类必须实现此方法来提供具体的取消订阅机制
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">要取消订阅的事件处理器</param>
    public abstract void Unsubscribe(Type eventType, IEventHandler handler);

    /// <summary>
    /// 取消工厂对象的事件订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
    /// <param name="factory">要取消订阅的事件处理器工厂</param>
    /// <exception cref="ArgumentNullException">当 factory 为 null 时</exception>
    public virtual void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        Unsubscribe(typeof(TEvent), factory);
    }

    /// <summary>
    /// 取消工厂对象的事件订阅（抽象方法）
    /// 子类必须实现此方法来提供具体的取消订阅机制
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">要取消订阅的事件处理器工厂</param>
    public abstract void Unsubscribe(Type eventType, IEventHandlerFactory factory);

    /// <summary>
    /// 取消指定事件类型的所有订阅
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
    public virtual void UnsubscribeAll<TEvent>() where TEvent : class
    {
        UnsubscribeAll(typeof(TEvent));
    }

    /// <summary>
    /// 取消指定事件类型的所有订阅（抽象方法）
    /// 子类必须实现此方法来提供具体的取消订阅机制
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public abstract void UnsubscribeAll(Type eventType);

    /// <summary>
    /// 异步发布事件
    /// 根据工作单元设置决定是立即发布还是延迟到工作单元完成后发布
    /// </summary>
    /// <typeparam name="TEvent">事件类型，必须是引用类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在当前工作单元完成后再发布事件，默认为 true</param>
    /// <returns>表示异步操作的任务</returns>
    /// <exception cref="ArgumentNullException">当 eventData 为 null 时</exception>
    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(eventData);
        return PublishAsync(typeof(TEvent), eventData, onUnitOfWorkComplete);
    }

    /// <summary>
    /// 异步发布指定类型的事件
    /// 根据工作单元设置决定是立即发布还是延迟到工作单元完成后发布
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在当前工作单元完成后再发布事件，默认为 true</param>
    /// <returns>表示异步操作的任务</returns>
    /// <exception cref="ArgumentNullException">当 eventType 或 eventData 为 null 时</exception>
    public virtual async Task PublishAsync(
        Type eventType,
        object eventData,
        bool onUnitOfWorkComplete = true)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(eventData);

        if (onUnitOfWorkComplete && UnitOfWorkManager.Current != null)
        {
            AddToUnitOfWork(
                UnitOfWorkManager.Current,
                new UnitOfWorkEventRecord(eventType, eventData, EventOrderGenerator.GetNext())
            );
            return;
        }

        await PublishToEventBusAsync(eventType, eventData);
    }

    /// <summary>
    /// 异步触发事件处理器
    /// 调用所有注册的事件处理器来处理指定的事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    /// <exception cref="ArgumentNullException">当 eventType 或 eventData 为 null 时</exception>
    /// <exception cref="AggregateException">当多个事件处理器抛出异常时</exception>
    public virtual async Task TriggerHandlersAsync(Type eventType, object eventData)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(eventData);

        var exceptions = new List<Exception>();

        await TriggerHandlersAsync(eventType, eventData, exceptions);

        if (exceptions.Count != 0)
        {
            ThrowOriginalExceptions(eventType, exceptions);
        }
    }

    /// <summary>
    /// 抛出原始异常
    /// 根据异常数量决定抛出单个异常还是聚合异常
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="exceptions">异常列表</param>
    /// <exception cref="AggregateException">当存在多个异常时</exception>
    protected static void ThrowOriginalExceptions(Type eventType, List<Exception> exceptions)
    {
        if (exceptions.Count == 1)
        {
            exceptions[0].ReThrow();
        }

        throw new AggregateException("触发事件时发生多个错误：" + eventType, exceptions);
    }

    /// <summary>
    /// 发布事件到事件总线（抽象方法）
    /// 子类必须实现此方法来提供具体的事件发布机制
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    protected abstract Task PublishToEventBusAsync(Type eventType, object eventData);

    /// <summary>
    /// 将事件添加到工作单元（抽象方法）
    /// 子类必须实现此方法来提供工作单元事件延迟发布功能
    /// </summary>
    /// <param name="unitOfWork">工作单元实例</param>
    /// <param name="eventRecord">事件记录</param>
    protected abstract void AddToUnitOfWork(IUnitOfWork unitOfWork, UnitOfWorkEventRecord eventRecord);

    /// <summary>
    /// 异步触发事件处理器（受保护的方法）
    /// 内部实现，支持异常收集和收件箱配置
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="exceptions">异常收集列表</param>
    /// <param name="inboxConfig">收件箱配置，可选</param>
    /// <returns>表示异步操作的任务</returns>
    protected virtual async Task TriggerHandlersAsync(Type eventType, object eventData, List<Exception> exceptions, InboxConfig? inboxConfig = null)
    {
        await new SynchronizationContextRemover();

        foreach (var handlerFactories in GetHandlerFactories(eventType).ToList())
        {
            foreach (var handlerFactory in handlerFactories.EventHandlerFactories.ToList())
            {
                await TriggerHandlerAsync(handlerFactory, handlerFactories.EventType, eventData, exceptions, inboxConfig);
            }
        }

        if (eventType.GetTypeInfo().IsGenericType &&
            eventType.GetGenericArguments().Length == 1 &&
            typeof(IEventDataWithInheritableGenericArgument).IsAssignableFrom(eventType))
        {
            var genericArg = eventType.GetGenericArguments()[0];
            var baseArg = genericArg.GetTypeInfo().BaseType;
            if (baseArg != null)
            {
                var baseEventType = eventType.GetGenericTypeDefinition().MakeGenericType(baseArg);
                var constructorArgs = ((IEventDataWithInheritableGenericArgument)eventData).GetConstructorArgs();
                var baseEventData = Activator.CreateInstance(baseEventType, constructorArgs)!;
                await PublishToEventBusAsync(baseEventType, baseEventData);
            }
        }
    }

    /// <summary>
    /// 订阅处理器集合
    /// 批量注册事件处理器，通过反射自动发现和注册
    /// </summary>
    /// <param name="handlers">事件处理器类型列表</param>
    /// <exception cref="ArgumentNullException">当 handlers 为 null 时</exception>
    protected virtual void SubscribeHandlers(ITypeList<IEventHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        foreach (var handler in handlers)
        {
            var interfaces = handler.GetInterfaces();
            foreach (var @interface in interfaces)
            {
                if (!typeof(IEventHandler).GetTypeInfo().IsAssignableFrom(@interface))
                {
                    continue;
                }

                var genericArgs = @interface.GetGenericArguments();
                if (genericArgs.Length == 1)
                {
                    Subscribe(genericArgs[0], new IocEventHandlerFactory(ServiceScopeFactory, handler));
                }
            }
        }
    }

    /// <summary>
    /// 获取事件处理器工厂集合（抽象方法）
    /// 子类必须实现此方法来提供事件类型对应的处理器工厂
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件处理器工厂集合</returns>
    protected abstract IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType);

    /// <summary>
    /// 异步触发单个事件处理器
    /// 处理单个事件处理器的调用，包括租户上下文管理和异常处理
    /// </summary>
    /// <param name="asyncHandlerFactory">事件处理器工厂</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="exceptions">异常收集列表</param>
    /// <param name="inboxConfig">收件箱配置，可选</param>
    /// <returns>表示异步操作的任务</returns>
    protected virtual async Task TriggerHandlerAsync(IEventHandlerFactory asyncHandlerFactory, Type eventType,
        object eventData, List<Exception> exceptions, InboxConfig? inboxConfig = null)
    {
        using var eventHandlerWrapper = asyncHandlerFactory.GetHandler();
        try
        {
            var handlerType = eventHandlerWrapper.EventHandler.GetType();

            if (inboxConfig?.HandlerSelector != null &&
                !inboxConfig.HandlerSelector(handlerType))
            {
                return;
            }

            using (CurrentTenant.Change(GetEventDataTenantId(eventData)))
            {
                await InvokeEventHandlerAsync(eventHandlerWrapper.EventHandler, eventData, eventType);
            }
        }
        catch (TargetInvocationException ex)
        {
            exceptions.Add(ex.InnerException!);
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }
    }

    /// <summary>
    /// 异步调用事件处理器
    /// 委托给事件处理器调用器来执行具体的处理逻辑
    /// </summary>
    /// <param name="eventHandler">事件处理器实例</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="eventType">事件类型</param>
    /// <returns>表示异步操作的任务</returns>
    protected virtual Task InvokeEventHandlerAsync(IEventHandler eventHandler, object eventData, Type eventType)
    {
        return EventHandlerInvoker.InvokeAsync(eventHandler, eventData, eventType);
    }

    /// <summary>
    /// 获取事件数据的租户唯一标识
    /// 从事件数据中提取租户信息，支持多租户事件处理
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>租户唯一标识，如果不是多租户事件则返回当前租户唯一标识</returns>
    protected virtual Guid? GetEventDataTenantId(object eventData)
    {
        return eventData switch
        {
            IMultiTenant multiTenantEventData => multiTenantEventData.TenantId,
            IEventDataMayHaveTenantId eventDataMayHaveTenantId when eventDataMayHaveTenantId.IsMultiTenant(out var tenantId) => tenantId,
            _ => CurrentTenant.Id
        };
    }

    /// <summary>
    /// 同步上下文移除器
    /// 用于在异步操作中移除同步上下文，避免死锁问题
    /// </summary>
    /// <remarks>
    /// 参考来源：https://blogs.msdn.microsoft.com/benwilli/2017/02/09/an-alternative-to-configureawaitfalse-everywhere/
    /// </remarks>
    protected struct SynchronizationContextRemover : INotifyCompletion
    {
        /// <summary>
        /// 获取操作是否已完成
        /// 当当前同步上下文为 null 时表示已完成
        /// </summary>
        /// <value>如果当前同步上下文为 null 则返回 true，否则返回 false</value>
        public bool IsCompleted => SynchronizationContext.Current == null;

        /// <summary>
        /// 当操作完成时执行延续操作
        /// 临时移除同步上下文，执行延续操作，然后恢复同步上下文
        /// </summary>
        /// <param name="continuation">要执行的延续操作</param>
        public void OnCompleted(Action continuation)
        {
            var prevContext = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                continuation();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        /// <summary>
        /// 获取等待器
        /// 返回当前结构实例作为等待器
        /// </summary>
        /// <returns>当前 SynchronizationContextRemover 实例</returns>
        public SynchronizationContextRemover GetAwaiter()
        {
            return this;
        }

        /// <summary>
        /// 获取结果
        /// 此方法不返回任何值，仅用于满足等待器接口要求
        /// </summary>
        public void GetResult()
        {
        }
    }
}
