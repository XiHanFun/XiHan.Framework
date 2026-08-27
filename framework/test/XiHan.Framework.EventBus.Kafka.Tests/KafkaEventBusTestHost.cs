// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using XiHan.Framework.Core.Tracing;
using XiHan.Framework.EventBus.Abstractions;
using XiHan.Framework.EventBus.Abstractions.Local;
using XiHan.Framework.EventBus.Distributed;
using XiHan.Framework.EventBus.Local;

namespace XiHan.Framework.EventBus.Kafka.Tests;

/// <summary>
/// 可观测的 Kafka 分布式事件总线
/// </summary>
/// <remarks>
/// 序列化与入站消息处理都是 <c>protected</c>，但它们正是 Kafka 提供程序对外的两条数据通道：
/// 出站决定写进 Topic 的字节，入站是消费循环收到消息后的唯一入口。
/// 这里以最小面积把它们开放出来，从而在完全不连接 Broker 的前提下验证这两条通道。
/// </remarks>
public sealed class TestKafkaEventBus : KafkaDistributedEventBus
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂</param>
    /// <param name="kafkaOptions">Kafka 事件总线选项</param>
    /// <param name="localEventBus">本地事件总线</param>
    /// <param name="eventHandlerInvoker">事件处理器调用器</param>
    /// <param name="correlationIdProvider">关联标识提供器</param>
    public TestKafkaEventBus(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<XiHanKafkaEventBusOptions> kafkaOptions,
        ILocalEventBus localEventBus,
        IEventHandlerInvoker eventHandlerInvoker,
        ICorrelationIdProvider correlationIdProvider)
        : base(
            serviceScopeFactory,
            new StubCurrentTenant(),
            new StubUnitOfWorkManager(),
            Options.Create(new XiHanDistributedEventBusOptions()),
            kafkaOptions,
            new StubGuidGenerator(),
            new StubClock(),
            eventHandlerInvoker,
            localEventBus,
            correlationIdProvider,
            NullLogger<KafkaDistributedEventBus>.Instance)
    {
    }

    /// <summary>
    /// 序列化事件数据，得到写入 Kafka 消息 Value 的字节
    /// </summary>
    /// <param name="eventData">事件数据</param>
    /// <returns>字节数组</returns>
    public byte[] SerializeForTest(object eventData)
    {
        return Serialize(eventData);
    }

    /// <summary>
    /// 模拟消费循环收到一条 Kafka 消息
    /// </summary>
    /// <param name="messageId">消息唯一标识（取自 messageId 消息头）</param>
    /// <param name="eventName">事件名称（取自消息 Key）</param>
    /// <param name="correlationId">关联标识（取自 X-Correlation-Id 消息头）</param>
    /// <param name="body">消息 Value</param>
    /// <returns>表示异步操作的任务</returns>
    public Task ProcessIncomingMessageForTestAsync(string? messageId, string eventName, string? correlationId, byte[] body)
    {
        return ProcessIncomingMessageAsync(messageId, eventName, correlationId, body);
    }
}

/// <summary>
/// Kafka 事件总线测试宿主
/// </summary>
/// <remarks>
/// 用真实容器提供作用域工厂、用真实 <see cref="LocalEventBus"/> 承接订阅与派发，
/// 其余依赖用手写测试桩。构造过程完全不接触 Kafka——只有显式调用 <c>InitializeAsync</c> 才会连接 Broker。
/// </remarks>
public sealed class KafkaEventBusTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>
    /// 构造函数，使用默认 Kafka 选项
    /// </summary>
    public KafkaEventBusTestHost()
        : this(new XiHanKafkaEventBusOptions())
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="kafkaOptions">Kafka 事件总线选项</param>
    public KafkaEventBusTestHost(XiHanKafkaEventBusOptions kafkaOptions)
    {
        _provider = new ServiceCollection().BuildServiceProvider();

        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();

        Invoker = new RecordingEventHandlerInvoker();
        CorrelationIdProvider = new StubCorrelationIdProvider();
        LocalBus = new LocalEventBus(
            Options.Create(new XiHanLocalEventBusOptions()),
            scopeFactory,
            new StubCurrentTenant(),
            new StubUnitOfWorkManager(),
            Invoker);
        Bus = new TestKafkaEventBus(
            scopeFactory,
            Options.Create(kafkaOptions),
            LocalBus,
            Invoker,
            CorrelationIdProvider);
    }

    /// <summary>
    /// 被测的 Kafka 分布式事件总线
    /// </summary>
    public TestKafkaEventBus Bus { get; }

    /// <summary>
    /// 承接订阅的本地事件总线
    /// </summary>
    public LocalEventBus LocalBus { get; }

    /// <summary>
    /// 记录处理器调用的调用器
    /// </summary>
    public RecordingEventHandlerInvoker Invoker { get; }

    /// <summary>
    /// 关联标识提供器
    /// </summary>
    public StubCorrelationIdProvider CorrelationIdProvider { get; }

    /// <summary>
    /// 释放事件总线与容器
    /// </summary>
    /// <returns>表示异步操作的任务</returns>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        await Bus.DisposeAsync();
        await _provider.DisposeAsync();
    }
}
