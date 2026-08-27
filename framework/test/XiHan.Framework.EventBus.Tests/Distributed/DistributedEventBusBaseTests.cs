// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.DistributedIds;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Distributed;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Local;
using XiHan.Framework.EventBus.Tests.Fakes;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Timing;
using XiHan.Framework.Uow;

namespace XiHan.Framework.EventBus.Tests.Distributed;

/// <summary>
/// 分布式事件总线基类编排测试
/// </summary>
/// <remarks>
/// 用一个只记录调用、不连接任何中间件的最小子类，验证基类自身的编排：
/// 工作单元缓冲、发件箱多箱分发与选择器、收件箱多箱分发与关联标识落库、以及旁路通知的容错。
/// 收件箱「重复消息也算已接管」这条既有用例已在 <c>InboxIdempotencyTests</c> 覆盖，此处不重复。
/// </remarks>
public class DistributedEventBusBaseTests
{
    /// <summary>
    /// 无工作单元且不走发件箱时直接推到事件总线
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithoutUnitOfWorkAndOutbox_PublishesToEventBus()
    {
        using var harness = RecordingBusHarness.Create();
        var eventData = new NamedNoticeEvent { Message = "直发" };

        await harness.Bus.PublishAsync(typeof(NamedNoticeEvent), eventData, onUnitOfWorkComplete: false, useOutbox: false);

        var published = Assert.Single(harness.Bus.PublishedToBus);
        Assert.Equal(typeof(NamedNoticeEvent), published.EventType);
        Assert.Same(eventData, published.EventData);
    }

    /// <summary>
    /// 存在工作单元时把事件缓冲起来，等提交后再统一投递
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenUnitOfWorkActive_BuffersInsteadOfPublishing()
    {
        using var harness = RecordingBusHarness.Create();
        var unitOfWork = harness.StartUnitOfWork();

        await harness.Bus.PublishAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent(), onUnitOfWorkComplete: true, useOutbox: true);

