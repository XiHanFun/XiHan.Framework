// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using XiHan.Framework.Kernel;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Redis.Tests;

/// <summary>
/// 消费循环怎么了结一次投递（RFC-0009「投递」第 3、5 条，ADR-0261）
/// </summary>
/// <remarks>
/// 对打桩的数据库验证新条目的投递：成功确认，失败认领后原位重试、到上限竞争订阅写死信流再确认、广播订阅确认掉，停止打断的不确认，应用排空期间不接着原位重试。不需要 Redis；回收与投递次数由 <see cref="RedisStreamBehaviorTests"/> 在真实 Redis 上验证
/// </remarks>
public sealed class RedisDeliveryTests
{
    /// <summary>
    /// 测试主题
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 主题的死信流键（缺省前缀）
    /// </summary>
    private const string DeadLetterKey = "xihan:messaging:orders.dlq";

    /// <summary>
    /// 等待条件成立的上限
    /// </summary>
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Success_AcknowledgesTheEntry()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (database, fake) = FakeRedisDatabase.Create(Entry("1-1", "ok"));
        var consumer = Consumer(host, database, SubscriptionMode.Competing);
        using var stopping = new CancellationTokenSource();

        var loop = consumer.RunAsync(static (_, _) => ValueTask.FromResult(Result.Success()), stopping.Token);
        Assert.True(SpinWait.SpinUntil(() => fake.Read(static f => f.Acknowledged.Count) == 1, _patience));
        await StopAsync(stopping, loop);

