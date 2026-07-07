#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BrokerDistributedEventBusBase
// Guid:f1ebe388-464c-4cfc-b006-d48cff8e0cb3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/07/07 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Core.Collections;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Attributes;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Uow;
using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 面向真实消息中间件（RabbitMQ / Kafka / Redis 等）的分布式事件总线基类
/// </summary>
/// <remarks>
/// 抽取三种 Broker Provider 的公共逻辑：事件类型映射、订阅/退订（委派本地事件总线）、
/// 发件箱可靠投递、入站幂等/重试（复用收件箱）与统一的入站消息处理。
/// 具体 Provider 只需实现 <see cref="PublishToBrokerAsync"/>（把序列化后的事件送入 Broker）
/// 与 <see cref="InitializeAsync"/>（建立连接并注册消费者，收到消息后回调 <see cref="ProcessIncomingMessageAsync"/>）。
/// </remarks>
public abstract class BrokerDistributedEventBusBase : DistributedEventBusBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="currentTenant">当前租户访问器</param>
    /// <param name="unitOfWorkManager">工作单元管理器</param>
    /// <param name="distributedEventBusOptions">分布式事件总线选项</param>
    /// <param name="guidGenerator">全局唯一标识生成器</param>
    /// <param name="clock">时钟</param>
    /// <param name="eventHandlerInvoker">事件处理器调用器</param>
    /// <param name="localEventBus">本地事件总线</param>
    /// <param name="correlationIdProvider">关联唯一标识提供器</param>
    protected BrokerDistributedEventBusBase(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> distributedEventBusOptions,
        IDistributedIdGenerator<Guid> guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider)
        : base(serviceScopeFactory,
            currentTenant,
            unitOfWorkManager,
            distributedEventBusOptions,
            guidGenerator,
            clock,
            eventHandlerInvoker,
            localEventBus,
            correlationIdProvider)
    {
        EventTypes = new ConcurrentDictionary<string, Type>();

        // 把已声明的分布式事件处理器登记到本地事件总线并填充事件类型映射
        // （InitializeAsync 时据此把事件名绑定到 Broker 的队列 / 订阅）
        Subscribe(distributedEventBusOptions.Value.Handlers);
    }

    /// <summary>
    /// 事件名称 → 事件类型 映射（用于入站消息反序列化与投递）
    /// </summary>
    protected ConcurrentDictionary<string, Type> EventTypes { get; }

    /// <summary>
    /// 初始化 Provider：建立与 Broker 的连接并注册消费者
    /// </summary>
    /// <remarks>由 Provider 模块在应用初始化阶段（所有事件处理器已注册后）调用。</remarks>
    public abstract Task InitializeAsync();

    /// <summary>
    /// 订阅一批事件处理器（把已声明的分布式处理器登记到本地事件总线并填充事件类型映射）
    /// </summary>
    /// <param name="handlers">事件处理器类型列表</param>
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
    /// <returns>订阅句柄</returns>
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
    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action)
    {
        LocalEventBus.Unsubscribe(action);
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    public override void Unsubscribe(Type eventType, IEventHandler handler)
    {
        LocalEventBus.Unsubscribe(eventType, handler);
    }

    /// <summary>
    /// 取消订阅事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        LocalEventBus.Unsubscribe(eventType, factory);
    }

    /// <summary>
    /// 取消订阅某类型的所有事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public override void UnsubscribeAll(Type eventType)
    {
        LocalEventBus.UnsubscribeAll(eventType);
    }

    /// <summary>
    /// 从发件箱投递单条事件到 Broker
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <param name="outboxConfig">出站配置</param>
    public override async Task PublishFromOutboxAsync(OutgoingEventInfo outgoingEvent, OutboxConfig outboxConfig)
    {
        await TriggerDistributedEventSentAsync(new DistributedEventSent
        {
            Source = DistributedEventSource.Outbox,
            EventName = outgoingEvent.EventName,
            EventData = outgoingEvent.EventData
        });

        await PublishToBrokerAsync(
            outgoingEvent.EventName,
            outgoingEvent.EventData,
            outgoingEvent.Id.ToString("N"),
            outgoingEvent.GetCorrelationId());
    }

    /// <summary>
    /// 从发件箱批量投递事件到 Broker
    /// </summary>
    /// <param name="outgoingEvents">出站事件信息列表</param>
    /// <param name="outboxConfig">出站配置</param>
    public override async Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig)
    {
        foreach (var outgoingEvent in outgoingEvents)
        {
            await PublishFromOutboxAsync(outgoingEvent, outboxConfig);
        }
    }

    /// <summary>
    /// 处理收件箱中的事件（触发本地处理器）
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    /// <param name="inboxConfig">入站配置</param>
    public override async Task ProcessFromInboxAsync(IncomingEventInfo incomingEvent, InboxConfig inboxConfig)
    {
        var eventType = EventTypes.GetOrDefault(incomingEvent.EventName);
        if (eventType == null)
        {
            return;
        }

        var eventData = JsonSerializer.Deserialize(incomingEvent.EventData, eventType)!;
        var exceptions = new List<Exception>();
        using (CorrelationIdProvider.Change(incomingEvent.GetCorrelationId()))
        {
            await TriggerHandlersFromInboxAsync(eventType, eventData, exceptions, inboxConfig);
        }

        if (exceptions.Count != 0)
        {
            ThrowOriginalExceptions(eventType, exceptions);
        }
    }

    /// <summary>
    /// 直接发布事件到事件总线（非发件箱路径，直接推入 Broker）
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        var eventName = EventNameAttribute.GetNameOrDefault(eventType);
        EventTypes.GetOrAdd(eventName, eventType);

        await PublishToBrokerAsync(
            eventName,
            Serialize(eventData),
            GuidGenerator.NextId().ToString("N"),
            CorrelationIdProvider.Get());
    }

    /// <summary>
    /// 把分布式事件缓冲到工作单元（提交后再统一投递，保证与业务事务一致）
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="eventRecord">事件记录</param>
    protected override void AddToUnitOfWork(IUnitOfWork unitOfWork, UnitOfWorkEventRecord eventRecord)
    {
        unitOfWork.AddOrReplaceDistributedEvent(eventRecord);
    }

    /// <summary>
    /// 加入发件箱前登记事件类型映射
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    protected override Task OnAddToOutboxAsync(string eventName, Type eventType, object eventData)
    {
        EventTypes.GetOrAdd(eventName, eventType);
        return base.OnAddToOutboxAsync(eventName, eventType, eventData);
    }

    /// <summary>
    /// 序列化事件数据（UTF-8 JSON）
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>字节数组</returns>
    protected override byte[] Serialize(object eventData)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventData));
    }

    /// <summary>
    /// 获取事件处理器工厂（来自本地事件总线）
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件处理器工厂列表</returns>
    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        return LocalEventBus.GetEventHandlerFactories(eventType);
    }

    /// <summary>
    /// 处理从 Broker 收到的入站消息（Provider 消费者回调统一入口）
    /// </summary>
    /// <param name="messageId">消息唯一标识（用于收件箱幂等去重，可空）</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="correlationId">关联标识（可空）</param>
    /// <param name="body">序列化的事件数据</param>
    /// <remarks>
    /// 若配置了收件箱：写入收件箱（去重）后返回，由收件箱处理器异步触发处理器（带重试）；
    /// 否则在当前上下文直接触发处理器。处理失败会抛出，供 Provider 决定是否重投/Nack。
    /// </remarks>
    protected virtual async Task ProcessIncomingMessageAsync(string? messageId, string eventName, string? correlationId, byte[] body)
    {
        var eventType = EventTypes.GetOrDefault(eventName);
        if (eventType == null)
        {
            // 本实例没有该事件的订阅者，直接忽略
            return;
        }

        var eventData = JsonSerializer.Deserialize(body, eventType)!;

        // 优先走收件箱：保证幂等（按 messageId 去重）与失败重试
        if (await AddToInboxAsync(messageId, eventName, eventType, eventData, correlationId))
        {
            return;
        }

        // 未配置收件箱：直接触发处理器
        var exceptions = new List<Exception>();
        using (CorrelationIdProvider.Change(correlationId))
        {
            await TriggerHandlersFromInboxAsync(eventType, eventData, exceptions, null);
        }

        if (exceptions.Count != 0)
        {
            ThrowOriginalExceptions(eventType, exceptions);
        }
    }

    /// <summary>
    /// 把序列化后的事件送入具体的消息中间件（由各 Provider 实现）
    /// </summary>
    /// <param name="eventName">事件名称（用作路由键 / Topic Key / Stream 键）</param>
    /// <param name="body">序列化的事件数据</param>
    /// <param name="messageId">消息唯一标识</param>
    /// <param name="correlationId">关联标识（可空）</param>
    protected abstract Task PublishToBrokerAsync(string eventName, byte[] body, string? messageId, string? correlationId);
}