        Assert.Empty(harness.Bus.PublishedToBus);
        Assert.Single(unitOfWork.DistributedEvents);
        Assert.True(Assert.Single(harness.Bus.BufferedRecords).UseOutbox);
    }

    /// <summary>
    /// 未显式指定时默认走发件箱
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithoutExplicitOutboxChoice_DefaultsToUsingOutbox()
    {
        using var harness = RecordingBusHarness.Create();
        harness.StartUnitOfWork();

        await harness.Bus.PublishAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent(), onUnitOfWorkComplete: true);

        Assert.True(Assert.Single(harness.Bus.BufferedRecords).UseOutbox);
    }

    /// <summary>
    /// 配置发件箱后事件入箱且不再直接推到事件总线
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithOutbox_EnqueuesAndSkipsEventBus()
    {
        using var harness = RecordingBusHarness.Create(
            options => options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox)));
        harness.StartUnitOfWork();

        await harness.Bus.PublishAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent(), onUnitOfWorkComplete: false, useOutbox: true);

        Assert.Empty(harness.Bus.PublishedToBus);
        var outgoing = Assert.Single(await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(NamedNoticeEvent.DeclaredEventName, outgoing.EventName);
    }

    /// <summary>
    /// 入发件箱时把当前关联标识写进事件记录
    /// </summary>
    [Fact]
    public async Task AddToOutbox_WithCorrelationId_PersistsIt()
    {
        using var harness = RecordingBusHarness.Create(
            options => options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox)),
            correlationId: "corr-outbox");
        harness.StartUnitOfWork();

        Assert.True(await harness.Bus.AddToOutboxForTestAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent()));

        var outgoing = Assert.Single(await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("corr-outbox", outgoing.GetCorrelationId());
    }

    /// <summary>
    /// 没有关联标识时不写入该扩展属性
    /// </summary>
    [Fact]
    public async Task AddToOutbox_WithoutCorrelationId_LeavesItUnset()
    {
        using var harness = RecordingBusHarness.Create(
            options => options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox)));
        harness.StartUnitOfWork();

        await harness.Bus.AddToOutboxForTestAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent());

        var outgoing = Assert.Single(await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(outgoing.GetCorrelationId());
    }

    /// <summary>
    /// 没有环境工作单元时不入发件箱
    /// </summary>
    [Fact]
    public async Task AddToOutbox_WithoutUnitOfWork_ReturnsFalse()
    {
        using var harness = RecordingBusHarness.Create(
            options => options.Outboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventOutbox)));

        Assert.False(await harness.Bus.AddToOutboxForTestAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent()));
    }

    /// <summary>
    /// 未配置任何发件箱时不入箱
    /// </summary>
    [Fact]
    public async Task AddToOutbox_WithoutConfiguredBoxes_ReturnsFalse()
    {
        using var harness = RecordingBusHarness.Create();
        harness.StartUnitOfWork();

        Assert.False(await harness.Bus.AddToOutboxForTestAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent()));
    }

    /// <summary>
    /// 配置多个发件箱时每个匹配的箱子都会收到一份
    /// </summary>
    [Fact]
    public async Task AddToOutbox_WithMultipleBoxes_EnqueuesIntoEveryMatchingBox()
    {
        using var harness = RecordingBusHarness.Create(options =>
        {
            options.Outboxes.Configure("Default", config => config.ImplementationType = typeof(InMemoryEventOutbox));
            options.Outboxes.Configure("Secondary", config =>
            {
                config.ImplementationType = typeof(SecondaryEventOutbox);
                config.Selector = eventType => eventType == typeof(NamedNoticeEvent);
            });
        });
        harness.StartUnitOfWork();

        Assert.True(await harness.Bus.AddToOutboxForTestAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent()));

        Assert.Single(await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(await harness.GetSecondaryOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 选择器不匹配的发件箱被跳过，其余箱子照常入箱
    /// </summary>
    [Fact]
    public async Task AddToOutbox_WhenSelectorRejects_SkipsThatBoxOnly()
    {
        using var harness = RecordingBusHarness.Create(options =>
        {
            options.Outboxes.Configure("Default", config => config.ImplementationType = typeof(InMemoryEventOutbox));
            options.Outboxes.Configure("Secondary", config =>
            {
                config.ImplementationType = typeof(SecondaryEventOutbox);
                config.Selector = eventType => eventType == typeof(PlainNoticeEvent);
            });
        });
        harness.StartUnitOfWork();

        Assert.True(await harness.Bus.AddToOutboxForTestAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent()));

        Assert.Single(await harness.GetOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(await harness.GetSecondaryOutbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 未配置任何收件箱时交回调用方内联处理
    /// </summary>
    [Fact]
    public async Task AddToInbox_WithoutConfiguredBoxes_ReturnsFalse()
    {
        using var harness = RecordingBusHarness.Create();

        Assert.False(await harness.Bus.AddToInboxForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            null));
    }

    /// <summary>
    /// 入收件箱时把关联标识写进事件记录
    /// </summary>
    [Fact]
    public async Task AddToInbox_WithCorrelationId_PersistsIt()
    {
        using var harness = RecordingBusHarness.Create(
            options => options.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox)));

        await harness.Bus.AddToInboxForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            "corr-inbox");

        var incoming = Assert.Single(await harness.GetInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("corr-inbox", incoming.GetCorrelationId());
    }

    /// <summary>
    /// 关联标识缺省或为空白时不写入该扩展属性
    /// </summary>
    /// <param name="correlationId">关联标识</param>
    /// <remarks>
    /// 本地总线直发以及未携带该请求头的 Broker 消息都没有关联标识，
    /// 而写入接口对空值会抛异常，因此必须按「有值才写」处理。
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddToInbox_WithBlankCorrelationId_LeavesItUnset(string? correlationId)
    {
        using var harness = RecordingBusHarness.Create(
            options => options.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox)));

        await harness.Bus.AddToInboxForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            correlationId);

        var incoming = Assert.Single(await harness.GetInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(incoming.GetCorrelationId());
    }

    /// <summary>
    /// 事件选择器不匹配时收件箱不接管该事件
    /// </summary>
    [Fact]
    public async Task AddToInbox_WhenEventSelectorRejects_ReturnsFalse()
    {
        using var harness = RecordingBusHarness.Create(options => options.Inboxes.Configure(config =>
        {
            config.ImplementationType = typeof(InMemoryEventInbox);
            config.EventSelector = eventType => eventType == typeof(PlainNoticeEvent);
        }));

        Assert.False(await harness.Bus.AddToInboxForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            null));
        Assert.Empty(await harness.GetInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 配置多个收件箱时每个匹配的箱子都会收到一份
    /// </summary>
    [Fact]
    public async Task AddToInbox_WithMultipleBoxes_EnqueuesIntoEveryMatchingBox()
    {
        using var harness = RecordingBusHarness.Create(options =>
        {
            options.Inboxes.Configure("Default", config => config.ImplementationType = typeof(InMemoryEventInbox));
            options.Inboxes.Configure("Secondary", config =>
            {
                config.ImplementationType = typeof(SecondaryEventInbox);
                config.EventSelector = eventType => eventType == typeof(NamedNoticeEvent);
            });
        });

        Assert.True(await harness.Bus.AddToInboxForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent(),
            null));

        Assert.Single(await harness.GetInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(await harness.GetSecondaryInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 「已发送」通知会广播给本地订阅者
    /// </summary>
    [Fact]
    public async Task TriggerDistributedEventSentAsync_NotifiesLocalObservers()
    {
        using var harness = RecordingBusHarness.Create();
        var observer = new RecordingLocalHandler<DistributedEventSent>();
        harness.LocalBus.Subscribe(typeof(DistributedEventSent), observer);

        await harness.Bus.TriggerDistributedEventSentAsync(new DistributedEventSent
        {
            Source = DistributedEventSource.Outbox,
            EventName = NamedNoticeEvent.DeclaredEventName,
            EventData = new NamedNoticeEvent()
        });

        Assert.Equal(DistributedEventSource.Outbox, Assert.Single(observer.Received).Source);
    }

    /// <summary>
    /// 「已接收」通知会广播给本地订阅者
    /// </summary>
    [Fact]
    public async Task TriggerDistributedEventReceivedAsync_NotifiesLocalObservers()
    {
        using var harness = RecordingBusHarness.Create();
        var observer = new RecordingReceivedObserver();
        harness.LocalBus.Subscribe(typeof(DistributedEventReceived), observer);

        await harness.Bus.TriggerDistributedEventReceivedAsync(new DistributedEventReceived
        {
            Source = DistributedEventSource.Inbox,
            EventName = NamedNoticeEvent.DeclaredEventName,
            EventData = new NamedNoticeEvent()
        });

        Assert.Equal(DistributedEventSource.Inbox, Assert.Single(observer.Received).Source);
    }

    /// <summary>
    /// 「已发送」通知的观察者失败被吞掉，不影响调用方
    /// </summary>
    [Fact]
    public async Task TriggerDistributedEventSentAsync_WhenObserverFails_Swallows()
    {
        using var harness = RecordingBusHarness.Create();
        var observer = new ThrowingSentObserver();
        harness.LocalBus.Subscribe(typeof(DistributedEventSent), observer);

        await harness.Bus.TriggerDistributedEventSentAsync(new DistributedEventSent
        {
            Source = DistributedEventSource.Direct,
            EventName = NamedNoticeEvent.DeclaredEventName,
            EventData = new NamedNoticeEvent()
        });

        Assert.Equal(1, observer.CallCount);
    }

    /// <summary>
    /// 「已接收」通知的观察者失败同样被吞掉
    /// </summary>
    [Fact]
    public async Task TriggerDistributedEventReceivedAsync_WhenObserverFails_Swallows()
    {
        using var harness = RecordingBusHarness.Create();
        var observer = new ThrowingLocalHandler<DistributedEventReceived>();
        harness.LocalBus.Subscribe(typeof(DistributedEventReceived), observer);

        await harness.Bus.TriggerDistributedEventReceivedAsync(new DistributedEventReceived
        {
            Source = DistributedEventSource.Direct,
            EventName = NamedNoticeEvent.DeclaredEventName,
            EventData = new NamedNoticeEvent()
        });

        Assert.Equal(1, observer.CallCount);
    }

    /// <summary>
    /// 直发路径在推完事件后才广播「已发送」通知
    /// </summary>
    [Fact]
    public async Task PublishAsync_DirectPath_NotifiesSentObservers()
    {
        using var harness = RecordingBusHarness.Create();
        var observer = new RecordingLocalHandler<DistributedEventSent>();
        harness.LocalBus.Subscribe(typeof(DistributedEventSent), observer);

        await harness.Bus.PublishAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent(), onUnitOfWorkComplete: false, useOutbox: false);

        Assert.Single(harness.Bus.PublishedToBus);
        var notification = Assert.Single(observer.Received);
        Assert.Equal(DistributedEventSource.Direct, notification.Source);
        Assert.Equal(NamedNoticeEvent.DeclaredEventName, notification.EventName);
    }
}

/// <summary>
/// 测试替身：只记录编排结果、不连接任何消息中间件的分布式事件总线
/// </summary>
/// <remarks>
/// 订阅与处理器解析全部委派给真实的本地事件总线，投递动作则只记录不外发。
/// </remarks>
public sealed class RecordingDistributedEventBus : DistributedEventBusBase
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
    public RecordingDistributedEventBus(
        IServiceScopeFactory serviceScopeFactory,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<XiHanDistributedEventBusOptions> distributedEventBusOptions,
        IDistributedIdGenerator<Guid> guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider)
        : base(
            serviceScopeFactory,
            currentTenant,
            unitOfWorkManager,
            distributedEventBusOptions,
            guidGenerator,
            clock,
            eventHandlerInvoker,
            localEventBus,
            correlationIdProvider)
    {
    }

    /// <summary>
    /// 按调用顺序记录推到事件总线的事件
    /// </summary>
    public List<(Type EventType, object EventData)> PublishedToBus { get; } = [];

    /// <summary>
    /// 按调用顺序记录缓冲到工作单元的事件记录
    /// </summary>
    public List<UnitOfWorkEventRecord> BufferedRecords { get; } = [];

    /// <summary>
    /// 记录经发件箱投递出去的事件
    /// </summary>
    /// <remarks>
    /// 发件箱后台服务在后台线程写入、测试线程读取，故用并发集合。
    /// </remarks>
    public ConcurrentQueue<OutgoingEventInfo> OutboxPublished { get; } = new();

    /// <summary>
    /// 记录经收件箱处理的事件
    /// </summary>
    public ConcurrentQueue<IncomingEventInfo> InboxProcessed { get; } = new();

    /// <summary>
    /// 是否让收件箱处理失败
    /// </summary>
    public bool FailInboxProcessing { get; set; }

    /// <summary>
    /// 暴露受保护的入发件箱入口供测试调用
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>是否已入发件箱</returns>
    public Task<bool> AddToOutboxForTestAsync(Type eventType, object eventData)
    {
        return AddToOutboxAsync(eventType, eventData);
    }

    /// <summary>
    /// 暴露受保护的入收件箱入口供测试调用
    /// </summary>
    /// <param name="messageId">消息标识</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <param name="correlationId">关联标识</param>
    /// <returns>是否已由收件箱接管</returns>
    public Task<bool> AddToInboxForTestAsync(
        string? messageId,
        string eventName,
        Type eventType,
        object eventData,
        string? correlationId)
    {
        return AddToInboxAsync(messageId, eventName, eventType, eventData, correlationId);
    }

    /// <summary>
    /// 从发件箱投递，测试替身只记录
    /// </summary>
    /// <param name="outgoingEvent">出站事件信息</param>
    /// <param name="outboxConfig">出站配置</param>
    /// <returns>表示异步操作的任务</returns>
    public override Task PublishFromOutboxAsync(OutgoingEventInfo outgoingEvent, OutboxConfig outboxConfig)
    {
        OutboxPublished.Enqueue(outgoingEvent);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 从发件箱批量投递，测试替身只记录
    /// </summary>
    /// <param name="outgoingEvents">出站事件信息列表</param>
    /// <param name="outboxConfig">出站配置</param>
    /// <returns>表示异步操作的任务</returns>
    public override Task PublishManyFromOutboxAsync(IEnumerable<OutgoingEventInfo> outgoingEvents, OutboxConfig outboxConfig)
    {
        foreach (var outgoingEvent in outgoingEvents)
        {
            OutboxPublished.Enqueue(outgoingEvent);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理收件箱事件，测试替身只记录，必要时按开关模拟失败
    /// </summary>
    /// <param name="incomingEvent">入站事件信息</param>
    /// <param name="inboxConfig">入站配置</param>
    /// <returns>表示异步操作的任务</returns>
    public override Task ProcessFromInboxAsync(IncomingEventInfo incomingEvent, InboxConfig inboxConfig)
    {
        InboxProcessed.Enqueue(incomingEvent);

        return FailInboxProcessing
            ? Task.FromException(new InvalidOperationException("收件箱处理故意失败"))
            : Task.CompletedTask;
    }

    /// <summary>
    /// 订阅事件，委派给本地事件总线
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    /// <returns>订阅句柄</returns>
    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory) => base.LocalEventBus.Subscribe(eventType, factory);

    /// <summary>
    /// 取消委托订阅，委派给本地事件总线
    /// </summary>
    /// <typeparam name="TEvent">事件类型</typeparam>
    /// <param name="action">事件处理动作</param>
    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action) => base.LocalEventBus.Unsubscribe(action);

    /// <summary>
    /// 取消处理器订阅，委派给本地事件总线
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="handler">事件处理器</param>
    public override void Unsubscribe(Type eventType, IEventHandler handler) => base.LocalEventBus.Unsubscribe(eventType, handler);

    /// <summary>
    /// 取消工厂订阅，委派给本地事件总线
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="factory">事件处理器工厂</param>
    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory) => base.LocalEventBus.Unsubscribe(eventType, factory);

    /// <summary>
    /// 取消该事件类型的全部订阅，委派给本地事件总线
    /// </summary>
    /// <param name="eventType">事件类型</param>
    public override void UnsubscribeAll(Type eventType) => base.LocalEventBus.UnsubscribeAll(eventType);

    /// <summary>
    /// 序列化事件数据
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>字节数组</returns>
    protected override byte[] Serialize(object eventData) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventData));

    /// <summary>
    /// 推送事件到事件总线，测试替身只记录
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <param name="eventData">事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    protected override Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        PublishedToBus.Add((eventType, eventData));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 缓冲事件到工作单元
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    /// <param name="eventRecord">事件记录</param>
    protected override void AddToUnitOfWork(IUnitOfWork unitOfWork, UnitOfWorkEventRecord eventRecord)
    {
        BufferedRecords.Add(eventRecord);
        unitOfWork.AddOrReplaceDistributedEvent(eventRecord);
    }

    /// <summary>
    /// 获取事件处理器工厂，委派给本地事件总线
    /// </summary>
    /// <param name="eventType">事件类型</param>
    /// <returns>事件处理器工厂集合</returns>
    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
        => base.LocalEventBus.GetEventHandlerFactories(eventType);
}

