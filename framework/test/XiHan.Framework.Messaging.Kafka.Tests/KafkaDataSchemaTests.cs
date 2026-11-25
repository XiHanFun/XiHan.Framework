// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
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
/// Kafka 的发布、投递与死信都原样带着信封的 <c>dataschema</c>
/// </summary>
/// <remarks>
/// 它是消息值里结构化 JSON 的成员。打桩的生产者与消费者，不需要 Kafka（RFC-0009「信封」各传输上的 <c>dataschema</c>，ADR-0305）。门控的 <c>KafkaMessagingConformanceTests</c> 用真实服务跑同一件事，CI 没有 Kafka，这里兜住映射本身
/// </remarks>
public sealed class KafkaDataSchemaTests
{
    /// <summary>
    /// 测试主题
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 测试用的数据模式
    /// </summary>
    private const string Schema = "https://example.com/schemas/order.json";

    /// <summary>
    /// 等待条件成立的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Publish_WritesTheDataSchema_IntoTheStructuredDocument_NotIntoTheHeaders()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (producer, producerFake) = FakeKafkaProducer.Create();
        var produced = new ConcurrentQueue<Message<string?, byte[]>>();
        producerFake.Respond = (_, topic, message) =>
        {
            produced.Enqueue(message);
            return FakeKafkaProducer.Report(topic, message, PersistenceStatus.Persisted);
        };
        var (consumer, _) = FakeKafkaConsumer.Create();
        await using var broker = Broker(host, producer, consumer);

        // 单条发布走 ProduceAsync（打桩的生产者只实现了它）；批量发布写的是同一份消息值
        Assert.True((await broker.PublishAsync(Topic, Envelope("with") with { DataSchema = Schema }, CancellationToken.None)).IsSuccess);
        Assert.True((await broker.PublishAsync(Topic, Envelope("without"), CancellationToken.None)).IsSuccess);

        var messages = produced.ToArray();
        Assert.Equal(2, messages.Length);
        using (var with = JsonDocument.Parse(messages[0].Value))
        {
            Assert.Equal(Schema, with.RootElement.GetProperty("dataschema").GetString());
        }

        using (var without = JsonDocument.Parse(messages[1].Value))
        {
            Assert.False(without.RootElement.TryGetProperty("dataschema", out _));
        }

        // 消息头只有内容类型与尝试次数：属性都在消息值里，不另写 ce-* 头
        Assert.All(messages, static m => Assert.DoesNotContain(m.Headers, static h => h.Key.StartsWith("ce-", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(Schema, CloudEventsFormat.Decode(messages[0].Value).ValueOrThrow().DataSchema);
    }

    [Fact]
    public async Task Delivery_HandsTheDataSchemaToTheHandler_IncludingFromAStandardEventWrittenElsewhere()
    {
        // 别的 CloudEvents 实现写的标准事件：ADR-0298 时带 dataschema 的这份文档无法解码、进死信
        var external = Encoding.UTF8.GetBytes("{\"specversion\":\"1.0\",\"id\":\"external\",\"source\":\"/billing\",\"type\":\"orders.placed\",\"dataschema\":\"urn:example:schemas:order:1\",\"data\":{\"total\":1}}");
        await using var host = await TestHost.StartAsync(static _ => { });
        var (producer, producerFake) = FakeKafkaProducer.Create();
        var (consumer, _) = FakeKafkaConsumer.Create(CloudEventsFormat.Encode(Envelope("own") with { DataSchema = Schema }), external);
        var received = new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);
        await using var broker = Broker(host, producer, consumer);

        await broker.StartAsync(
            [new MessageSubscription(Topic, SubscriptionMode.Competing)],
            (delivery, _) =>
            {
                received[delivery.Envelope.Id] = delivery.Envelope.DataSchema;
                return ValueTask.FromResult(Result.Success());
            },
            CancellationToken.None);
        Assert.True(SpinWait.SpinUntil(() => received.Count == 2, _patience));
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

        Assert.Equal(Schema, received["own"]);
        Assert.Equal("urn:example:schemas:order:1", received["external"]);
        Assert.Empty(producerFake.Topics);
    }

    [Fact]
    public async Task DeadLetter_KeepsTheDataSchema()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (producer, producerFake) = FakeKafkaProducer.Create();
        var deadLetters = new ConcurrentQueue<byte[]>();
        producerFake.Respond = (_, topic, message) =>
        {
            deadLetters.Enqueue(message.Value);
            return FakeKafkaProducer.Report(topic, message, PersistenceStatus.Persisted);
        };
        var (consumer, _) = FakeKafkaConsumer.Create(CloudEventsFormat.Encode(Envelope("poison") with { DataSchema = Schema }));
        await using var broker = Broker(host, producer, consumer);

        await broker.StartAsync(
            [new MessageSubscription(Topic, SubscriptionMode.Competing)],
            static (_, _) => ValueTask.FromResult(Result.Failure(Error.Literal("ORDERS_VALIDATION_001", "Rejected."))),
            CancellationToken.None);
        Assert.True(SpinWait.SpinUntil(() => !deadLetters.IsEmpty, _patience));
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

        // 用完尝试次数后原信封进死信主题，数据模式不丢
        Assert.Equal(["xihan.orders.dlq"], producerFake.Topics);
        Assert.Equal(Schema, CloudEventsFormat.Decode(Assert.Single(deadLetters)).ValueOrThrow().DataSchema);
    }

    /// <summary>
    /// 用打桩的生产者与消费者创建代理：最多投递 2 次
    /// </summary>
    private static KafkaMessageBroker Broker(TestHost host, IProducer<string?, byte[]> producer, IConsumer<string?, byte[]> consumer)
        => new(
            new KafkaMessagingOptions { BootstrapServers = "unused:9092" },
            new MessagingOptions { MaxAttempts = 2 },
            new MessagingInstance(new MessagingOptions()),
            host.App,
            new FakeTimeProvider(),
            NullLoggerFactory.Instance,
            () => producer,
            _ => consumer);

    /// <summary>
    /// 一个测试信封
    /// </summary>
    private static MessageEnvelope Envelope(string id) => new() { Id = id, Source = "/tests", Type = "orders.placed" };
}
