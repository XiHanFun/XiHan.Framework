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
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds.Guids;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Attributes;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Uow;

namespace XiHan.Framework.EventBus.Distributed;

/// <summary>
/// DistributedEventBusBase
/// </summary>
public abstract class DistributedEventBusBase : EventBusBase, IDistributedEventBus, ISupportsEventBoxes
{
    protected DistributedEventBusBase(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> XiHanDistributedEventBusOptions,
        IGuidGenerator guidGenerator,
        //IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider) : base(
        serviceScopeFactory,
        currentTenant,
        unitOfWorkManager,
        eventHandlerInvoker)
    {
        GuidGenerator = guidGenerator;
        //Clock = clock;
        //XiHanDistributedEventBusOptions = XiHanDistributedEventBusOptions.Value;
        LocalEventBus = localEventBus;
        CorrelationIdProvider = correlationIdProvider;
    }

    protected IGuidGenerator GuidGenerator { get; }

    //protected IClock Clock { get; }
    protected XiHanDistributedEventBusOptions XiHanDistributedEventBusOptions { get; }

    protected ILocalEventBus LocalEventBus { get; }
    protected ICorrelationIdProvider CorrelationIdProvider { get; }

    public IDisposable Subscribe<TEvent>(IDistributedEventHandler<TEvent> handler) where TEvent : class
    {
        return Subscribe(typeof(TEvent), handler);
    }

    public override Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
    {
        return PublishAsync(eventType, eventData, onUnitOfWorkComplete, useOutbox: true);
    }

    public Task PublishAsync<TEvent>(
        TEvent eventData,
        bool onUnitOfWorkComplete = true,
        bool useOutbox = true)
        where TEvent : class
    {
        return PublishAsync(typeof(TEvent), eventData, onUnitOfWorkComplete, useOutbox);
    }

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
            //if (await AddToOutboxAsync(eventType, eventData))
            //{
            //    return;
            //}
        }

        await PublishToEventBusAsync(eventType, eventData);

        //await TriggerDistributedEventSentAsync(new DistributedEventSent()
        //{
        //    Source = DistributedEventSource.Direct,
        //    EventName = EventNameAttribute.GetNameOrDefault(eventType),
        //    EventData = eventData
        //});
    }

    public abstract Task PublishFromOutboxAsync(
        OutgoingEventInfo outgoingEvent,
        OutboxConfig outboxConfig
    );

    public abstract Task PublishManyFromOutboxAsync(
        IEnumerable<OutgoingEventInfo> outgoingEvents,
        OutboxConfig outboxConfig
    );

    public abstract Task ProcessFromInboxAsync(
        IncomingEventInfo incomingEvent,
        InboxConfig inboxConfig);

    //public virtual async Task TriggerDistributedEventSentAsync(DistributedEventSent distributedEvent)
    //{
    //    try
    //    {
    //        await LocalEventBus.PublishAsync(distributedEvent, onUnitOfWorkComplete: false);
    //    }
    //    catch (Exception)
    //    {
    //        // ignored
    //    }
    //}

    //public virtual async Task TriggerDistributedEventReceivedAsync(DistributedEventReceived distributedEvent)
    //{
    //    try
    //    {
    //        await LocalEventBus.PublishAsync(distributedEvent, false);
    //    }
    //    catch (Exception)
    //    {
    //        // ignored
    //    }
    //}

    //protected virtual async Task<bool> AddToOutboxAsync(Type eventType, object eventData)
    //{
    //    var unitOfWork = UnitOfWorkManager.Current;
    //    if (unitOfWork == null)
    //    {
    //        return false;
    //    }

    //    var addedToOutbox = false;

    //    foreach (var outboxConfig in XiHanDistributedEventBusOptions.Outboxes.Values.OrderBy(x => x.Selector is null))
    //    {
    //        if (outboxConfig.Selector == null || outboxConfig.Selector(eventType))
    //        {
    //            var eventOutbox = (IEventOutbox)unitOfWork.ServiceProvider.GetRequiredService(outboxConfig.ImplementationType);
    //            var eventName = EventNameAttribute.GetNameOrDefault(eventType);

    //            await OnAddToOutboxAsync(eventName, eventType, eventData);

    //            var outgoingEventInfo = new OutgoingEventInfo(
    //                GuidGenerator.Create(),
    //                eventName,
    //                Serialize(eventData),
    //                Clock.Now
    //            );

    //            var correlationId = CorrelationIdProvider.Get();
    //            if (correlationId != null)
    //            {
    //                outgoingEventInfo.SetCorrelationId(correlationId);
    //            }

    //            await eventOutbox.EnqueueAsync(outgoingEventInfo);
    //            addedToOutbox = true;
    //        }
    //    }

    //    return addedToOutbox;
    //}

    protected virtual Task OnAddToOutboxAsync(string eventName, Type eventType, object eventData)
    {
        return Task.CompletedTask;
    }

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
                    //var eventInbox =
                    //    (IEventInbox)scope.ServiceProvider.GetRequiredService(inboxConfig.ImplementationType);

                    //if (!messageId.IsNullOrEmpty())
                    //{
                    //    if (await eventInbox.ExistsByMessageIdAsync(messageId!))
                    //    {
                    //        continue;
                    //    }
                    //}

                    //var incomingEventInfo = new IncomingEventInfo(
                    //    GuidGenerator.Create(),
                    //    messageId!,
                    //    eventName,
                    //    Serialize(eventData),
                    //    Clock.Now
                    //);
                    //incomingEventInfo.SetCorrelationId(correlationId!);
                    //await eventInbox.EnqueueAsync(incomingEventInfo);
                    //addToInbox = true;
                }
            }
        }

        return addToInbox;
    }

    protected abstract byte[] Serialize(object eventData);

    //protected virtual async Task TriggerHandlersDirectAsync(Type eventType, object eventData)
    //{
    //    await TriggerDistributedEventReceivedAsync(new DistributedEventReceived
    //    {
    //        Source = DistributedEventSource.Direct,
    //        EventName = EventNameAttribute.GetNameOrDefault(eventType),
    //        EventData = eventData
    //    });

    //    await TriggerHandlersAsync(eventType, eventData);
    //}

    //protected virtual async Task TriggerHandlersFromInboxAsync(Type eventType, object eventData, List<Exception> exceptions, InboxConfig? inboxConfig = null)
    //{
    //    await TriggerDistributedEventReceivedAsync(new DistributedEventReceived
    //    {
    //        Source = DistributedEventSource.Inbox,
    //        EventName = EventNameAttribute.GetNameOrDefault(eventType),
    //        EventData = eventData
    //    });

    //    await TriggerHandlersAsync(eventType, eventData, exceptions, inboxConfig);
    //}
}
