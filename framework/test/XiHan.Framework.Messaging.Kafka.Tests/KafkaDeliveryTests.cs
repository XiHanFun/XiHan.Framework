// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Kernel;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Error = XiHan.Framework.Primitives.Error;

namespace XiHan.Framework.Messaging.Kafka.Tests;

/// <summary>
/// 停止与排空期间的投递不被记成失败，无法解码的消息按订阅方式了结
/// </summary>
/// <remarks>
/// 打桩的生产者与消费者：处理器被停止取消后经管道返回失败结果时不原位重试、不发布死信、不提交位点；应用排空期间不接着原位重试（RFC-0009「流」第 2 条、「投递」第 5 条，ADR-0261）；无法解码的消息竞争订阅发布到死信主题、广播订阅丢弃（ADR-0275）。不需要 Kafka
/// </remarks>
public sealed class KafkaDeliveryTests
{
    /// <summary>
    /// 测试主题
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 等待条件成立的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(SubscriptionMode.Competing)]
    [InlineData(SubscriptionMode.Broadcast)]
    public async Task HandlerCancelledByStop_IsNotRetried_DeadLettered_OrCommitted(SubscriptionMode mode)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (producer, producerFake) = FakeKafkaProducer.Create();
        var (consumer, consumerFake) = FakeKafkaConsumer.Create(Encode("slow"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatched = 0;
        await using var broker = Broker(host, producer, consumer);

        await broker.StartAsync(
            [new MessageSubscription(Topic, mode)],
            async (_, cancellationToken) =>
            {
                Interlocked.Increment(ref dispatched);
                entered.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // 管道把处理器的取消写成失败结果返回，不抛异常
                }

                return Result.Failure(new Error(KernelErrors.Cancelled, KernelMessages.Cancelled));
            },
            CancellationToken.None);
        await entered.Task.WaitAsync(_patience);
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

        // 停止打断的投递没有完成：不发布死信，不提交位点，也不在原位把尝试次数用完，重启后重新投递
        Assert.Empty(producerFake.Topics);
        Assert.Empty(consumerFake.Read(static c => c.Committed.ToList()));
        Assert.Equal(1, dispatched);
        Assert.True(consumerFake.Closed);
    }

    [Fact]
    public async Task WhileTheAppDrains_AFailedAttemptIsNotRetriedInPlace()
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
        var (producer, producerFake) = FakeKafkaProducer.Create();
        var (consumer, consumerFake) = FakeKafkaConsumer.Create(Encode("poison"));
        var dispatched = 0;
        Task? draining = null;
        await using var broker = Broker(host, producer, consumer);
        try
        {
            await broker.StartAsync(
                [new MessageSubscription(Topic, SubscriptionMode.Competing)],
                async (_, _) =>
                {
                    // 第一次尝试进行中应用开始排空，随后处理器返回失败
                    if (Interlocked.Increment(ref dispatched) == 1)
                    {
                        draining = host.App.StopAsync(CancellationToken.None);
                        await stopEntered.Task;
                    }

                    return Result.Failure(Error.Literal("ORDERS_VALIDATION_001", "Rejected."));
                },
                CancellationToken.None);
            await stopEntered.Task.WaitAsync(_patience);
            await Task.Delay(300);

            // 排空中不接着原位重试：管道此时只会拒绝调用，空转会把尝试次数用完、把消息送进死信
            Assert.Empty(producerFake.Topics);
            Assert.Empty(consumerFake.Read(static c => c.Committed.ToList()));
            Assert.Equal(1, Volatile.Read(ref dispatched));

            await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);
            Assert.Equal(1, dispatched);
            Assert.Empty(producerFake.Topics);
            Assert.Empty(consumerFake.Read(static c => c.Committed.ToList()));
        }
        finally
        {
            // 断言失败也要放开闸门，否则宿主停不下来
            stopGate.TrySetResult();
            if (draining is not null)
            {
                await draining.WaitAsync(_patience);
            }
        }
    }

    [Theory]
    [InlineData(SubscriptionMode.Competing, true, "Topic orders received an undecodable message; it was dead-lettered: ")]
    [InlineData(SubscriptionMode.Broadcast, false, "Topic orders delivered an undecodable broadcast message; it was dropped: ")]
    public async Task UndecodableMessage_IsSettledByMode_WithoutDispatching_AndLoggedUnderTheBrokersCategory(SubscriptionMode mode, bool deadLettered, string prefix)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logs = new LogCapture();
        var (producer, producerFake) = FakeKafkaProducer.Create();
        var body = "not json"u8.ToArray();
        var (consumer, consumerFake) = FakeKafkaConsumer.Create(body, Encode("ok"));
        var dispatched = new ConcurrentQueue<string>();
        await using var broker = Broker(host, producer, consumer, logs);

        await broker.StartAsync(
            [new MessageSubscription(Topic, mode)],
            (delivery, _) =>
            {
                dispatched.Enqueue(delivery.Envelope.Id);
                return ValueTask.FromResult(Result.Success());
            },
            CancellationToken.None);
        Assert.True(SpinWait.SpinUntil(() => dispatched.Contains("ok"), _patience));
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

        // 了结方式由共用的一处判定（ADR-0275）：竞争订阅发布到死信主题后提交位点，广播订阅丢弃、不提交；坏消息不派发，后面的消息照常投递
        string[] deadLetterTopics = deadLettered ? ["xihan.orders.dlq"] : [];
        long[] committed = deadLettered ? [0L, 1L] : [];
        Assert.Equal(["ok"], dispatched);
        Assert.Equal(deadLetterTopics, producerFake.Topics);
        Assert.Equal(committed, consumerFake.Read(static c => c.Committed.ToList()));

        // 日志措辞与别的代理相同，记在 Kafka 代理自己的类别下
        var entry = Assert.Single(logs.Entries, static e => e.Level >= LogLevel.Warning);
        Assert.Equal(typeof(KafkaMessageBroker).FullName, entry.Category);
        Assert.Equal(prefix + CloudEventsFormat.Decode(body).Error!.Message, entry.Message);
    }

    /// <summary>
    /// 用打桩的生产者与消费者创建代理：最多投递 2 次
    /// </summary>
    /// <remarks>
    /// <paramref name="logs"/> 为空时不收日志
    /// </remarks>
    private static KafkaMessageBroker Broker(TestHost host, IProducer<string?, byte[]> producer, IConsumer<string?, byte[]> consumer, ILoggerFactory? logs = null)
        => new(
            new KafkaMessagingOptions { BootstrapServers = "unused:9092" },
            new MessagingOptions { MaxAttempts = 2 },
            new MessagingInstance(new MessagingOptions()),
            host.App,
            new FakeTimeProvider(),
            logs ?? NullLoggerFactory.Instance,
            () => producer,
            _ => consumer);

    /// <summary>
    /// 编码一个测试信封
    /// </summary>
    private static byte[] Encode(string id) => CloudEventsFormat.Encode(new MessageEnvelope { Id = id, Source = "/tests", Type = "orders.placed" });
}