        Assert.Equal(["1-1"], fake.Acknowledged);
        Assert.Empty(fake.Claimed);
        Assert.Empty(fake.Added);
    }

    [Theory]
    [InlineData(false, "ORDERS_VALIDATION_001")]
    [InlineData(true, MessagingErrors.HandlerFailed)]
    public async Task CompetingFailure_IsClaimedAndRetriedInPlace_ThenDeadLetteredWithAttemptAndError(bool throws, string errorCode)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (database, fake) = FakeRedisDatabase.Create(Entry("1-1", "poison"));
        var consumer = Consumer(host, database, SubscriptionMode.Competing);
        var attempts = new ConcurrentQueue<(int Attempt, SubscriptionMode Mode)>();
        using var stopping = new CancellationTokenSource();

        var loop = consumer.RunAsync(
            (delivery, _) =>
            {
                attempts.Enqueue((delivery.Attempt, delivery.Mode));
                return throws
                    ? throw new InvalidOperationException("Boom.")
                    : ValueTask.FromResult(Result.Failure(Error.Literal("ORDERS_VALIDATION_001", "Rejected.")));
            },
            stopping.Token);
        Assert.True(SpinWait.SpinUntil(() => fake.Read(static f => f.Acknowledged.Count) == 1, _patience));
        await StopAsync(stopping, loop);

        // 重试前把条目认领给自己（投递次数加一）；到上限先写死信流（带最后一次的尝试号与错误码），写完才确认原条目
        Assert.Equal([(1, SubscriptionMode.Competing), (2, SubscriptionMode.Competing)], attempts);
        Assert.Equal(["1-1"], fake.Claimed);
        var (key, fields) = Assert.Single(fake.Added);
        Assert.Equal(DeadLetterKey, key);
        Assert.Equal("poison", CloudEventsFormat.Decode(System.Text.Encoding.UTF8.GetBytes(fields["envelope"])).ValueOrThrow().Id);
        Assert.Equal(("2", errorCode), (fields["attempt"], fields["error"]));
        Assert.Equal(["1-1"], fake.Acknowledged);
    }

    [Fact]
    public async Task BroadcastFailure_IsRetriedInPlace_ThenAcknowledgedWithoutADeadLetter()
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (database, fake) = FakeRedisDatabase.Create(Entry("1-1", "poison"));
        var consumer = Consumer(host, database, SubscriptionMode.Broadcast);
        var attempts = new ConcurrentQueue<int>();
        using var stopping = new CancellationTokenSource();

        var loop = consumer.RunAsync(
            (delivery, _) =>
            {
                attempts.Enqueue(delivery.Attempt);
                return ValueTask.FromResult(Result.Failure(Error.Literal("REALTIME_UNAVAILABLE_001", "Not yet.")));
            },
            stopping.Token);
        Assert.True(SpinWait.SpinUntil(() => fake.Read(static f => f.Acknowledged.Count) == 1, _patience));
        await StopAsync(stopping, loop);

        // 广播没有死信：到上限确认掉，不写死信流
        Assert.Equal([1, 2], attempts);
        Assert.Equal(["1-1"], fake.Claimed);
        Assert.Empty(fake.Added);
        Assert.Equal(["1-1"], fake.Acknowledged);
    }

    [Theory]
    [InlineData(SubscriptionMode.Competing)]
    [InlineData(SubscriptionMode.Broadcast)]
    public async Task HandlerCancelledByStop_IsNotAcknowledged_Retried_OrDeadLettered(SubscriptionMode mode)
    {
        await using var host = await TestHost.StartAsync(static _ => { });
        var (database, fake) = FakeRedisDatabase.Create(Entry("1-1", "slow"));
        var consumer = Consumer(host, database, mode);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatched = 0;
        using var stopping = new CancellationTokenSource();

        var loop = consumer.RunAsync(
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
            stopping.Token);
        await entered.Task.WaitAsync(_patience);
        await StopAsync(stopping, loop);

        // 停止期间的失败不重试、不进死信、不确认：条目留在待确认列表里，由别的实例回收
        Assert.Equal(1, dispatched);
        Assert.Empty(fake.Acknowledged);
        Assert.Empty(fake.Claimed);
        Assert.Empty(fake.Added);
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
        var (database, fake) = FakeRedisDatabase.Create(Entry("1-1", "poison"));
        var consumer = Consumer(host, database, SubscriptionMode.Competing);
        var dispatched = 0;
        Task? draining = null;
        using var stopping = new CancellationTokenSource();
        try
        {
            var loop = consumer.RunAsync(
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
                stopping.Token);
            await stopEntered.Task.WaitAsync(_patience);
            await Task.Delay(300);

            // 排空中不接着原位重试：管道此时只会拒绝调用，空转会把尝试次数用完、把消息送进死信流
            Assert.Empty(fake.Read(static f => f.Added.ToList()));
            Assert.Empty(fake.Read(static f => f.Acknowledged.ToList()));
            Assert.Equal(1, Volatile.Read(ref dispatched));

            await StopAsync(stopping, loop);
            Assert.Equal(1, dispatched);
            Assert.Empty(fake.Added);
            Assert.Empty(fake.Acknowledged);
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

    /// <summary>
    /// 发停止信号并等消费循环结束
    /// </summary>
    private static async Task StopAsync(CancellationTokenSource stopping, Task loop)
    {
        await stopping.CancelAsync();
        await loop.WaitAsync(_patience);
    }

    /// <summary>
    /// 在打桩数据库上建一个订阅的消费：最多投递 2 次
    /// </summary>
    private static RedisStreamConsumer Consumer(TestHost host, IDatabase database, SubscriptionMode mode)
        => new(
            database,
            new RedisMessagingOptions { Configuration = "unused:6379", PollInterval = TimeSpan.FromMilliseconds(20) },
            new MessagingOptions { MaxAttempts = 2 },
            new MessagingInstance(new MessagingOptions()),
            host.App,
            TimeProvider.System,
            NullLogger.Instance,
            new MessageSubscription(Topic, mode));

    /// <summary>
    /// 带一个测试信封的流条目
    /// </summary>
    private static StreamEntry Entry(string entryId, string messageId)
        => new(entryId, [new NameValueEntry("envelope", CloudEventsFormat.Encode(new MessageEnvelope { Id = messageId, Source = "/tests", Type = "orders.placed" }))]);
}
