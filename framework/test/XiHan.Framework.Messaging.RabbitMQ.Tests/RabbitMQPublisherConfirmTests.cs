// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 发布确认（RFC-0009 投递第 6 条）
/// </summary>
/// <remarks>
/// 对打桩的连接与通道验证：发布通道开启并跟踪确认，发布方只声明交换机、经它按路由键发布，等到确认才成功，拒绝按失败返回，退回（没有订阅者）丢弃并成功，批量连续发布后一次等待全部确认；订阅方声明并绑定队列；重试与死信经默认交换机定向重新发布，同样等确认，未确认时原消息退回队列而不是被确认掉。不需要真实 RabbitMQ
/// </remarks>
public sealed class RabbitMQPublisherConfirmTests
{
    /// <summary>
    /// 测试主题
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 主题对应的队列与路由键（缺省前缀 <c>xihan.</c>）
    /// </summary>
    private const string Queue = "xihan.orders";

    /// <summary>
    /// 缺省的交换机
    /// </summary>
    private const string Exchange = "xihan";

    /// <summary>
    /// 等待条件成立的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 来源 <c>/tests</c>、类型 <c>orders.placed</c> 的测试信封
    /// </summary>
    private static MessageEnvelope Envelope(string id) => new() { Id = id, Source = "/tests", Type = "orders.placed" };

    /// <summary>
    /// 用打桩连接创建代理
    /// </summary>
    private static RabbitMQMessageBroker Broker(TestHost host, IConnection connection, int maxAttempts = 3)
        => new(new RabbitMQMessagingOptions { Configuration = "amqp://localhost:5672" }, new MessagingOptions { MaxAttempts = maxAttempts }, new MessagingInstance(new MessagingOptions()), host.App, NullLoggerFactory.Instance, _ => Task.FromResult(connection));

