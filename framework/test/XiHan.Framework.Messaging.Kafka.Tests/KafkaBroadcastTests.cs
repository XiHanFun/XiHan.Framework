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

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// 广播订阅（RFC-0009「订阅方式」，ADR-0079）
/// </summary>
/// <remarks>
/// 打桩的生产者与消费者：广播订阅用本实例独占的消费组、从最新位点开始、不提交位点；投递带广播方式；重试耗尽或无法解码时丢弃，不发布死信。竞争订阅照旧在共用的消费组里、从最早位点开始。不需要 Kafka
/// </remarks>
public sealed class KafkaBroadcastTests
{
    /// <summary>
    /// 测试主题
    /// </summary>
    private const string Topic = "sessions.revoked";

    /// <summary>
    /// 等待条件成立的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 本实例的标识
    /// </summary>
    private readonly MessagingInstance _instance = new(new MessagingOptions());

    [Fact]
    public async Task Subscriptions_GetTheirOwnConsumerGroups_AndStartingPositions()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (producer, _) = FakeKafkaProducer.Create();
        var configs = new ConcurrentQueue<ConsumerConfig>();
        await using var broker = Broker(host, producer, config =>
        {
            configs.Enqueue(config);
            return FakeKafkaConsumer.Create().Consumer;
        });

        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing), new MessageSubscription(Topic, SubscriptionMode.Broadcast)], static (_, _) => ValueTask.FromResult(Result.Success()), CancellationToken.None);

        // 竞争：全部实例共用的消费组、从最早位点开始；广播：本实例独占的消费组、从最新位点开始，都手动提交
        Assert.Equal(
            [("xihan", AutoOffsetReset.Earliest), ($"xihan@{_instance.Id}", AutoOffsetReset.Latest)],
            configs.Select(static c => (c.GroupId, c.AutoOffsetReset ?? AutoOffsetReset.Error)));
        Assert.All(configs, static c => Assert.False(c.EnableAutoCommit));
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);
    }

    [Fact]
    public async Task AConfiguredInstanceId_NamesTheBroadcastConsumerGroup()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (producer, _) = FakeKafkaProducer.Create();
        var configs = new ConcurrentQueue<ConsumerConfig>();
        var instance = new MessagingInstance(new MessagingOptions { InstanceId = "orders-api-0" });
        await using var broker = Broker(host, producer, config =>
        {
            configs.Enqueue(config);
            return FakeKafkaConsumer.Create().Consumer;
        }, instance);

        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Broadcast)], static (_, _) => ValueTask.FromResult(Result.Success()), CancellationToken.None);

        // 配置了标识：广播消费组是 <ConsumerGroup>@<配置值>，每次启动同名；仍从最新位点开始、手动提交（广播不提交），重启不回放
        var config = Assert.Single(configs);
        Assert.Equal("xihan@orders-api-0", config.GroupId);
        Assert.Equal(AutoOffsetReset.Latest, config.AutoOffsetReset);
        Assert.False(config.EnableAutoCommit);
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);
    }

    [Fact]
    public async Task BroadcastDeliveries_AreNotCommitted_AndFailuresAreDroppedWithoutADeadLetter()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (producer, producerFake) = FakeKafkaProducer.Create();
        var (consumer, consumerFake) = FakeKafkaConsumer.Create(Encode("poison"), "not json"u8.ToArray(), Encode("ok"));
        var dispatched = new ConcurrentQueue<(string Id, SubscriptionMode Mode, int Attempt)>();
        await using var broker = Broker(host, producer, _ => consumer);

        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Broadcast)], (delivery, _) =>
        {
            dispatched.Enqueue((delivery.Envelope.Id, delivery.Mode, delivery.Attempt));
            return ValueTask.FromResult(delivery.Envelope.Id == "poison" ? Result.Failure(Error.Literal("REALTIME_UNAVAILABLE_001", "Not yet.")) : Result.Success());
        }, CancellationToken.None);

        Assert.True(SpinWait.SpinUntil(() => dispatched.Any(static d => d.Id == "ok"), _patience));
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

        // 原位重试到上限后丢弃、无法解码的丢弃：都不发布死信；广播不提交位点
        Assert.Equal([("poison", SubscriptionMode.Broadcast, 1), ("poison", SubscriptionMode.Broadcast, 2), ("ok", SubscriptionMode.Broadcast, 1)], dispatched);
        Assert.Empty(producerFake.Topics);
        Assert.Empty(consumerFake.Read(static c => c.Committed.ToList()));
        Assert.Empty(consumerFake.Read(static c => c.Seeks.ToList()));
    }

    /// <summary>
    /// 用打桩的生产者与给定的消费者工厂创建代理
    /// </summary>
    /// <remarks>
    /// <paramref name="instance"/> 为空时用本测试类的实例标识
    /// </remarks>
    private KafkaMessageBroker Broker(TestHost host, IProducer<string?, byte[]> producer, Func<ConsumerConfig, IConsumer<string?, byte[]>> createConsumer, MessagingInstance? instance = null)
        => new(
            new KafkaMessagingOptions { BootstrapServers = "unused:9092" },
            new MessagingOptions { MaxAttempts = 2 },
            instance ?? _instance,
            host.App,
            new FakeTimeProvider(),
            NullLoggerFactory.Instance,
            () => producer,
            createConsumer);

    /// <summary>
    /// 编码一个测试信封
    /// </summary>
    private static byte[] Encode(string id) => CloudEventsFormat.Encode(new MessageEnvelope { Id = id, Source = "/tests", Type = "sessions.revoked" });
}
