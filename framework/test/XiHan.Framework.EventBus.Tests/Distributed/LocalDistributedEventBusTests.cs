// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Tests.Fakes;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 本地分布式事件总线测试
/// </summary>
/// <remarks>
/// 该实现把订阅与触发全部委派给本地事件总线，因此可以在完全不连接任何消息中间件的前提下
/// 验证发件箱/收件箱编排、去重、以及工作单元延迟发布这三条关键路径。
/// </remarks>
public class LocalDistributedEventBusTests
{
    /// <summary>
    /// 未启用事件盒时事件直接投递到订阅的处理器
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithoutEventBoxes_DeliversToSubscribedHandler()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent { Message = "直发" },
            onUnitOfWorkComplete: false,
            useOutbox: false);

        Assert.Equal("直发", Assert.Single(handler.Received).Message);
    }

    /// <summary>
    /// 订阅会把处理器登记到底层本地事件总线
    /// </summary>
    [Fact]
    public void Subscribe_RegistersHandlerOnLocalEventBus()
    {
        using var harness = LocalDistributedEventBusHarness.Create();

        harness.Bus.Subscribe<NamedNoticeEvent>(new RecordingDistributedHandler<NamedNoticeEvent>());

        Assert.Single(harness.LocalBus.GetEventHandlerFactories(typeof(NamedNoticeEvent)));
    }

    /// <summary>
    /// 退订会从底层本地事件总线移除处理器并停止投递
    /// </summary>
    [Fact]
    public async Task Unsubscribe_StopsDelivery()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        harness.Bus.Unsubscribe(typeof(NamedNoticeEvent), handler);
        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            onUnitOfWorkComplete: false,
            useOutbox: false);

        Assert.Empty(handler.Received);
        Assert.Empty(harness.LocalBus.GetEventHandlerFactories(typeof(NamedNoticeEvent)));
    }

    /// <summary>
    /// 退订全部会清空该事件类型在本地事件总线上的订阅
    /// </summary>
    [Fact]
    public void UnsubscribeAll_ClearsLocalEventBusRegistrations()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        harness.Bus.Subscribe<NamedNoticeEvent>(new RecordingDistributedHandler<NamedNoticeEvent>());
        harness.Bus.Subscribe<NamedNoticeEvent>(new AlternateRecordingDistributedHandler<NamedNoticeEvent>());

        harness.Bus.UnsubscribeAll(typeof(NamedNoticeEvent));

        Assert.Empty(harness.LocalBus.GetEventHandlerFactories(typeof(NamedNoticeEvent)));
    }

    /// <summary>
    /// 存在环境工作单元时事件被缓冲到工作单元的分布式事件列表
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenUnitOfWorkActive_BuffersDistributedEvent()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);
        var unitOfWork = harness.StartUnitOfWork();
        var eventData = new NamedNoticeEvent { Message = "延迟发布" };

        await harness.Bus.PublishAsync(typeof(NamedNoticeEvent), eventData, onUnitOfWorkComplete: true, useOutbox: true);

        Assert.Empty(handler.Received);
        Assert.Empty(unitOfWork.LocalEvents);
        var record = Assert.Single(unitOfWork.DistributedEvents);
        Assert.Equal(typeof(NamedNoticeEvent), record.EventType);
        Assert.Same(eventData, record.EventData);
        Assert.True(record.UseOutbox);
    }

    /// <summary>
    /// 缓冲到工作单元时保留是否走发件箱的选择
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenUnitOfWorkActive_PreservesUseOutboxChoice()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var unitOfWork = harness.StartUnitOfWork();

        await harness.Bus.PublishAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent(), onUnitOfWorkComplete: true, useOutbox: false);

        Assert.False(Assert.Single(unitOfWork.DistributedEvents).UseOutbox);
    }

    /// <summary>
    /// 配置了发件箱且存在工作单元时事件先入发件箱而不是直接投递
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithOutbox_EnqueuesInsteadOfDelivering()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            configureOptions: options => options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox)),
            correlationId: "corr-outbox");
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);
        harness.StartUnitOfWork();

        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent { Message = "走发件箱" },
            onUnitOfWorkComplete: false,
            useOutbox: true);

        Assert.Empty(handler.Received);
        var waiting = await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);
        var outgoing = Assert.Single(waiting);
        Assert.Equal(NamedNoticeEvent.DeclaredEventName, outgoing.EventName);
        Assert.Equal("corr-outbox", outgoing.GetCorrelationId());
    }

    /// <summary>
    /// 没有环境工作单元时发件箱不介入，事件按直发处理
    /// </summary>
    /// <remarks>
    /// 发件箱的意义是与业务事务同生共死，没有事务可挂靠时强行入箱只会让事件永远等不到提交。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WithOutboxButNoUnitOfWork_FallsBackToDirectDelivery()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            configureOptions: options => options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox)));
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            onUnitOfWorkComplete: false,
            useOutbox: true);

        Assert.Single(handler.Received);
        Assert.Empty(await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 发件箱选择器不匹配的事件不进箱，直接投递
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenOutboxSelectorRejectsEvent_DeliversDirectly()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            configureOptions: options => options.Outboxes.Configure(config =>
            {
                config.ImplementationType = typeof(InMemoryEventOutbox);
                config.Selector = eventType => eventType == typeof(PlainNoticeEvent);
            }));
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);
        harness.StartUnitOfWork();

        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            onUnitOfWorkComplete: false,
            useOutbox: true);

        Assert.Single(handler.Received);
        Assert.Empty(await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 配置了收件箱时直发事件也先落收件箱，由收件箱处理器异步触发
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithInbox_RoutesThroughInboxInsteadOfDelivering()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            configureOptions: options => options.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox)));
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent { Message = "走收件箱" },
            onUnitOfWorkComplete: false,
            useOutbox: false);

        Assert.Empty(handler.Received);
        Assert.Single(await harness.GetInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 从发件箱投递已知事件名的记录会触达处理器
    /// </summary>
    [Fact]
    public async Task PublishFromOutboxAsync_WithKnownEventName_DeliversToHandler()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.PublishFromOutboxAsync(CreateOutgoing("来自发件箱"), new OutboxConfig("Default"));

        Assert.Equal("来自发件箱", Assert.Single(handler.Received).Message);
    }

    /// <summary>
    /// 本实例没有订阅该事件名时静默忽略，不抛异常
    /// </summary>
    [Fact]
    public async Task PublishFromOutboxAsync_WithUnknownEventName_IsIgnored()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.PublishFromOutboxAsync(
            CreateOutgoing("无人认领", eventName: "xihan.tests.unknown"),
            new OutboxConfig("Default"));

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// 同一条发件箱记录被重复投递时收件箱只保留一条
    /// </summary>
    /// <remarks>
    /// 去重键取发件箱记录自身的标识：发送器重试或投递后未及时标记已发送都会造成重复投递，
    /// 若去重键取随机值则永远命不中，收件箱形同虚设。
    /// </remarks>
    [Fact]
    public async Task PublishFromOutboxAsync_WithSameRecordTwice_DeduplicatesInInbox()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            configureOptions: options => options.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox)));
        harness.Bus.Subscribe<NamedNoticeEvent>(new RecordingDistributedHandler<NamedNoticeEvent>());
        var outgoing = CreateOutgoing("重复投递");
        var outboxConfig = new OutboxConfig("Default");

        await harness.Bus.PublishFromOutboxAsync(outgoing, outboxConfig);
        await harness.Bus.PublishFromOutboxAsync(outgoing, outboxConfig);

        Assert.Single(await harness.GetInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 不同的发件箱记录各自入收件箱
    /// </summary>
    [Fact]
    public async Task PublishFromOutboxAsync_WithDistinctRecords_EnqueuesEach()
    {
        using var harness = LocalDistributedEventBusHarness.Create(
            configureOptions: options => options.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox)));
        harness.Bus.Subscribe<NamedNoticeEvent>(new RecordingDistributedHandler<NamedNoticeEvent>());
        var outboxConfig = new OutboxConfig("Default");

        await harness.Bus.PublishFromOutboxAsync(CreateOutgoing("第一条"), outboxConfig);
        await harness.Bus.PublishFromOutboxAsync(CreateOutgoing("第二条"), outboxConfig);

        var waiting = await harness.GetInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, waiting.Count);
    }

    /// <summary>
    /// 批量投递会逐条处理
    /// </summary>
    [Fact]
    public async Task PublishManyFromOutboxAsync_DeliversEveryRecord()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.PublishManyFromOutboxAsync(
            [CreateOutgoing("第一条"), CreateOutgoing("第二条")],
            new OutboxConfig("Default"));

        Assert.Equal(2, handler.Received.Count);
    }

    /// <summary>
    /// 处理收件箱事件时触达处理器
    /// </summary>
    [Fact]
    public async Task ProcessFromInboxAsync_DeliversToHandler()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.ProcessFromInboxAsync(CreateIncoming("来自收件箱"), new InboxConfig("Default"));

        Assert.Equal("来自收件箱", Assert.Single(handler.Received).Message);
    }

    /// <summary>
    /// 收件箱里出现本实例不认识的事件名时静默忽略
    /// </summary>
    [Fact]
    public async Task ProcessFromInboxAsync_WithUnknownEventName_IsIgnored()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.ProcessFromInboxAsync(
            CreateIncoming("无人认领", eventName: "xihan.tests.unknown"),
            new InboxConfig("Default"));

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// 收件箱配置的处理器选择器可把事件限定给指定处理器
    /// </summary>
    [Fact]
    public async Task ProcessFromInboxAsync_AppliesHandlerSelector()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var selected = new RecordingDistributedHandler<NamedNoticeEvent>();
        var skipped = new AlternateRecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(selected);
        harness.Bus.Subscribe<NamedNoticeEvent>(skipped);
        var inboxConfig = new InboxConfig("Default")
        {
            HandlerSelector = handlerType => handlerType == typeof(RecordingDistributedHandler<NamedNoticeEvent>)
        };

        await harness.Bus.ProcessFromInboxAsync(CreateIncoming("只给一个处理器"), inboxConfig);

        Assert.Single(selected.Received);
        Assert.Empty(skipped.Received);
    }

    /// <summary>
    /// 处理器失败时把原始异常抛回给收件箱处理器，交由其决定重试还是丢弃
    /// </summary>
    [Fact]
    public async Task ProcessFromInboxAsync_WhenHandlerFails_RethrowsOriginalException()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        harness.Bus.Subscribe<NamedNoticeEvent>(new ThrowingDistributedHandler<NamedNoticeEvent> { FailureMessage = "收件箱处理失败" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Bus.ProcessFromInboxAsync(CreateIncoming("必然失败"), new InboxConfig("Default")));

        Assert.Equal("收件箱处理失败", exception.Message);
    }

    /// <summary>
    /// 处理收件箱事件期间切换到消息自带的关联标识
    /// </summary>
    [Fact]
    public async Task ProcessFromInboxAsync_AppliesIncomingCorrelationId()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        harness.Bus.Subscribe<NamedNoticeEvent>(new RecordingDistributedHandler<NamedNoticeEvent>());
        var incoming = CreateIncoming("带关联标识");
        incoming.SetCorrelationId("corr-from-broker");

        await harness.Bus.ProcessFromInboxAsync(incoming, new InboxConfig("Default"));

        Assert.Contains(harness.CorrelationIdProvider.ChangedIds, id => id == "corr-from-broker");
        Assert.Null(harness.CorrelationIdProvider.Current);
    }

    /// <summary>
    /// 直发路径会向本地总线广播「已接收」通知
    /// </summary>
    [Fact]
    public async Task PublishAsync_DirectPath_NotifiesReceivedObservers()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var observer = new RecordingReceivedObserver();
        harness.LocalBus.Subscribe(typeof(DistributedEventReceived), observer);

        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            onUnitOfWorkComplete: false,
            useOutbox: false);

        var notification = Assert.Single(observer.Received);
        Assert.Equal(DistributedEventSource.Direct, notification.Source);
        Assert.Equal(NamedNoticeEvent.DeclaredEventName, notification.EventName);
    }

    /// <summary>
    /// 「已发送」通知的观察者失败不影响事件本身的投递
    /// </summary>
    /// <remarks>
    /// 通知是旁路观测能力，绝不能让它把主发布流程带崩。
    /// </remarks>
    [Fact]
    public async Task PublishAsync_WhenSentObserverFails_StillDeliversEvent()
    {
        using var harness = LocalDistributedEventBusHarness.Create();
        var observer = new ThrowingSentObserver();
        harness.LocalBus.Subscribe(typeof(DistributedEventSent), observer);
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            onUnitOfWorkComplete: false,
            useOutbox: false);

        Assert.Equal(1, observer.CallCount);
        Assert.Single(handler.Received);
    }

    /// <summary>
    /// 构造用于测试的出站事件
    /// </summary>
    /// <param name="message">载荷</param>
    /// <param name="id">事件标识</param>
    /// <param name="eventName">事件名称</param>
    /// <returns>出站事件</returns>
    private static OutgoingEventInfo CreateOutgoing(string message, Guid? id = null, string? eventName = null)
    {
        return new OutgoingEventInfo(
            id ?? Guid.NewGuid(),
            eventName ?? NamedNoticeEvent.DeclaredEventName,
            Serialize(message),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// 构造用于测试的入站事件
    /// </summary>
    /// <param name="message">载荷</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="messageId">消息标识</param>
    /// <returns>入站事件</returns>
    private static IncomingEventInfo CreateIncoming(string message, string? eventName = null, string messageId = "message-1")
    {
        return new IncomingEventInfo(
            Guid.NewGuid(),
            messageId,
            eventName ?? NamedNoticeEvent.DeclaredEventName,
            Serialize(message),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// 按总线自身的序列化口径生成事件负载
    /// </summary>
    /// <param name="message">载荷</param>
    /// <returns>序列化后的字节数组</returns>
    private static byte[] Serialize(string message)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new NamedNoticeEvent { Message = message }));
    }
}
