// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
/// 面向消息中间件的分布式事件总线基类测试
/// </summary>
/// <remarks>
/// 基类把「投递到中间件」抽象成一个待实现的钩子，因此可以用一个只记录投递内容的假 Provider
/// 在完全不连接 RabbitMQ / Kafka / Redis 的前提下，验证事件名映射、发件箱投递、
/// 以及入站消息的收件箱幂等与内联处理两条分支。
/// </remarks>
public class BrokerDistributedEventBusBaseTests
{
    /// <summary>
    /// 订阅会同时登记到本地事件总线并建立事件名映射
    /// </summary>
    [Fact]
    public async Task Subscribe_RegistersHandlerAndEventNameMapping()
    {
        using var harness = BrokerBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        Assert.Single(harness.LocalBus.GetEventHandlerFactories(typeof(NamedNoticeEvent)));

        // 事件名映射建立后，入站消息才可能被反序列化并派发
        await harness.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            null,
            Serialize("已建立映射"));

        Assert.Single(handler.Received);
    }

    /// <summary>
    /// 直发路径把序列化后的事件推入中间件，并带上消息标识与关联标识
    /// </summary>
    [Fact]
    public async Task PublishAsync_WithoutOutbox_SendsSerializedEventToBroker()
    {
        using var harness = BrokerBusHarness.Create(correlationId: "corr-publish");

        await harness.Bus.PublishAsync(
            typeof(NamedNoticeEvent),
            new NamedNoticeEvent { Message = "推给中间件" },
            onUnitOfWorkComplete: false,
            useOutbox: false);

        var sent = Assert.Single(harness.Bus.Sent);
        Assert.Equal(NamedNoticeEvent.DeclaredEventName, sent.EventName);
        Assert.Equal("corr-publish", sent.CorrelationId);
        Assert.False(string.IsNullOrWhiteSpace(sent.MessageId));
        Assert.Equal("推给中间件", Deserialize(sent.Body).Message);
    }

    /// <summary>
    /// 存在环境工作单元时事件先缓冲，不立刻推入中间件
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenUnitOfWorkActive_DoesNotTouchBroker()
    {
        using var harness = BrokerBusHarness.Create();
        var unitOfWork = harness.StartUnitOfWork();

        await harness.Bus.PublishAsync(typeof(NamedNoticeEvent), new NamedNoticeEvent(), onUnitOfWorkComplete: true, useOutbox: true);

        Assert.Empty(harness.Bus.Sent);
        Assert.Single(unitOfWork.DistributedEvents);
    }

    /// <summary>
    /// 从发件箱投递时以发件箱记录自身的标识作为消息标识
    /// </summary>
    /// <remarks>
    /// 消费端按该标识做幂等去重，若每次重发都换标识，重投的消息会被当成新消息重复处理。
    /// </remarks>
    [Fact]
    public async Task PublishFromOutboxAsync_UsesRecordIdAsMessageId()
    {
        using var harness = BrokerBusHarness.Create();
        var outgoing = CreateOutgoing("来自发件箱");
        outgoing.SetCorrelationId("corr-outbox");

        await harness.Bus.PublishFromOutboxAsync(outgoing, new OutboxConfig("Default"));

        var sent = Assert.Single(harness.Bus.Sent);
        Assert.Equal(outgoing.Id.ToString("N"), sent.MessageId);
        Assert.Equal(NamedNoticeEvent.DeclaredEventName, sent.EventName);
        Assert.Equal("corr-outbox", sent.CorrelationId);
        Assert.Same(outgoing.EventData, sent.Body);
    }

    /// <summary>
    /// 从发件箱批量投递会逐条推入中间件
    /// </summary>
    [Fact]
    public async Task PublishManyFromOutboxAsync_SendsEveryRecord()
    {
        using var harness = BrokerBusHarness.Create();

        await harness.Bus.PublishManyFromOutboxAsync(
            [CreateOutgoing("第一条"), CreateOutgoing("第二条")],
            new OutboxConfig("Default"));

        Assert.Equal(2, harness.Bus.Sent.Count);
    }

    /// <summary>
    /// 未配置收件箱时入站消息在当前上下文直接触发处理器
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_WithoutInbox_TriggersHandlersInline()
    {
        using var harness = BrokerBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            null,
            Serialize("内联处理"));

        Assert.Equal("内联处理", Assert.Single(handler.Received).Message);
    }

    /// <summary>
    /// 本实例没有订阅该事件名时静默忽略入站消息
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_WithUnknownEventName_IsIgnored()
    {
        using var harness = BrokerBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            "xihan.tests.unknown",
            null,
            Serialize("无人认领"));

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// 配置收件箱后入站消息先落收件箱，不在消费上下文里同步处理
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_WithInbox_WritesToInboxInsteadOfTriggering()
    {
        using var harness = BrokerBusHarness.Create(
            options => options.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox)));
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            null,
            Serialize("走收件箱"));

        Assert.Empty(handler.Received);
        Assert.Single(await harness.GetInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 同一消息标识重复投递时收件箱只保留一条
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_WithSameMessageIdTwice_DeduplicatesInInbox()
    {
        using var harness = BrokerBusHarness.Create(
            options => options.Inboxes.Configure(config => config.ImplementationType = typeof(InMemoryEventInbox)));
        harness.Bus.Subscribe<NamedNoticeEvent>(new RecordingDistributedHandler<NamedNoticeEvent>());

        await harness.Bus.ProcessIncomingMessageForTestAsync("message-1", NamedNoticeEvent.DeclaredEventName, null, Serialize("第一次"));
        await harness.Bus.ProcessIncomingMessageForTestAsync("message-1", NamedNoticeEvent.DeclaredEventName, null, Serialize("重投"));

        Assert.Single(await harness.GetInbox().GetWaitingEventsAsync(10, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// 处理器失败时把原始异常抛回给 Provider，由其决定重投还是拒绝
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_WhenHandlerFails_RethrowsOriginalException()
    {
        using var harness = BrokerBusHarness.Create();
        harness.Bus.Subscribe<NamedNoticeEvent>(new ThrowingDistributedHandler<NamedNoticeEvent> { FailureMessage = "消费失败" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Bus.ProcessIncomingMessageForTestAsync(
                "message-1",
                NamedNoticeEvent.DeclaredEventName,
                null,
                Serialize("必然失败")));

        Assert.Equal("消费失败", exception.Message);
    }

    /// <summary>
    /// 内联处理期间切换到消息携带的关联标识，处理完成后还原
    /// </summary>
    [Fact]
    public async Task ProcessIncomingMessage_AppliesAndRestoresCorrelationId()
    {
        using var harness = BrokerBusHarness.Create();
        harness.Bus.Subscribe<NamedNoticeEvent>(new RecordingDistributedHandler<NamedNoticeEvent>());

        await harness.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            "corr-broker",
            Serialize("带关联标识"));

        Assert.Contains(harness.CorrelationIdProvider.ChangedIds, id => id == "corr-broker");
        Assert.Null(harness.CorrelationIdProvider.Current);
    }

    /// <summary>
    /// 收件箱处理器回调时触发处理器
    /// </summary>
    [Fact]
    public async Task ProcessFromInboxAsync_TriggersHandlers()
    {
        using var harness = BrokerBusHarness.Create();
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
        using var harness = BrokerBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        await harness.Bus.ProcessFromInboxAsync(
            CreateIncoming("无人认领", "xihan.tests.unknown"),
            new InboxConfig("Default"));

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// 退订后入站消息不再触发该处理器
    /// </summary>
    [Fact]
    public async Task Unsubscribe_StopsInlineDelivery()
    {
        using var harness = BrokerBusHarness.Create();
        var handler = new RecordingDistributedHandler<NamedNoticeEvent>();
        harness.Bus.Subscribe<NamedNoticeEvent>(handler);

        harness.Bus.Unsubscribe(typeof(NamedNoticeEvent), handler);
        await harness.Bus.ProcessIncomingMessageForTestAsync(
            "message-1",
            NamedNoticeEvent.DeclaredEventName,
            null,
            Serialize("已退订"));

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// 构造用于测试的出站事件
    /// </summary>
    /// <param name="message">载荷</param>
    /// <returns>出站事件</returns>
    private static OutgoingEventInfo CreateOutgoing(string message)
    {
        return new OutgoingEventInfo(
            Guid.NewGuid(),
            NamedNoticeEvent.DeclaredEventName,
            Serialize(message),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// 构造用于测试的入站事件
    /// </summary>
    /// <param name="message">载荷</param>
    /// <param name="eventName">事件名称</param>
    /// <returns>入站事件</returns>
    private static IncomingEventInfo CreateIncoming(string message, string? eventName = null)
    {
        return new IncomingEventInfo(
            Guid.NewGuid(),
            "message-1",
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

    /// <summary>
    /// 还原事件负载
    /// </summary>
    /// <param name="body">序列化后的字节数组</param>
    /// <returns>事件</returns>
    private static NamedNoticeEvent Deserialize(byte[] body)
    {
        return JsonSerializer.Deserialize<NamedNoticeEvent>(Encoding.UTF8.GetString(body))!;
    }
}

/// <summary>
/// 测试替身：记录投递内容、不连接任何消息中间件的 Provider
/// </summary>
public sealed class FakeBrokerDistributedEventBus : BrokerDistributedEventBusBase
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
    public FakeBrokerDistributedEventBus(
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
    /// 按调用顺序记录推入中间件的消息
    /// </summary>
    public List<BrokerMessageRecord> Sent { get; } = [];

    /// <summary>
    /// 初始化次数
    /// </summary>
    public int InitializeCallCount { get; private set; }

    /// <summary>
    /// 初始化 Provider，测试替身只记录调用次数
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    public override Task InitializeAsync()
    {
        InitializeCallCount++;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 暴露受保护的入站消息入口供测试调用
    /// </summary>
    /// <param name="messageId">消息标识</param>
    /// <param name="eventName">事件名称</param>
    /// <param name="correlationId">关联标识</param>
    /// <param name="body">序列化的事件数据</param>
    /// <returns>表示异步操作的任务</returns>
    public Task ProcessIncomingMessageForTestAsync(string? messageId, string eventName, string? correlationId, byte[] body)
    {
        return ProcessIncomingMessageAsync(messageId, eventName, correlationId, body);
    }

    /// <summary>
    /// 推入中间件，测试替身只记录
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="body">序列化的事件数据</param>
    /// <param name="messageId">消息标识</param>
    /// <param name="correlationId">关联标识</param>
    /// <returns>表示异步操作的任务</returns>
    protected override Task PublishToBrokerAsync(string eventName, byte[] body, string? messageId, string? correlationId)
    {
        Sent.Add(new BrokerMessageRecord(eventName, body, messageId, correlationId));
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试替身：一条被推入中间件的消息
/// </summary>
public sealed class BrokerMessageRecord
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="body">序列化的事件数据</param>
    /// <param name="messageId">消息标识</param>
    /// <param name="correlationId">关联标识</param>
    public BrokerMessageRecord(string eventName, byte[] body, string? messageId, string? correlationId)
    {
        EventName = eventName;
        Body = body;
        MessageId = messageId;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// 事件名称
    /// </summary>
    public string EventName { get; }

    /// <summary>
    /// 序列化的事件数据
    /// </summary>
    public byte[] Body { get; }

    /// <summary>
    /// 消息标识
    /// </summary>
    public string? MessageId { get; }

    /// <summary>
    /// 关联标识
    /// </summary>
    public string? CorrelationId { get; }
}

/// <summary>
/// 面向消息中间件的分布式事件总线测试装配器
/// </summary>
public sealed class BrokerBusHarness : IDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="provider">服务提供器</param>
    /// <param name="options">分布式事件总线选项</param>
    /// <param name="correlationId">初始关联标识</param>
    private BrokerBusHarness(ServiceProvider provider, XiHanDistributedEventBusOptions options, string? correlationId)
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
        Bus = new FakeBrokerDistributedEventBus(
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
    public FakeBrokerDistributedEventBus Bus { get; }

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
    /// 创建装配器
    /// </summary>
    /// <param name="configureOptions">分布式选项配置</param>
    /// <param name="correlationId">初始关联标识</param>
    /// <returns>装配器</returns>
    public static BrokerBusHarness Create(
        Action<XiHanDistributedEventBusOptions>? configureOptions = null,
        string? correlationId = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<InMemoryEventInbox>();
        services.AddSingleton<InMemoryEventOutbox>();
        var provider = services.BuildServiceProvider();

        var options = new XiHanDistributedEventBusOptions();
        configureOptions?.Invoke(options);

        return new BrokerBusHarness(provider, options, correlationId);
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
    /// 获取默认收件箱
    /// </summary>
    /// <returns>收件箱</returns>
    public InMemoryEventInbox GetInbox() => _provider.GetRequiredService<InMemoryEventInbox>();

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _provider.Dispose();
    }
}
