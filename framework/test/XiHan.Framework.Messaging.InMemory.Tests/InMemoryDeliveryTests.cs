// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 内存代理怎么了结一次投递
/// </summary>
/// <remarks>
/// 停止打断的投递按原尝试号放回队列，应用排空中不派发，重投入不了队就到此为止，派发抛出的异常记日志（RFC-0009「投递」第 3、5 条，ADR-0261）；被管道拒绝的派发不计尝试（ADR-0275）
/// </remarks>
public sealed class InMemoryDeliveryTests
{
    /// <summary>
    /// 测试订阅的主题
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

    /// <summary>
    /// 以给定方式订阅了测试主题的订阅表
    /// </summary>
    private static MessageSubscriptions Subscriptions(SubscriptionMode mode) => new MessageSubscriptions().Add(Topic, typeof(RecordingHandler), mode);

    [Theory]
    [InlineData(SubscriptionMode.Competing, false)]
    [InlineData(SubscriptionMode.Competing, true)]
    [InlineData(SubscriptionMode.Broadcast, false)]
    [InlineData(SubscriptionMode.Broadcast, true)]
    public async Task DeliveryInterruptedByStop_IsPutBackWithTheSameAttempt_AndNotDeadLettered(SubscriptionMode mode, bool throws)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logs = new LogCapture();
        var subscriptions = Subscriptions(mode);
        // 最多投递 1 次：被停止打断的那一次要是算成失败，就直接进死信（广播则丢弃）
        using var broker = new InMemoryMessageBroker(new InMemoryMessagingOptions(), new MessagingOptions { MaxAttempts = 1 }, subscriptions, host.App, logs);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True((await broker.PublishAsync(Topic, Envelope("slow"), CancellationToken.None)).IsSuccess);

