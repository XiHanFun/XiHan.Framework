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
using System.Threading;
using XiHan.Framework.Core.Collections;
using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;
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

[Dependency(TryRegister = true)]
[ExposeServices(typeof(IDistributedEventBus), typeof(LocalDistributedEventBus))]
public class LocalDistributedEventBus : DistributedEventBusBase, ISingletonDependency
{
    public LocalDistributedEventBus(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> abpDistributedEventBusOptions,
        IGuidGenerator guidGenerator,
        //IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider)
        : base(serviceScopeFactory,
            currentTenant,
            unitOfWorkManager,
            abpDistributedEventBusOptions,
            guidGenerator,
            //clock,
            eventHandlerInvoker,
            localEventBus,
            correlationIdProvider)
    {
        EventTypes = new ConcurrentDictionary<string, Type>();
        Subscribe(abpDistributedEventBusOptions.Value.Handlers);
    }

    protected ConcurrentDictionary<string, Type> EventTypes { get; }

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

    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        var eventName = EventNameAttribute.GetNameOrDefault(eventType);
        EventTypes.GetOrAdd(eventName, eventType);
        return LocalEventBus.Subscribe(eventType, factory);
    }

    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action)
    {
        LocalEventBus.Unsubscribe(action);
    }

    public override void Unsubscribe(Type eventType, IEventHandler handler)
    {
        LocalEventBus.Unsubscribe(eventType, handler);
    }

    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        LocalEventBus.Unsubscribe(eventType, factory);
    }

    public override void UnsubscribeAll(Type eventType)
    {
        LocalEventBus.UnsubscribeAll(eventType);
    }

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
            //if (await AddToOutboxAsync(eventType, eventData))
            //{
            //    return;
            //}
        }

        //await TriggerDistributedEventSentAsync(new DistributedEventSent()
        //{
        //    Source = DistributedEventSource.Direct,
        //    EventName = EventNameAttribute.GetNameOrDefault(eventType),
        //    EventData = eventData
        //});

        //await TriggerDistributedEventReceivedAsync(new DistributedEventReceived
        //{
        //    Source = DistributedEventSource.Direct,
        //    EventName = EventNameAttribute.GetNameOrDefault(eventType),
        //    EventData = eventData
        //});

        await PublishToEventBusAsync(eventType, eventData);
    }

    public override async Task PublishFromOutboxAsync(OutgoingEventInfo outgoingEvent, OutboxConfig outboxConfig)
    {
        //await TriggerDistributedEventSentAsync(new DistributedEventSent()
        //{
        //    Source = DistributedEventSource.Outbox,
        //    EventName = outgoingEvent.EventName,
        //    EventData = outgoingEvent.EventData
        //});

        //await TriggerDistributedEventReceivedAsync(new DistributedEventReceived
        //{
        //    Source = DistributedEventSource.Direct,
        //    EventName = outgoingEvent.EventName,
        //    EventData = outgoingEvent.EventData
        //});

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

    public override async Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig)
    {
        foreach (var outgoingEvent in outgoingEvents)
        {
            await PublishFromOutboxAsync(outgoingEvent, outboxConfig);
        }
    }

    public override async Task ProcessFromInboxAsync(IncomingEventInfo incomingEvent, InboxConfig inboxConfig)
    {
        var eventType = EventTypes.GetOrDefault(incomingEvent.EventName);
        if (eventType == null)
        {
            return;
        }

        var eventData = JsonSerializer.Deserialize(incomingEvent.EventData, eventType);
        var exceptions = new List<Exception>();
        //using (CorrelationIdProvider.Change(incomingEvent.GetCorrelationId()))
        //{
        //    await TriggerHandlersFromInboxAsync(eventType, eventData!, exceptions, inboxConfig);
        //}
        if (exceptions.Any())
        {
            ThrowOriginalExceptions(eventType, exceptions);
        }
    }

    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        if (await AddToInboxAsync(Guid.NewGuid().ToString(), EventNameAttribute.GetNameOrDefault(eventType), eventType, eventData, null))
        {
            return;
        }

        await LocalEventBus.PublishAsync(eventType, eventData, false);
    }

    protected override void AddToUnitOfWork(IUnitOfWork unitOfWork, UnitOfWorkEventRecord eventRecord)
    {
        unitOfWork.AddOrReplaceDistributedEvent(eventRecord);
    }

    protected override byte[] Serialize(object eventData)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventData));
    }

    protected override Task OnAddToOutboxAsync(string eventName, Type eventType, object eventData)
    {
        EventTypes.GetOrAdd(eventName, eventType);
        return base.OnAddToOutboxAsync(eventName, eventType, eventData);
    }

    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        return LocalEventBus.GetEventHandlerFactories(eventType);
    }
}
