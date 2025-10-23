#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LocalDistributedEventBus
// Guid:b7e662f9-7f68-4a85-abf6-d4949a4c5426
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/19 7:58:48
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Core.Collections;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
using XiHan.Framework.Core.Timing;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds.Guids;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Attributes;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 本地分布式事件总线
/// </summary>
[Dependency(TryRegister = true)]
[ExposeServices(typeof(IDistributedEventBus), typeof(LocalDistributedEventBus))]
public class LocalDistributedEventBus : DistributedEventBusBase, ISingletonDependency
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="currentTenant">当前租户访问器</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="abpDistributedEventBusOptions">分布式事件总线选项</param>
    /// <param name="guidGenerator">全局唯一标识生成器</param>
    /// <param name="clock">时钟</param>
    /// <param name="eventHandlerInvoker">事件处理器调用器</param>
    /// <param name="localEventBus">本地事件总线</param>
    /// <param name="correlationIdProvider">关联唯一标识提供器</param>
    public LocalDistributedEventBus(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> abpDistributedEventBusOptions,
        IGuidGenerator guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider)
        : base(serviceScopeFactory,
            currentTenant,
            unitOfWorkManager,
            abpDistributedEventBusOptions,
            guidGenerator,
            clock,
            eventHandlerInvoker,
            localEventBus,
            correlationIdProvider)
    {
        EventTypes = new ConcurrentDictionary<string, Type>();
        Subscribe(abpDistributedEventBusOptions.Value.Handlers);
    }

    /// <summary>
    /// 事件类型字典集合
    /// </summary>
    protected ConcurrentDictionary<string, Type> EventTypes { get; }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="handlers">事件处理器列表</param>
    public virtual void Subscribe(ITypeList<IEventHandler> handlers)
    {
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
    /// 订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>事件订阅者</returns>
    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        var eventName = EventNameAttribute.GetNameOrDefault(eventType);
        EventTypes.GetOrAdd(eventName, eventType);
        return LocalEventBus.Subscribe(eventType, factory);
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理动作</param>
    /// <returns>任务</returns>
    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action)
    {
        LocalEventBus.Unsubscribe(action);
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    /// <returns>任务</returns>
    public override void Unsubscribe(Type eventType, IEventHandler handler)
    {
        LocalEventBus.Unsubscribe(eventType, handler);
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>任务</returns>
    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        LocalEventBus.Unsubscribe(eventType, factory);
    }

    /// <summary>
    /// 取消订阅所有事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>任务</returns>
    public override void UnsubscribeAll(Type eventType)
    {
        LocalEventBus.UnsubscribeAll(eventType);
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在当前工作单元完成后再发布事件</param>
    /// <param name="useOutbox">是否使用收件箱模式进行事件持久化与可靠投递</param>
    /// <returns>任务</returns>
    public override async Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true, bool useOutbox = true)
    {
        if (onUnitOfWorkComplete && UnitOfWorkManager.Current != null)
        {
            AddToUnitOfWork(
                UnitOfWorkManager.Current,
                new UnitOfWorkEventRecord(eventType, eventData, EventOrderGenerator.GetNext(), useOutbox)
            );
            return;
        }

        if (useOutbox)
        {
            if (await AddToOutboxAsync(eventType, eventData))
            {
                return;
            }
        }

        await TriggerDistributedEventSentAsync(new DistributedEventSent()
        {
            Source = DistributedEventSource.Direct,
            EventName = EventNameAttribute.GetNameOrDefault(eventType),
            EventData = eventData
        });

        await TriggerDistributedEventReceivedAsync(new DistributedEventReceived
        {
            Source = DistributedEventSource.Direct,
            EventName = EventNameAttribute.GetNameOrDefault(eventType),
            EventData = eventData
        });

        await PublishToEventBusAsync(eventType, eventData);
    }

    /// <summary>
    /// 从收件箱发布事件
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <param name="outboxConfig">出站配置</param>
    /// <returns>任务</returns>
    public override async Task PublishFromOutboxAsync(OutgoingEventInfo outgoingEvent, OutboxConfig outboxConfig)
    {
        await TriggerDistributedEventSentAsync(new DistributedEventSent()
        {
            Source = DistributedEventSource.Outbox,
            EventName = outgoingEvent.EventName,
            EventData = outgoingEvent.EventData
        });

        await TriggerDistributedEventReceivedAsync(new DistributedEventReceived
        {
            Source = DistributedEventSource.Direct,
            EventName = outgoingEvent.EventName,
            EventData = outgoingEvent.EventData
        });

        var eventType = EventTypes.GetOrDefault(outgoingEvent.EventName);
        if (eventType == null)
        {
            return;
        }

        var eventData = JsonSerializer.Deserialize(Encoding.UTF8.GetString(outgoingEvent.EventData), eventType)!;
        if (await AddToInboxAsync(Guid.NewGuid().ToString(), outgoingEvent.EventName, eventType, eventData, null))
        {
            return;
        }

        await LocalEventBus.PublishAsync(eventType, eventData, false);
    }

    /// <summary>
    /// 从收件箱发布多个事件
    /// </summary>
    /// <param name="outgoingEvents">出站事件信息列表</param>
    /// <param name="outboxConfig">出站配置</param>
    /// <returns>任务</returns>
    public override async Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig)
    {
        foreach (var outgoingEvent in outgoingEvents)
        {
            await PublishFromOutboxAsync(outgoingEvent, outboxConfig);
        }
    }

    /// <summary>
    /// 处理从入站事件盒接收到的事件
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    /// <param name="inboxConfig">入站配置</param>
    /// <returns>任务</returns>
    public override async Task ProcessFromInboxAsync(IncomingEventInfo incomingEvent, InboxConfig inboxConfig)
    {
        var eventType = EventTypes.GetOrDefault(incomingEvent.EventName);
        if (eventType == null)
        {
            return;
        }

        var eventData = JsonSerializer.Deserialize(incomingEvent.EventData, eventType);
        var exceptions = new List<Exception>();
        using (CorrelationIdProvider.Change(incomingEvent.GetCorrelationId()))
        {
            await TriggerHandlersFromInboxAsync(eventType, eventData!, exceptions, inboxConfig);
        }
        if (exceptions.Count != 0)
        {
            ThrowOriginalExceptions(eventType, exceptions);
        }
    }

    /// <summary>
    /// 发布事件到事件总线
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>任务</returns>
    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        if (await AddToInboxAsync(Guid.NewGuid().ToString(), EventNameAttribute.GetNameOrDefault(eventType), eventType, eventData, null))
        {
            return;
        }

        await LocalEventBus.PublishAsync(eventType, eventData, false);
    }

    /// <summary>
    /// 添加到工作单元
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="eventRecord">事件记录</param>
    protected override void AddToUnitOfWork(IUnitOfWork unitOfWork, UnitOfWorkEventRecord eventRecord)
    {
        unitOfWork.AddOrReplaceDistributedEvent(eventRecord);
    }

    /// <summary>
    /// 序列化事件数据
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>字节数组</returns>
    protected override byte[] Serialize(object eventData)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventData));
    }

    /// <summary>
    /// 添加到出站事件盒
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>任务</returns>
    protected override Task OnAddToOutboxAsync(string eventName, Type eventType, object eventData)
    {
        EventTypes.GetOrAdd(eventName, eventType);
        return base.OnAddToOutboxAsync(eventName, eventType, eventData);
    }

    /// <summary>
    /// 获取事件处理器工厂
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件处理器工厂列表</returns>
    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        return LocalEventBus.GetEventHandlerFactories(eventType);
    }
}
