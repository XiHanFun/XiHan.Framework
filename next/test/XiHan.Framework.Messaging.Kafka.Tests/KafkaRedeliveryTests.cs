// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Error = XiHan.Framework.Primitives.Error;
using ErrorCode = Confluent.Kafka.ErrorCode;

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// 投递没有完成时消费循环不退出（RFC-0009「流」第 2 条，ADR-0064）。
/// </summary>
/// <remarks>
/// 打桩的生产者与消费者、可推进的时钟：死信发布没有落盘或抛出时不提交、回退到这条消息，等 <see cref="KafkaMessagingOptions.RedeliveryDelay"/> 后重新投递，恢复后继续消费下一条；消费者本身出错也不退出；停止信号到来照常退出。不需要 Kafka。
/// </remarks>
public sealed class KafkaRedeliveryTests
{
    /// <summary>
    /// 测试主题。
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 主题映射后的死信主题。
    /// </summary>
    private const string DeadLetterTopic = "xihan.orders.dlq";

    /// <summary>
    /// 重新投递前的等待。
    /// </summary>
    private static readonly TimeSpan _delay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 等待条件成立的上限。
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadLetterPublishFailure_IsNotCommitted_RewindsAndRedelivers_ThenMovesOn(bool throws)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero));
        var (producer, producerFake) = FakeKafkaProducer.Create();
        // 第一次死信发布失败：没有落盘，或客户端抛出（投递超时等）；之后恢复
        producerFake.Respond = (sequence, topic, message) => sequence > 0
            ? FakeKafkaProducer.Report(topic, message, PersistenceStatus.Persisted)
            : throws
                ? throw new ProduceException<string?, byte[]>(new Confluent.Kafka.Error(ErrorCode.Local_MsgTimedOut), FakeKafkaProducer.Report(topic, message, PersistenceStatus.NotPersisted))
                : FakeKafkaProducer.Report(topic, message, PersistenceStatus.NotPersisted);
        var (consumer, consumerFake) = FakeKafkaConsumer.Create(Encode("poison"), Encode("next"));
        var dispatched = new ConcurrentQueue<string>();
        await using var broker = Broker(host, clock, producer, consumer);

        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing)], Dispatch(dispatched), CancellationToken.None);

        // 处理器两次都失败、死信发布失败：不提交，回退到这条消息，循环在等时钟
        Assert.True(SpinWait.SpinUntil(() => consumerFake.Read(static c => c.Seeks.Count) == 1, _patience));
        Assert.Equal([0L], consumerFake.Read(static c => c.Seeks.ToList()));
        Assert.Empty(consumerFake.Read(static c => c.Committed.ToList()));
        Assert.Equal([DeadLetterTopic], producerFake.Topics);
        Assert.DoesNotContain("next", dispatched);

        // 推进时钟：重新投递（处理器再跑两次）、死信发布落盘后提交，再继续消费下一条
        Assert.True(SpinWait.SpinUntil(() => { clock.Advance(_delay); return consumerFake.Read(static c => c.Committed.Count) == 2; }, _patience));
        Assert.Equal([0L, 1L], consumerFake.Read(static c => c.Committed.ToList()));
        Assert.Equal([DeadLetterTopic, DeadLetterTopic], producerFake.Topics);
        Assert.Equal(["poison", "poison", "poison", "poison", "next"], dispatched);

        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);
        Assert.True(consumerFake.Closed);
    }

    [Fact]
    public async Task Stop_WhileWaitingToRedeliver_ExitsWithoutCommitting()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var clock = new FakeTimeProvider();
        var (producer, producerFake) = FakeKafkaProducer.Create();
        producerFake.Respond = static (_, topic, message) => FakeKafkaProducer.Report(topic, message, PersistenceStatus.NotPersisted);
        var (consumer, consumerFake) = FakeKafkaConsumer.Create(Encode("poison"));
        await using var broker = Broker(host, clock, producer, consumer);

        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing)], Dispatch(new ConcurrentQueue<string>()), CancellationToken.None);
        Assert.True(SpinWait.SpinUntil(() => consumerFake.Read(static c => c.Seeks.Count) == 1, _patience));

        // 停止信号打断等待：循环退出，没有提交，消费者照常关闭
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);
        Assert.Empty(consumerFake.Read(static c => c.Committed.ToList()));
        Assert.True(consumerFake.Closed);
    }

    [Fact]
    public async Task ConsumerError_DoesNotEndTheLoop()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var clock = new FakeTimeProvider();
        var (producer, _) = FakeKafkaProducer.Create();
        var (consumer, consumerFake) = FakeKafkaConsumer.Create(Encode("ok"));
        // 消费者本身的错误（不是某条消息的 ConsumeException）：等一会儿再取
        consumerFake.ConsumeFailures.Enqueue(new KafkaException(ErrorCode.Local_Transport));
        var dispatched = new ConcurrentQueue<string>();
        await using var broker = Broker(host, clock, producer, consumer);

        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing)], Dispatch(dispatched), CancellationToken.None);

        Assert.True(SpinWait.SpinUntil(() => { clock.Advance(_delay); return consumerFake.Read(static c => c.Committed.Count) == 1; }, _patience));
        Assert.Equal(["ok"], dispatched);
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);
    }

    /// <summary>
    /// 用打桩的生产者与消费者创建代理：最多投递 2 次，重新投递前等 5 秒。
    /// </summary>
    private static KafkaMessageBroker Broker(TestHost host, TimeProvider clock, IProducer<string?, byte[]> producer, IConsumer<string?, byte[]> consumer)
        => new(
            new KafkaMessagingOptions { BootstrapServers = "unused:9092", RedeliveryDelay = _delay },
            new MessagingOptions { MaxAttempts = 2 },
            new MessagingInstance(),
            host.App,
            clock,
            NullLoggerFactory.Instance,
            () => producer,
            _ => consumer);

    /// <summary>
    /// 记录事件 ID 的派发：<c>poison</c> 一律失败，其余成功。
    /// </summary>
    private static MessageDispatch Dispatch(ConcurrentQueue<string> dispatched) => (delivery, _) =>
    {
        dispatched.Enqueue(delivery.Envelope.Id);
        return ValueTask.FromResult(delivery.Envelope.Id == "poison" ? Result.Failure(Error.Literal("ORDERS_VALIDATION_001", "Rejected.")) : Result.Success());
    };

    /// <summary>
    /// 编码一个测试信封。
    /// </summary>
    private static byte[] Encode(string id) => CloudEventsFormat.Encode(new MessageEnvelope { Id = id, Source = "/tests", Type = "orders.placed" });
}