    [Fact]
    public async Task Connect_OpensThePublishChannelWithTrackedPublisherConfirmations()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection);

        await broker.ConnectAsync(CancellationToken.None);

        var channel = Assert.Single(fake.Channels);
        Assert.NotNull(channel.Options);
        Assert.True(channel.Options.PublisherConfirmationsEnabled);
        Assert.True(channel.Options.PublisherConfirmationTrackingEnabled);
        // 发布方只声明持久的 direct 交换机，不声明队列
        Assert.Equal([(Exchange, ExchangeType.Direct, true)], channel.Exchanges);
        Assert.Empty(channel.Declared);
    }

    [Fact]
    public async Task Publish_SucceedsOnlyAfterTheBrokerConfirms()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        fake.OnChannel = static channel => channel.HoldConfirms = true;
        await using var broker = Broker(host, connection);

        var publishing = broker.PublishAsync(Topic, Envelope("1"), CancellationToken.None).AsTask();
        var channel = Assert.Single(fake.Channels);
        Assert.True(SpinWait.SpinUntil(() => channel.Published.Count == 1, _patience));

        // 消息已写出、确认未到：发布还没有完成
        Assert.False(publishing.IsCompleted);
        var published = Assert.Single(channel.Published);
        Assert.Equal(Exchange, published.Exchange);
        Assert.Equal(Queue, published.RoutingKey);
        Assert.True(published.Mandatory);
        Assert.Equal(1, published.Attempt);
        Assert.Empty(channel.Declared);

        channel.ReleaseConfirms();
        Assert.True((await publishing).IsSuccess);
    }

    [Fact]
    public async Task Publish_FailsAsUnavailable_WhenTheBrokerNacks_AndSucceedsWhenNothingIsBound()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        fake.OnChannel = static channel => channel.Confirm = static (sequence, _) => sequence switch
        {
            0 => new PublishException(1, isReturn: false),
            1 => new PublishException(2, isReturn: true),
            _ => null,
        };
        await using var broker = Broker(host, connection);

        var nacked = await broker.PublishAsync(Topic, Envelope("nacked"), CancellationToken.None);
        var returned = await broker.PublishAsync(Topic, Envelope("returned"), CancellationToken.None);
        var confirmed = await broker.PublishAsync(Topic, Envelope("confirmed"), CancellationToken.None);

        Assert.Equal(MessagingErrors.BrokerUnavailable, nacked.Error?.Code);
        Assert.Equal(MessagingMessages.BrokerUnavailable, nacked.Error?.MessageKey);
        Assert.Contains("did not confirm", Assert.IsType<string>(nacked.Error?.Details?["reason"]), StringComparison.Ordinal);
        // 退回：路由键上没有队列绑定，即没有订阅者——丢弃并成功，与内存代理一致，发件箱不会为它重试或记死信
        Assert.True(returned.IsSuccess, returned.ToString());
        Assert.True(confirmed.IsSuccess);
        // 发布方从不声明队列：退回之后也不会补建一个没人消费的队列
        Assert.Empty(Assert.Single(fake.Channels).Declared);
    }

    [Fact]
    public async Task PublishMany_DropsTheReturnedItems_AndReportsTheRestAsConfirmed()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        fake.OnChannel = static channel => channel.Confirm = static (sequence, _) => sequence is 0 or 2 ? new PublishException((ulong)sequence + 1, isReturn: true) : null;
        await using var broker = Broker(host, connection);

        var results = await broker.PublishManyAsync("scheduling.job.started", [Envelope("a"), Envelope("b"), Envelope("c")], CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.All(results, static r => Assert.True(r.IsSuccess, r.ToString()));
        var channel = Assert.Single(fake.Channels);
        Assert.All(channel.Published, static p => Assert.Equal(("xihan", "xihan.scheduling.job.started"), (p.Exchange, p.RoutingKey)));
        Assert.Empty(channel.Declared);
    }

    [Fact]
    public async Task Start_DeclaresTheDurableQueue_AndBindsItToTheExchange()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection);

        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing)], static (_, _) => ValueTask.FromResult(Result.Success()), CancellationToken.None);

        // 队列只由订阅方声明：持久、非独占、不自动删除，以同名路由键绑定到交换机
        var consumer = fake.Channels[1];
        Assert.Equal([new QueueDeclaration(Queue, Durable: true, Exclusive: false, AutoDelete: false)], consumer.Declared);
        Assert.Equal([(Queue, Exchange, Queue)], consumer.Bindings);
        Assert.Empty(fake.Channels[0].Declared);
    }

    [Fact]
    public async Task PublishMany_PublishesTheWholeBatchThenWaitsOnceForConfirms_AndReportsEachItem()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        fake.OnChannel = static channel =>
        {
            channel.HoldConfirms = true;
            channel.Confirm = static (sequence, _) => sequence == 1 ? new PublishException(2, isReturn: false) : null;
        };
        await using var broker = Broker(host, connection);

        var publishing = broker.PublishManyAsync(Topic, [Envelope("a"), Envelope("b"), Envelope("c")], CancellationToken.None).AsTask();
        var channel = Assert.Single(fake.Channels);

        // 三条都已写出才开始等确认，确认未到前批量发布不完成
        Assert.True(SpinWait.SpinUntil(() => channel.Published.Count == 3, _patience));
        Assert.False(publishing.IsCompleted);
        Assert.All(channel.Published, static p => Assert.True(p.Mandatory));
        Assert.Empty(channel.Declared);

        channel.ReleaseConfirms();
        var results = await publishing;
        Assert.Equal(3, results.Count);
        Assert.True(results[0].IsSuccess);
        Assert.Equal(MessagingErrors.BrokerUnavailable, results[1].Error?.Code);
        Assert.True(results[2].IsSuccess);
    }

    [Fact]
    public async Task PublishMany_ThrowsUnavailable_WhenTheChannelFailsWhileWaitingForConfirms()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        fake.OnChannel = static channel => channel.Confirm = static (sequence, _) => sequence == 0 ? new IOException("connection reset") : null;
        await using var broker = Broker(host, connection);

        var exception = await Assert.ThrowsAsync<FrameworkException>(async () => await broker.PublishManyAsync(Topic, [Envelope("a"), Envelope("b")], CancellationToken.None));

        Assert.Equal(MessagingErrors.BrokerUnavailable, exception.Code);
        // 框架异常的消息会进发件箱行（ADR-0108）：只写框架自己的文字，代理的异常放在内层
        Assert.DoesNotContain("connection reset", exception.Message, StringComparison.Ordinal);
        Assert.IsType<IOException>(exception.InnerException);
        Assert.Equal(2, Assert.Single(fake.Channels).Published.Count);
    }

    [Fact]
    public async Task Retry_AcksTheOriginalOnlyAfterTheRepublishIsConfirmed()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection);
        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing)], static (_, _) => ValueTask.FromResult<Result>(Error.Literal("ORDERS_VALIDATION_001", "Rejected.")), CancellationToken.None);
        var publish = fake.Channels[0];
        var consumer = fake.Channels[1];

        await DeliverAsync(consumer, deliveryTag: 7, attempt: 1, CloudEventsFormat.Encode(Envelope("retry")));

        // 重试经默认交换机定向发回所消费的队列，不经主题的交换机，也不重新声明队列
        var republished = Assert.Single(publish.Published);
        Assert.Equal(string.Empty, republished.Exchange);
        Assert.Equal(Queue, republished.RoutingKey);
        Assert.Equal(2, republished.Attempt);
        Assert.Empty(publish.Declared);
        Assert.Equal([7UL], consumer.Acked);
        Assert.Empty(consumer.Nacked);
    }

    [Fact]
    public async Task Retry_ReturnsTheOriginalToTheQueue_WhenTheRepublishIsNotConfirmed()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        fake.OnChannel = static channel => channel.Confirm = static (_, _) => new PublishException(1, isReturn: false);
        await using var broker = Broker(host, connection);
        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing)], static (_, _) => ValueTask.FromResult<Result>(Error.Literal("ORDERS_VALIDATION_001", "Rejected.")), CancellationToken.None);
        var consumer = fake.Channels[1];

        await DeliverAsync(consumer, deliveryTag: 8, attempt: 1, CloudEventsFormat.Encode(Envelope("retry")));

        // 重新发布没被确认：原消息不能确认掉（那样就丢了），退回队列由 RabbitMQ 再投递
        Assert.Single(fake.Channels[0].Published);
        Assert.Empty(consumer.Acked);
        Assert.Equal([(8UL, true)], consumer.Nacked);
    }

    [Fact]
    public async Task DeadLetter_ReturnsTheOriginalToTheQueue_WhenTheDeadLetterPublishIsNotConfirmed()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        fake.OnChannel = static channel => channel.Confirm = static (_, routingKey) => routingKey.EndsWith(".dlq", StringComparison.Ordinal) ? new PublishException(1, isReturn: false) : null;
        await using var broker = Broker(host, connection, maxAttempts: 2);
        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing)], static (_, _) => ValueTask.FromResult<Result>(Error.Literal("ORDERS_VALIDATION_001", "Rejected.")), CancellationToken.None);
        var consumer = fake.Channels[1];

        // 第 2 次尝试失败即到上限：转入死信队列，死信发布未被确认时原消息退回
        await DeliverAsync(consumer, deliveryTag: 9, attempt: 2, CloudEventsFormat.Encode(Envelope("poison")));
        var deadLettered = Assert.Single(fake.Channels[0].Published);
        Assert.Equal((string.Empty, Queue + ".dlq"), (deadLettered.Exchange, deadLettered.RoutingKey));
        Assert.Empty(consumer.Acked);
        Assert.Equal([(9UL, true)], consumer.Nacked);
        // 死信队列由死信的一方声明为持久队列并以同名路由键绑定（即主题加后缀的竞争队列）
        Assert.Equal([new QueueDeclaration(Queue + ".dlq", Durable: true, Exclusive: false, AutoDelete: false)], fake.Channels[0].Declared);
        Assert.Equal([(Queue + ".dlq", Exchange, Queue + ".dlq")], fake.Channels[0].Bindings);

        // 无法解码的消息同样经确认的死信发布，未确认时退回
        await DeliverAsync(consumer, deliveryTag: 10, attempt: 1, "not json"u8.ToArray());
        Assert.Equal(2, fake.Channels[0].Published.Count);
        Assert.Equal([(9UL, true), (10UL, true)], consumer.Nacked);
        Assert.Empty(consumer.Acked);
    }

    [Fact]
    public async Task Success_AcksTheDelivery_WithoutPublishing()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection);
        await broker.StartAsync([new MessageSubscription(Topic, SubscriptionMode.Competing)], static (_, _) => ValueTask.FromResult(Result.Success()), CancellationToken.None);
        var consumer = fake.Channels[1];

        await DeliverAsync(consumer, deliveryTag: 11, attempt: 1, CloudEventsFormat.Encode(Envelope("ok")));

        Assert.Empty(fake.Channels[0].Published);
        Assert.Equal([11UL], consumer.Acked);
        Assert.Empty(consumer.Nacked);
    }

    /// <summary>
    /// 让打桩通道上的消费者收到一条投递，等处理完成
    /// </summary>
    private static async Task DeliverAsync(FakeRabbitChannel channel, ulong deliveryTag, int attempt, byte[] body)
    {
        var consumer = Assert.IsAssignableFrom<IAsyncBasicConsumer>(channel.Consumer);
        var properties = new BasicProperties { Headers = new Dictionary<string, object?>(StringComparer.Ordinal) { ["x-xihan-attempt"] = attempt } };
        await consumer.HandleBasicDeliverAsync("consumer-" + channel.Number, deliveryTag, redelivered: false, string.Empty, Queue, properties, body, CancellationToken.None);
    }
}
