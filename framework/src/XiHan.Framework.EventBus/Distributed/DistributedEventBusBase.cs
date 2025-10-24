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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Timing;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds.Guids;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Attributes;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// 分布式事件总线基类
/// </summary>
public abstract class DistributedEventBusBase : EventBusBase, IDistributedEventBus, ISupportsEventBoxes
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
    protected DistributedEventBusBase(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> distributedEventBusOptions,
        IGuidGenerator guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider) : base(
        serviceScopeFactory,
        currentTenant,
        unitOfWorkManager,
        eventHandlerInvoker)
    {
        GuidGenerator = guidGenerator;
        Clock = clock;
        XiHanDistributedEventBusOptions = distributedEventBusOptions.Value;
        LocalEventBus = localEventBus;
        CorrelationIdProvider = correlationIdProvider;
    }

    /// <summary>
    /// 全局唯一标识生成器
    /// </summary>
    protected IGuidGenerator GuidGenerator { get; }

    /// <summary>
    /// 时钟
    /// </summary>
    protected IClock Clock { get; }

    /// <summary>
    /// 分布式事件总线配置选项
    /// </summary>
    protected XiHanDistributedEventBusOptions XiHanDistributedEventBusOptions { get; }

    /// <summary>
    /// 本地事件总线
    /// </summary>
    protected ILocalEventBus LocalEventBus { get; }

    /// <summary>
    /// 关联唯一标识提供器
    /// </summary>
    protected ICorrelationIdProvider CorrelationIdProvider { get; }

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="handler">事件处理器</param>
    /// <returns>事件订阅者</returns>
    public IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler) where TEvent : class
    {
        return Subscribe(typeof(TEvent), handler);
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在当前工作单元完成后再发布事件</param>
    /// <returns>任务</returns>
    public override Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
    {
        return PublishAsync(eventType, eventData, onUnitOfWorkComplete, useOutbox: true);
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在当前工作单元完成后再发布事件</param>
    /// <param name="useOutbox">是否使用收件箱模式进行事件持久化与可靠投递</param>
    /// <returns>任务</returns>
    public Task PublishAsync<TEvent>(
        TEvent eventData,
        bool onUnitOfWorkComplete = true,
        bool useOutbox = true)
        where TEvent : class
    {
        return PublishAsync(typeof(TEvent), eventData, onUnitOfWorkComplete, useOutbox);
    }

    /// <summary>
    /// 发布事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="onUnitOfWorkComplete">是否在当前工作单元完成后再发布事件</param>
    /// <param name="useOutbox">是否使用收件箱模式进行事件持久化与可靠投递</param>
    /// <returns>任务</returns>
    public virtual async Task PublishAsync(
        Type eventType,
        object eventData,
        bool onUnitOfWorkComplete = true,
        bool useOutbox = true)
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

        await PublishToEventBusAsync(eventType, eventData);

        await TriggerDistributedEventSentAsync(new DistributedEventSent()
        {
            Source = DistributedEventSource.Direct,
            EventName = EventNameAttribute.GetNameOrDefault(eventType),
            EventData = eventData
        });
    }

    /// <summary>
    /// 从收件箱发布事件
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <param name="outboxConfig">出站配置</param>
    /// <returns>任务</returns>
    public abstract Task PublishFromOutboxAsync(
        OutgoingEventInfo outgoingEvent,
        OutboxConfig outboxConfig
    );

    /// <summary>
    /// 从收件箱发布多个事件
    /// </summary>
    /// <param name="outgoingEvents">出站事件信息列表</param>
    /// <param name="outboxConfig">出站配置</param>
    /// <returns>任务</returns>
    public abstract Task PublishManyFromOutboxAsync(
        IEnumerable<OutgoingEventInfo> outgoingEvents,
        OutboxConfig outboxConfig
    );

    /// <summary>
    /// 处理从入站事件盒接收到的事件
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    /// <param name="inboxConfig">入站配置</param>
    /// <returns>任务</returns>
    public abstract Task ProcessFromInboxAsync(
        IncomingEventInfo incomingEvent,
        InboxConfig inboxConfig);

    /// <summary>
    /// 触发分布式事件发送
    /// </summary>
    /// <param name="distributedEvent">分布式事件发送</param>
    /// <returns></returns>
    public virtual async Task TriggerDistributedEventSentAsync(DistributedEventSent distributedEvent)
    {
        try
        {
            await LocalEventBus.PublishAsync(distributedEvent, onUnitOfWorkComplete: false);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    /// <summary>
    /// 触发分布式事件接收
    /// </summary>
    /// <param name="distributedEvent">分布式事件接收</param>
    /// <returns>任务</returns>
    public virtual async Task TriggerDistributedEventReceivedAsync(DistributedEventReceived distributedEvent)
    {
        try
        {
            await LocalEventBus.PublishAsync(distributedEvent, false);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    /// <summary>
    /// 添加到出站事件盒
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>是否添加到出站事件盒</returns>
    protected virtual async Task<bool> AddToOutboxAsync(Type eventType, object eventData)
    {
        var unitOfWork = UnitOfWorkManager.Current;
        if (unitOfWork == null)
        {
            return false;
        }

        var addedToOutbox = false;

        foreach (var outboxConfig in XiHanDistributedEventBusOptions.Outboxes.Values.OrderBy(x => x.Selector is null))
        {
            if (outboxConfig.Selector == null || outboxConfig.Selector(eventType))
            {
                var eventOutbox = (IEventOutbox)unitOfWork.ServiceProvider.GetRequiredService(outboxConfig.ImplementationType);
                var eventName = EventNameAttribute.GetNameOrDefault(eventType);

                await OnAddToOutboxAsync(eventName, eventType, eventData);

                var outgoingEventInfo = new OutgoingEventInfo(
                    GuidGenerator.Create(),
                    eventName,
                    Serialize(eventData),
                    Clock.Now
                );

                var correlationId = CorrelationIdProvider.Get();
                if (correlationId != null)
                {
                    outgoingEventInfo.SetCorrelationId(correlationId);
                }

                await eventOutbox.EnqueueAsync(outgoingEventInfo);
                addedToOutbox = true;
            }
        }

        return addedToOutbox;
    }

    /// <summary>
    /// 添加到出站事件盒
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>任务</returns>
    protected virtual Task OnAddToOutboxAsync(string eventName, Type eventType, object eventData)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 添加到入站事件盒
    /// </summary>
    /// <param name="messageId">消息唯一标识</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="correlationId">关联唯一标识</param>
    /// <returns>是否添加到入站事件盒</returns>
    protected async Task<bool> AddToInboxAsync(
        string? messageId,
        string eventName,
        Type eventType,
        object eventData,
        string? correlationId)
    {
        if (XiHanDistributedEventBusOptions.Inboxes.Count <= 0)
        {
            return false;
        }

        var addToInbox = false;

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            foreach (var inboxConfig in XiHanDistributedEventBusOptions.Inboxes.Values.OrderBy(x => x.EventSelector is null))
            {
                if (inboxConfig.EventSelector == null || inboxConfig.EventSelector(eventType))
                {
                    var eventInbox =
                        (IEventInbox)scope.ServiceProvider.GetRequiredService(inboxConfig.ImplementationType);

                    if (!messageId.IsNullOrEmpty())
                    {
                        if (await eventInbox.ExistsByMessageIdAsync(messageId!))
                        {
                            continue;
                        }
                    }

                    var incomingEventInfo = new IncomingEventInfo(
                        GuidGenerator.Create(),
                        messageId!,
                        eventName,
                        Serialize(eventData),
                        Clock.Now
                    );
                    incomingEventInfo.SetCorrelationId(correlationId!);
                    await eventInbox.EnqueueAsync(incomingEventInfo);
                    addToInbox = true;
                }
            }
        }

        return addToInbox;
    }

    /// <summary>
    /// 序列化事件数据
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>字节数组</returns>
    protected abstract byte[] Serialize(object eventData);

    /// <summary>
    /// 触发直接事件接收
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns></returns>
    protected virtual async Task TriggerHandlersDirectAsync(Type eventType, object eventData)
    {
        await TriggerDistributedEventReceivedAsync(new DistributedEventReceived
        {
            Source = DistributedEventSource.Direct,
            EventName = EventNameAttribute.GetNameOrDefault(eventType),
            EventData = eventData
        });

        await TriggerHandlersAsync(eventType, eventData);
    }

    /// <summary>
    /// 触发从入站事件盒接收事件
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="exceptions">异常列表</param>
    /// <param name="inboxConfig">入站配置</param>
    /// <returns>任务</returns>
    protected virtual async Task TriggerHandlersFromInboxAsync(Type eventType, object eventData, List<Exception> exceptions, InboxConfig? inboxConfig = null)
    {
        await TriggerDistributedEventReceivedAsync(new DistributedEventReceived
        {
            Source = DistributedEventSource.Inbox,
            EventName = EventNameAttribute.GetNameOrDefault(eventType),
            EventData = eventData
        });

        await TriggerHandlersAsync(eventType, eventData, exceptions, inboxConfig);
    }
}
