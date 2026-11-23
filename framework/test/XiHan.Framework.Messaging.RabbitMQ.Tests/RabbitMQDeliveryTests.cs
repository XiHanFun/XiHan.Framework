// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using XiHan.Framework.Kernel;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.RabbitMQ.Tests;

/// <summary>
/// 停止与排空期间的投递不被了结（RFC-0009「投递」第 3、5 条，ADR-0261）
/// </summary>
/// <remarks>
/// 对打桩的连接与通道验证：处理器被停止取消后经管道返回失败结果时不确认、不退回、不重新发布，也不记进死信或丢弃的日志；应用排空期间到达的投递不派发，停止后仍没有了结。不需要真实 RabbitMQ
/// </remarks>
public sealed class RabbitMQDeliveryTests
{
    /// <summary>
    /// 测试主题
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 等待条件成立的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 来源 <c>/tests</c>、类型 <c>orders.placed</c> 的测试信封
    /// </summary>
    private static MessageEnvelope Envelope(string id) => new() { Id = id, Source = "/tests", Type = "orders.placed" };

    [Theory]
    [InlineData(SubscriptionMode.Competing, 1)]
    [InlineData(SubscriptionMode.Competing, 2)]
    [InlineData(SubscriptionMode.Broadcast, 1)]
    [InlineData(SubscriptionMode.Broadcast, 2)]
    public async Task HandlerCancelledByStop_LeavesTheDeliveryUnsettled(SubscriptionMode mode, int attempt)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logs = new LogCapture();
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection, logs);
        var cancelled = false;
        await broker.StartAsync(
            [new MessageSubscription(Topic, mode)],
            async (_, cancellationToken) =>
            {
                // 处理进行中宿主停止：停止信号取消了处理器，管道把取消写成失败结果返回，不抛异常
                await broker.StopAsync(CancellationToken.None);
                cancelled = cancellationToken.IsCancellationRequested;
                return Result.Failure(new Error(KernelErrors.Cancelled, KernelMessages.Cancelled));
            },
            CancellationToken.None);
        var consumer = fake.Channels[1];

        await DeliverAsync(consumer, deliveryTag: 21, attempt, CloudEventsFormat.Encode(Envelope("slow")));

        // 第 2 次是最后一次尝试：既不确认（广播原先在这里被确认掉）、不退回，也不重新发布或进死信；连接关闭后由 RabbitMQ 重新投递
        Assert.True(cancelled);
        Assert.Empty(consumer.Acked);
        Assert.Empty(consumer.Nacked);
        Assert.Empty(fake.Channels[0].Published);
        Assert.Empty(fake.Channels[0].Declared);
        Assert.DoesNotContain(logs.Entries, static e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task DeliveryArrivingWhileTheAppDrains_IsNotDispatched_AndStaysUnsettledOnStop()
    {
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // 特性停在闸门前，应用就一直处在排空中：代理的停止信号还没到
        var probe = new StubFeature(TestManifests.Create("xihan.test.messaging-draining"))
        {
            OnStop = async _ =>
            {
                stopEntered.SetResult();
                await stopGate.Task;
            },
        };
        await using var host = await TestHost.StartAsync(b => b.UseFeature(probe));
        var (connection, fake) = FakeRabbitConnection.Create();
        await using var broker = Broker(host, connection, new LogCapture());
        var dispatched = 0;
        await broker.StartAsync(
            [new MessageSubscription(Topic, SubscriptionMode.Competing)],
            (_, _) =>
            {
                Interlocked.Increment(ref dispatched);
                return ValueTask.FromResult(Result.Success());
            },
            CancellationToken.None);
        var consumer = fake.Channels[1];
        var draining = host.App.StopAsync(CancellationToken.None);
        try
        {
            await stopEntered.Task.WaitAsync(_patience);

            var delivering = DeliverAsync(consumer, deliveryTag: 22, attempt: 1, CloudEventsFormat.Encode(Envelope("held")));
            await Task.Delay(100);
            Assert.False(delivering.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref dispatched));

            await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);
            // 消费回调随停止结束；投递没有派发也没有确认，连接关闭后由 RabbitMQ 重新投递
            await delivering.WaitAsync(_patience);
            Assert.Equal(0, dispatched);
            Assert.Empty(consumer.Acked);
            Assert.Empty(consumer.Nacked);
        }
        finally
        {
            // 断言失败也要放开闸门，否则宿主停不下来
            stopGate.TrySetResult();
            await draining.WaitAsync(_patience);
        }
    }

    /// <summary>
    /// 用打桩连接创建代理：最多投递 2 次，日志写进给定的工厂
    /// </summary>
    private static RabbitMQMessageBroker Broker(TestHost host, IConnection connection, ILoggerFactory logs)
        => new(new RabbitMQMessagingOptions { Configuration = "amqp://localhost:5672" }, new MessagingOptions { MaxAttempts = 2 }, new MessagingInstance(new MessagingOptions()), host.App, logs, _ => Task.FromResult(connection));

    /// <summary>
    /// 让打桩通道上的消费者收到一条投递，等处理完成
    /// </summary>
    private static async Task DeliverAsync(FakeRabbitChannel channel, ulong deliveryTag, int attempt, byte[] body)
    {
        var consumer = Assert.IsAssignableFrom<IAsyncBasicConsumer>(channel.Consumer);
        var properties = new BasicProperties { Headers = new Dictionary<string, object?>(StringComparer.Ordinal) { ["x-xihan-attempt"] = attempt } };
        await consumer.HandleBasicDeliverAsync("consumer-" + channel.Number, deliveryTag, redelivered: false, string.Empty, "xihan." + Topic, properties, body, CancellationToken.None);
    }
}