        await broker.StartAsync(
            [.. subscriptions.All],
            async (_, cancellationToken) =>
            {
                entered.TrySetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException) when (!throws)
                {
                    // 管道把处理器的取消写成失败结果返回，不抛异常
                    return Result.Failure(new Error(KernelErrors.Cancelled, KernelMessages.Cancelled));
                }

                return Result.Success();
            },
            CancellationToken.None);
        await entered.Task.WaitAsync(_patience);
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

        // 停止打断的投递不算一次失败：放回队列，不进死信、不丢弃，也不记进死信或丢弃的日志
        Assert.Empty(broker.DeadLetters(Topic));
        Assert.Equal(1, broker.QueuedCount(Topic));
        Assert.Equal(0, broker.DeliveredCount(Topic));
        Assert.DoesNotContain(logs.Entries, static e => e.Level >= LogLevel.Warning);

        // 再次开始消费：同一条投递、同一个尝试号
        var redelivered = new TaskCompletionSource<MessageDelivery>(TaskCreationOptions.RunContinuationsAsynchronously);
        await broker.StartAsync(
            [.. subscriptions.All],
            (delivery, _) =>
            {
                redelivered.TrySetResult(delivery);
                return ValueTask.FromResult(Result.Success());
            },
            CancellationToken.None);
        var again = await redelivered.Task.WaitAsync(_patience);
        Assert.Equal(("slow", 1, mode), (again.Envelope.Id, again.Attempt, again.Mode));
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);
    }

    [Fact]
    public async Task HandlerCancelledByStop_ThroughThePipeline_IsNotDeadLettered()
    {
        var log = new DeliveryLog();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            [new("XiHan:Messaging:MaxAttempts", "1")],
            services =>
            {
                services.AddSingleton(log);
                services.AddMessageHandler<BlockingHandler>(Topic);
            });
        var broker = host.Services.GetRequiredService<InMemoryMessageBroker>();

        Assert.True((await broker.PublishAsync(Topic, Envelope("slow"), CancellationToken.None)).IsSuccess);
        await log.WaitAsync(static r => r.Delivery.Envelope.Id == "slow", 1, _patience);
        await host.Services.GetRequiredService<MessagingRuntime>().StopAsync(CancellationToken.None).WaitAsync(_patience);

        // 真实的派发器与管道：处理器被停止取消，管道返回 KERNEL_CANCELLED_001 的失败结果。它不是一次失败的投递
        Assert.Empty(broker.DeadLetters(Topic));
        Assert.Equal(1, broker.QueuedCount(Topic));
    }

    [Fact]
    public async Task DeliveryTakenWhileTheAppDrains_IsNotDispatched_AndIsPutBackOnStop()
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
        var draining = host.App.StopAsync(CancellationToken.None);
        try
        {
            await stopEntered.Task.WaitAsync(_patience);
            var subscriptions = Subscriptions(SubscriptionMode.Competing);
            using var broker = new InMemoryMessageBroker(new InMemoryMessagingOptions(), new MessagingOptions(), subscriptions, host.App, NullLoggerFactory.Instance);
            var dispatched = 0;
            Assert.True((await broker.PublishAsync(Topic, Envelope("held"), CancellationToken.None)).IsSuccess);

            await broker.StartAsync(
                [.. subscriptions.All],
                (_, _) =>
                {
                    Interlocked.Increment(ref dispatched);
                    return ValueTask.FromResult(Result.Success());
                },
                CancellationToken.None);
            // 消费循环已经把投递取出队列，等在服务状态上
            Assert.True(SpinWait.SpinUntil(() => broker.QueuedCount(Topic) == 0, _patience));
            await Task.Delay(100);
            Assert.Equal(0, Volatile.Read(ref dispatched));

            await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

            // 取出来却没能派发的投递放回队列，不随停止丢掉
            Assert.Equal(0, dispatched);
            Assert.Equal(1, broker.QueuedCount(Topic));
            Assert.Empty(broker.DeadLetters(Topic));
        }
        finally
        {
            // 断言失败也要放开闸门，否则宿主停不下来
            stopGate.TrySetResult();
            await draining.WaitAsync(_patience);
        }
    }

    [Theory]
    [InlineData(SubscriptionMode.Competing)]
    [InlineData(SubscriptionMode.Broadcast)]
    public async Task DispatchRejectedBecauseTheAppLeftService_IsNotCounted_AndTheDeliveryIsPutBackOnStop(SubscriptionMode mode)
    {
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // 特性停在闸门前，应用就一直处在排空中：代理的停止信号还没到
        var probe = new StubFeature(TestManifests.Create("xihan.test.messaging-rejected"))
        {
            OnStop = async _ =>
            {
                stopEntered.SetResult();
                await stopGate.Task;
            },
        };
        await using var host = await TestHost.StartAsync(b => b.UseFeature(probe));
        Task? draining = null;
        try
        {
            var logs = new LogCapture();
            var subscriptions = Subscriptions(mode);
            // 最多投递 1 次：被拒绝的那一次要是算成失败，就直接进死信（广播则丢弃）
            using var broker = new InMemoryMessageBroker(new InMemoryMessagingOptions(), new MessagingOptions { MaxAttempts = 1 }, subscriptions, host.App, logs);
            var dispatched = 0;
            Assert.True((await broker.PublishAsync(Topic, Envelope("flipped"), CancellationToken.None)).IsSuccess);

            await broker.StartAsync(
                [.. subscriptions.All],
                async (_, cancellationToken) =>
                {
                    // 代理查看时应用在服务状态；进管道之前应用开始排空，真实的管道以 KERNEL_HOSTING_004 拒绝
                    Interlocked.Increment(ref dispatched);
                    draining ??= host.App.StopAsync(CancellationToken.None);
                    await stopEntered.Task.WaitAsync(_patience, cancellationToken);
                    await using var scope = host.Services.CreateAsyncScope();
                    return await host.App.InvokeAsync(App.CreateContext(scope.ServiceProvider, cancellationToken), static _ => ValueTask.CompletedTask);
                },
                CancellationToken.None);
            await stopEntered.Task.WaitAsync(_patience);
            await Task.Delay(200);

            // 被拒绝的派发不计尝试：投递还在代理手里等服务状态，没有进死信，也没有再派发
            Assert.Empty(broker.DeadLetters(Topic));
            Assert.Equal(1, Volatile.Read(ref dispatched));

            await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

            // 停止信号到来照旧放弃：按原尝试号放回队列
            Assert.Equal(1, dispatched);
            Assert.Equal(1, broker.QueuedCount(Topic));
            Assert.Empty(broker.DeadLetters(Topic));
            Assert.Equal(0, broker.DeliveredCount(Topic));
            Assert.DoesNotContain(logs.Entries, static e => e.Level >= LogLevel.Warning);
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
    [InlineData(SubscriptionMode.Competing, 1, "Message first on topic orders was dead-lettered after attempt 1 failed: ORDERS_VALIDATION_001")]
    [InlineData(SubscriptionMode.Broadcast, 0, "Broadcast message first on topic orders was dropped after attempt 1 failed: ORDERS_VALIDATION_001")]
    public async Task Retry_ThatCannotBeRequeuedBecauseTheQueueIsFull_EndsTheDelivery(SubscriptionMode mode, int deadLetters, string message)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logs = new LogCapture();
        var subscriptions = Subscriptions(mode);
        // 容量 1：处理第一条时再发布一条把队列占满，第一条的重投入不了队
        using var broker = new InMemoryMessageBroker(new InMemoryMessagingOptions { MaxQueuedMessages = 1 }, new MessagingOptions { MaxAttempts = 3 }, subscriptions, host.App, logs);
        var seen = new ConcurrentQueue<(string Id, int Attempt)>();
        Assert.True((await broker.PublishAsync(Topic, Envelope("first"), CancellationToken.None)).IsSuccess);

        await broker.StartAsync(
            [.. subscriptions.All],
            async (delivery, cancellationToken) =>
            {
                seen.Enqueue((delivery.Envelope.Id, delivery.Attempt));
                if (delivery.Envelope.Id != "first")
                {
                    return Result.Success();
                }

                (await broker.PublishAsync(Topic, Envelope("second"), cancellationToken)).ThrowIfFailure();
                return Result.Failure(Error.Literal("ORDERS_VALIDATION_001", "Rejected."));
            },
            CancellationToken.None);
        Assert.True(SpinWait.SpinUntil(() => broker.DeliveredCount(Topic) == 1, _patience));
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

        // 还没到上限也到此为止：竞争投递进死信、广播投递丢弃，日志记在内存代理自己的类别下
        Assert.Equal([("first", 1), ("second", 1)], seen);
        Assert.Equal(deadLetters, broker.DeadLetters(Topic).Count);
        Assert.Equal(
            [(LogLevel.Warning, typeof(InMemoryMessageBroker).FullName, message)],
            logs.Entries.Where(static e => e.Level >= LogLevel.Warning).Select(static e => (e.Level, (string?)e.Category, e.Message)));
    }

    [Fact]
    public async Task ThrowingDispatch_IsRetried_AndTheExceptionIsLoggedUnderTheBrokersCategory()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var logs = new LogCapture();
        var subscriptions = Subscriptions(SubscriptionMode.Competing);
        using var broker = new InMemoryMessageBroker(new InMemoryMessagingOptions(), new MessagingOptions { MaxAttempts = 2 }, subscriptions, host.App, logs);
        var attempts = new ConcurrentQueue<int>();
        Assert.True((await broker.PublishAsync(Topic, Envelope("boom"), CancellationToken.None)).IsSuccess);

        await broker.StartAsync(
            [.. subscriptions.All],
            (delivery, _) =>
            {
                attempts.Enqueue(delivery.Attempt);
                throw new InvalidOperationException("Server=db-01;Password=secret");
            },
            CancellationToken.None);
        Assert.True(SpinWait.SpinUntil(() => broker.DeadLetters(Topic).Count == 1, _patience));
        await broker.StopAsync(CancellationToken.None).WaitAsync(_patience);

        // 抛出与返回失败一样重试到上限后进死信；每次抛出记一条带异常的警告，死信的日志只带错误码
        Assert.Equal([1, 2], attempts);
        Assert.Equal(
            [
                "Dispatching message boom on topic orders threw on attempt 1.",
                "Dispatching message boom on topic orders threw on attempt 2.",
                "Message boom on topic orders was dead-lettered after attempt 2 failed: MESSAGING_UNEXPECTED_001",
            ],
            logs.Entries.Where(static e => e.Level >= LogLevel.Warning && e.Category == typeof(InMemoryMessageBroker).FullName).Select(static e => e.Message));
        Assert.DoesNotContain(logs.Entries, static e => e.Message.Contains("secret", StringComparison.Ordinal));
    }
}
