// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 广播订阅（RFC-0009「订阅方式」，ADR-0079）。
/// </summary>
/// <remarks>
/// 对打桩的连接与通道验证：广播订阅声明本实例独占、非持久、随连接删除的队列并以主题的路由键绑定；投递带广播方式；重试定向发回本实例的队列；到上限或无法解码时确认掉、不进死信。不需要真实 RabbitMQ。
/// </remarks>
public sealed class RabbitMQBroadcastTests
{
    /// <summary>
    /// 测试主题。
    /// </summary>
    private const string Topic = "sessions.revoked";

    /// <summary>
    /// 主题的路由键（也是竞争订阅的队列）。
    /// </summary>
    private const string RoutingKey = "xihan.sessions.revoked";

    /// <summary>
    /// 本实例的标识。
    /// </summary>
    private readonly MessagingInstance _instance = new(new MessagingOptions());

    /// <summary>
    /// 本实例的广播队列。
    /// </summary>
    private string BroadcastQueue => RoutingKey + "@" + _instance.Id;

    [Fact]
    public async Task Start_DeclaresAnExclusiveQueuePerInstance_BoundWithTheTopicsRoutingKey()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection);

        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing), new MessageSubscription(Topic, SubscriptionMode.Broadcast)], static (_, _) => ValueTask.FromResult(Result.Success()), CancellationToken.None);

        // 竞争：持久队列，同名路由键；广播：本实例独占、非持久、随连接删除的队列，绑定主题的路由键——交换机给两个队列各一份
        Assert.Equal([new QueueDeclaration(RoutingKey, Durable: true, Exclusive: false, AutoDelete: false)], fake.Channels[1].Declared);
        Assert.Equal([(RoutingKey, "xihan", RoutingKey)], fake.Channels[1].Bindings);
        Assert.Equal([new QueueDeclaration(BroadcastQueue, Durable: false, Exclusive: true, AutoDelete: true)], fake.Channels[2].Declared);
        Assert.Equal([(BroadcastQueue, "xihan", RoutingKey)], fake.Channels[2].Bindings);
        Assert.Matches(@"^xihan\.sessions\.revoked@[a-z0-9-]+-[0-9]+-[0-9a-f]{8}$", BroadcastQueue);
    }

    [Fact]
    public async Task AConfiguredInstanceId_NamesTheBroadcastQueue()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection, new MessagingInstance(new MessagingOptions { InstanceId = "orders-api-0" }));

        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Broadcast)], static (_, _) => ValueTask.FromResult(Result.Success()), CancellationToken.None);

        // 配置了标识：广播队列是 <QueuePrefix><topic>@<配置值>，仍是本实例独占、非持久、随连接删除的队列
        Assert.Equal([new QueueDeclaration(RoutingKey + "@orders-api-0", Durable: false, Exclusive: true, AutoDelete: true)], fake.Channels[1].Declared);
        Assert.Equal([(RoutingKey + "@orders-api-0", "xihan", RoutingKey)], fake.Channels[1].Bindings);
    }

    [Fact]
    public async Task BroadcastDelivery_CarriesTheMode_AndRetriesGoBackToTheInstanceQueueOnly()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection);
        var modes = new List<SubscriptionMode>();
        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Broadcast)], (delivery, _) =>
        {
            modes.Add(delivery.Mode);
            return ValueTask.FromResult<Result>(Error.Literal("REALTIME_UNAVAILABLE_001", "Not yet."));
        }, CancellationToken.None);
        var consumer = fake.Channels[1];

        await DeliverAsync(consumer, deliveryTag: 3, attempt: 1, CloudEventsFormat.Encode(Envelope("revoke-1")));

        // 重试经默认交换机定向发回本实例的广播队列：经交换机会让别的实例再收一遍
        Assert.Equal([SubscriptionMode.Broadcast], modes);
        var republished = Assert.Single(fake.Channels[0].Published);
        Assert.Equal((string.Empty, BroadcastQueue, 2), (republished.Exchange, republished.RoutingKey, republished.Attempt));
        Assert.Equal([3UL], consumer.Acked);
    }

    [Fact]
    public async Task BroadcastDelivery_IsDroppedAtTheLastAttempt_AndUndecodableBroadcastsAreDropped_WithoutDeadLetters()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection);
        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Broadcast)], static (_, _) => ValueTask.FromResult<Result>(Error.Literal("REALTIME_UNAVAILABLE_001", "Not yet.")), CancellationToken.None);
        var consumer = fake.Channels[1];

        await DeliverAsync(consumer, deliveryTag: 4, attempt: 2, CloudEventsFormat.Encode(Envelope("revoke-2")));
        await DeliverAsync(consumer, deliveryTag: 5, attempt: 1, "not json"u8.ToArray());

        // 广播没有死信：到上限、无法解码都确认掉，不发布、不声明死信队列
        Assert.Equal([4UL, 5UL], consumer.Acked);
        Assert.Empty(consumer.Nacked);
        Assert.Empty(fake.Channels[0].Published);
        Assert.Empty(fake.Channels[0].Declared);
    }

    /// <summary>
    /// 来源 <c>/tests</c>、类型 <c>sessions.revoked</c> 的测试信封。
    /// </summary>
    private static MessageEnvelope Envelope(string id) => new() { Id = id, Source = "/tests", Type = "sessions.revoked" };

    /// <summary>
    /// 用打桩连接创建代理。
    /// </summary>
    /// <remarks>
    /// <paramref name="instance"/> 为空时用本测试类的实例标识。
    /// </remarks>
    private RabbitMQMessageBroker Broker(TestHost host, IConnection connection, MessagingInstance? instance = null)
        => new(new RabbitMQMessagingOptions { Configuration = "amqp://localhost:5672" }, new MessagingOptions { MaxAttempts = 2 }, instance ?? _instance, host.App, NullLoggerFactory.Instance, _ => Task.FromResult(connection));

    /// <summary>
    /// 让打桩通道上的消费者收到一条投递，等处理完成。
    /// </summary>
    private async Task DeliverAsync(FakeRabbitChannel channel, ulong deliveryTag, int attempt, byte[] body)
    {
        var consumer = Assert.IsAssignableFrom<IAsyncBasicConsumer>(channel.Consumer);
        var properties = new BasicProperties { Headers = new Dictionary<string, object?>(StringComparer.Ordinal) { ["x-xihan-attempt"] = attempt } };
        await consumer.HandleBasicDeliverAsync("consumer-" + channel.Number, deliveryTag, redelivered: false, "xihan", BroadcastQueue, properties, body, CancellationToken.None);
    }
}