/// <summary>
/// 分布式事件总线基类测试装配器
/// </summary>
public sealed class RecordingBusHarness : IDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="provider">服务提供器</param>
    /// <param name="options">分布式事件总线选项</param>
    /// <param name="correlationId">初始关联标识</param>
    private RecordingBusHarness(ServiceProvider provider, XiHanDistributedEventBusOptions options, string? correlationId)
    {
        _provider = provider;
        CurrentTenant = new FakeCurrentTenant();
        UnitOfWorkManager = new FakeUnitOfWorkManager();
        CorrelationIdProvider = new FakeCorrelationIdProvider(correlationId);

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var invoker = new EventHandlerInvoker();
        LocalBus = new LocalEventBus(
            Microsoft.Extensions.Options.Options.Create(new XiHanLocalEventBusOptions()),
            scopeFactory,
            CurrentTenant,
            UnitOfWorkManager,
            invoker);
        Bus = new RecordingDistributedEventBus(
            scopeFactory,
            CurrentTenant,
            UnitOfWorkManager,
            Microsoft.Extensions.Options.Options.Create(options),
            new StubGuidGenerator(),
            new StubClock(),
            invoker,
            LocalBus,
            CorrelationIdProvider);
    }

    /// <summary>
    /// 被测总线
    /// </summary>
    public RecordingDistributedEventBus Bus { get; }

    /// <summary>
    /// 底层本地事件总线
    /// </summary>
    public LocalEventBus LocalBus { get; }

    /// <summary>
    /// 当前租户上下文
    /// </summary>
    public FakeCurrentTenant CurrentTenant { get; }

    /// <summary>
    /// 工作单元管理器
    /// </summary>
    public FakeUnitOfWorkManager UnitOfWorkManager { get; }

    /// <summary>
    /// 关联标识提供器
    /// </summary>
    public FakeCorrelationIdProvider CorrelationIdProvider { get; }

    /// <summary>
    /// 服务提供器
    /// </summary>
    public IServiceProvider Services => _provider;

    /// <summary>
    /// 创建装配器
    /// </summary>
    /// <param name="configureOptions">分布式选项配置</param>
    /// <param name="correlationId">初始关联标识</param>
    /// <returns>装配器</returns>
    public static RecordingBusHarness Create(
        Action<XiHanDistributedEventBusOptions>? configureOptions = null,
        string? correlationId = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<InMemoryEventOutbox>();
        services.AddSingleton<SecondaryEventOutbox>();
        services.AddSingleton<InMemoryEventInbox>();
        services.AddSingleton<SecondaryEventInbox>();
        var provider = services.BuildServiceProvider();

        var options = new XiHanDistributedEventBusOptions();
        configureOptions?.Invoke(options);

        return new RecordingBusHarness(provider, options, correlationId);
    }

    /// <summary>
    /// 开启一个环境工作单元
    /// </summary>
    /// <returns>工作单元</returns>
    public FakeUnitOfWork StartUnitOfWork()
    {
        var unitOfWork = new FakeUnitOfWork(_provider);
        UnitOfWorkManager.Current = unitOfWork;
        return unitOfWork;
    }

    /// <summary>
    /// 获取默认发件箱
    /// </summary>
    /// <returns>发件箱</returns>
    public InMemoryEventOutbox GetOutbox() => _provider.GetRequiredService<InMemoryEventOutbox>();

    /// <summary>
    /// 获取第二个发件箱
    /// </summary>
    /// <returns>发件箱</returns>
    public SecondaryEventOutbox GetSecondaryOutbox() => _provider.GetRequiredService<SecondaryEventOutbox>();

    /// <summary>
    /// 获取默认收件箱
    /// </summary>
    /// <returns>收件箱</returns>
    public InMemoryEventInbox GetInbox() => _provider.GetRequiredService<InMemoryEventInbox>();

    /// <summary>
    /// 获取第二个收件箱
    /// </summary>
    /// <returns>收件箱</returns>
    public SecondaryEventInbox GetSecondaryInbox() => _provider.GetRequiredService<SecondaryEventInbox>();

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _provider.Dispose();
    }
}
