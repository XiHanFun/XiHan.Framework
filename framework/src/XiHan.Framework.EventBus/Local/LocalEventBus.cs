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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Threading;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.EventBus.Local;

/// <summary>
/// 本地事件总线
/// 提供本地进程内的事件发布订阅功能,支持事件的注册、取消注册、发布等操作
/// </summary>
[ExposeServices(typeof(ILocalEventBus), typeof(LocalEventBus))]
public class LocalEventBus : EventBusBase, ILocalEventBus, ISingletonDependency
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">本地事件总线配置选项</param>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="currentTenant">当前租户信息</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="eventHandlerInvoker">事件处理器调用器</param>
    public LocalEventBus(
            IOptions<XiHanLocalEventBusOptions> options,
            IServiceScopeFactory serviceScopeFactory,
            ICurrentTenant currentTenant,
            IUnitOfWorkManager unitOfWorkManager,
            IEventHandlerInvoker eventHandlerInvoker)
            : base(serviceScopeFactory, currentTenant, unitOfWorkManager, eventHandlerInvoker)
    {
        Options = options.Value;
        Logger = NullLogger<LocalEventBus>.Instance;

        HandlerFactories = new ConcurrentDictionary<Type, List<IEventHandlerFactory>>();
        SubscribeHandlers(Options.Handlers);
    }

    /// <summary>
    /// 日志记录器
    /// </summary>
    public ILogger<LocalEventBus> Logger { get; set; }

    /// <summary>
    /// 本地事件总线配置选项
    /// </summary>
    protected XiHanLocalEventBusOptions Options { get; }

    /// <summary>
    /// 事件处理器工厂字典集合
    /// </summary>
    protected ConcurrentDictionary<Type, List<IEventHandlerFactory>> HandlerFactories { get; }

    /// <summary>
    /// 订阅本地事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public virtual IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class
    {
        return Subscribe(typeof(TEvent), handler);
    }

    /// <summary>
    /// 订阅指定类型的事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>返回一个可释放的对象，用于取消订阅</returns>
    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        GetOrCreateHandlerFactories(eventType)
            .Locking(factories =>
            {
                if (!factory.IsInFactories(factories))
                {
                    factories.Add(factory);
                }
            }
            );

        return new EventHandlerFactoryUnregistrar(this, eventType, factory);
    }

    /// <summary>
    /// 取消订阅指定的事件处理动作
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">要取消订阅的事件处理动作</param>
    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action)
    {
        Guard.NotNull(action, nameof(action));

        GetOrCreateHandlerFactories(typeof(TEvent))
            .Locking(factories =>
            {
                factories.RemoveAll(
                    factory =>
                    {
                        if (factory is not SingleInstanceHandlerFactory singleInstanceFactory)
                        {
                            return false;
                        }

                        if (singleInstanceFactory.HandlerInstance is not ActionEventHandler<TEvent> actionHandler)
                        {
                            return false;
                        }

                        return actionHandler.Action == action;
                    });
            });
    }

    /// <summary>
    /// 取消事件处理器的订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">要取消订阅的事件处理器</param>
    public override void Unsubscribe(Type eventType, IEventHandler handler)
    {
        GetOrCreateHandlerFactories(eventType)
            .Locking(factories =>
            {
                factories.RemoveAll(
                    factory =>
                        factory is SingleInstanceHandlerFactory &&
                        ((factory as SingleInstanceHandlerFactory)!).HandlerInstance == handler
                );
            });
    }

    /// <summary>
    /// 取消工厂对象的事件订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">要取消订阅的事件处理器工厂</param>
    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        GetOrCreateHandlerFactories(eventType).Locking(factories => factories.Remove(factory));
    }

    /// <summary>
    /// 取消指定事件类型的所有订阅
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public override void UnsubscribeAll(Type eventType)
    {
        GetOrCreateHandlerFactories(eventType).Locking(factories => factories.Clear());
    }

    /// <summary>
    /// 异步发布本地事件消息
    /// </summary>
    /// <param name="localEventMessage">本地事件消息对象</param>
    /// <returns>异步任务</returns>
    public virtual async Task PublishAsync(LocalEventMessage localEventMessage)
    {
        await TriggerHandlersAsync(localEventMessage.EventType, localEventMessage.EventData);
    }

    /// <summary>
    /// 获取指定事件类型的事件处理器工厂列表
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件类型与事件处理器工厂的列表</returns>
    public virtual List<EventTypeWithEventHandlerFactories> GetEventHandlerFactories(Type eventType)
    {
        return [.. GetHandlerFactories(eventType)];
    }

    /// <summary>
    /// 将事件发布到事件总线
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>异步任务</returns>
    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        await PublishAsync(new LocalEventMessage(Guid.NewGuid(), eventData, eventType));
    }

    /// <summary>
    /// 将事件记录添加到工作单元
    /// </summary>
    /// <param name="unitOfWork">工作单元实例</param>
    /// <param name="eventRecord">事件记录</param>
    protected override void AddToUnitOfWork(IUnitOfWork unitOfWork, UnitOfWorkEventRecord eventRecord)
    {
        unitOfWork.AddOrReplaceLocalEvent(eventRecord);
    }

    /// <summary>
    /// 获取事件处理器工厂集合
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件类型与事件处理器工厂的集合</returns>
    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        var handlerFactoryList = new List<Tuple<IEventHandlerFactory, Type, int>>();
        foreach (var handlerFactory in HandlerFactories.Where(hf => ShouldTriggerEventForHandler(eventType, hf.Key)))
        {
            foreach (var factory in handlerFactory.Value)
            {
                handlerFactoryList.Add(new Tuple<IEventHandlerFactory, Type, int>(
                    factory,
                    handlerFactory.Key,
                    ReflectionHelper.GetAttributesOfMemberOrDeclaringType<LocalEventHandlerOrderAttribute>(
                        factory.GetHandler().EventHandler.GetType()).FirstOrDefault()?.Order ?? 0
                        )
                    );
            }
        }

        return [.. handlerFactoryList.OrderBy(x => x.Item3).Select(x =>
            new EventTypeWithEventHandlerFactories(x.Item2, [x.Item1]))];
    }

    /// <summary>
    /// 判断是否应该为指定的事件处理器触发事件
    /// </summary>
    /// <param name="targetEventType">目标事件类型</param>
    /// <param name="handlerEventType">处理器事件类型</param>
    /// <returns>如果应该触发返回 true,否则返回 false</returns>
    private static bool ShouldTriggerEventForHandler(Type targetEventType, Type handlerEventType)
    {
        //应该触发相同类型的事件
        if (handlerEventType == targetEventType)
        {
            return true;
        }

        //应该触发继承类型的事件
        if (handlerEventType.IsAssignableFrom(targetEventType))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取或创建指定事件类型的事件处理器工厂列表
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件处理器工厂列表</returns>
    private List<IEventHandlerFactory> GetOrCreateHandlerFactories(Type eventType)
    {
        return HandlerFactories.GetOrAdd(eventType, (type) => []);
    }
}
