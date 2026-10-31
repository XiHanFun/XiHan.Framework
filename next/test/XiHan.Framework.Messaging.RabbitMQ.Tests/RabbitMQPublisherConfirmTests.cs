// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 发布确认（RFC-0009 投递第 6 条）。
/// </summary>
/// <remarks>
/// 对打桩的连接与通道验证：发布通道开启并跟踪确认，发布等到确认才成功，拒绝或退回按失败返回，批量连续发布后一次等待全部确认；重试与死信的重新发布同样等确认，未确认时原消息退回队列而不是被确认掉。不需要真实 RabbitMQ。
/// </remarks>
public sealed class RabbitMQPublisherConfirmTests
{
    /// <summary>
    /// 测试主题。
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 主题对应的队列（缺省前缀 <c>xihan.</c>）。
    /// </summary>
    private const string Queue = "xihan.orders";

    /// <summary>
    /// 等待条件成立的上限。
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 来源 <c>/tests</c>、类型 <c>orders.placed</c> 的测试信封。
    /// </summary>
    private static MessageEnvelope Envelope(string id) => new() { Id = id, Source = "/tests", Type = "orders.placed" };

    /// <summary>
    /// 用打桩连接创建代理。
    /// </summary>
    private static RabbitMQMessageBroker Broker(TestHost host, IConnection connection, int maxAttempts = 3)
        => new(new RabbitMQMessagingOptions { Configuration = "amqp://localhost:5672" }, new MessagingOptions { MaxAttempts = maxAttempts }, host.App, NullLoggerFactory.Instance, _ => Task.FromResult(connection));

    [Fact]
    public async Task Connect_OpensThePublishChannelWithTrackedPublisherConfirmations()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection);

        await broker.ConnectAsync(CancellationToken.None);

        var options = Assert.Single(fake.Channels).Options;
        Assert.NotNull(options);
        Assert.True(options.PublisherConfirmationsEnabled);
        Assert.True(options.PublisherConfirmationTrackingEnabled);
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
        Assert.Equal(Queue, published.RoutingKey);
        Assert.True(published.Mandatory);
        Assert.Equal(1, published.Attempt);

        channel.ReleaseConfirms();
        Assert.True((await publishing).IsSuccess);
    }

    [Fact]
    public async Task Publish_FailsAsUnavailable_WhenTheBrokerNacksOrReturnsTheMessage()
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
        Assert.Equal(MessagingErrors.BrokerUnavailable, returned.Error?.Code);
        Assert.Contains("could not be routed", Assert.IsType<string>(returned.Error?.Details?["reason"]), StringComparison.Ordinal);
        Assert.True(confirmed.IsSuccess);
        // 退回说明队列已不在：声明缓存被清掉，下一次发布重新声明
        Assert.Equal([Queue, Queue], Assert.Single(fake.Channels).Declared);
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
        Assert.Equal([Queue], channel.Declared);

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
        Assert.Equal(2, Assert.Single(fake.Channels).Published.Count);
    }

    [Fact]
    public async Task Retry_AcksTheOriginalOnlyAfterTheRepublishIsConfirmed()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection);
        await broker.StartAsync([Topic], static (_, _) => ValueTask.FromResult<Result>(Error.Literal("ORDERS_VALIDATION_001", "Rejected.")), CancellationToken.None);
        var publish = fake.Channels[0];
        var consumer = fake.Channels[1];

        await DeliverAsync(consumer, deliveryTag: 7, attempt: 1, CloudEventsFormat.Encode(Envelope("retry")));

        var republished = Assert.Single(publish.Published);
        Assert.Equal(Queue, republished.RoutingKey);
        Assert.Equal(2, republished.Attempt);
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
        await broker.StartAsync([Topic], static (_, _) => ValueTask.FromResult<Result>(Error.Literal("ORDERS_VALIDATION_001", "Rejected.")), CancellationToken.None);
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
        await broker.StartAsync([Topic], static (_, _) => ValueTask.FromResult<Result>(Error.Literal("ORDERS_VALIDATION_001", "Rejected.")), CancellationToken.None);
        var consumer = fake.Channels[1];

        // 第 2 次尝试失败即到上限：转入死信队列，死信发布未被确认时原消息退回
        await DeliverAsync(consumer, deliveryTag: 9, attempt: 2, CloudEventsFormat.Encode(Envelope("poison")));
        Assert.Equal(Queue + ".dlq", Assert.Single(fake.Channels[0].Published).RoutingKey);
        Assert.Empty(consumer.Acked);
        Assert.Equal([(9UL, true)], consumer.Nacked);

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
        await broker.StartAsync([Topic], static (_, _) => ValueTask.FromResult(Result.Success()), CancellationToken.None);
        var consumer = fake.Channels[1];

        await DeliverAsync(consumer, deliveryTag: 11, attempt: 1, CloudEventsFormat.Encode(Envelope("ok")));

        Assert.Empty(fake.Channels[0].Published);
        Assert.Equal([11UL], consumer.Acked);
        Assert.Empty(consumer.Nacked);
    }

    /// <summary>
    /// 让打桩通道上的消费者收到一条投递，等处理完成。
    /// </summary>
    private static async Task DeliverAsync(FakeRabbitChannel channel, ulong deliveryTag, int attempt, byte[] body)
    {
        var consumer = Assert.IsAssignableFrom<IAsyncBasicConsumer>(channel.Consumer);
        var properties = new BasicProperties { Headers = new Dictionary<string, object?>(StringComparer.Ordinal) { ["x-xihan-attempt"] = attempt } };
        await consumer.HandleBasicDeliverAsync("consumer-" + channel.Number, deliveryTag, redelivered: false, string.Empty, Queue, properties, body, CancellationToken.None);
    }
}
