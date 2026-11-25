// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Redis.Tests;

/// <summary>
/// Redis Streams 上的行为：流的建立、死信、回收、清理、整理与裁剪
/// </summary>
/// <remarks>
/// 对真实 Redis 运行，未设置 <c>XIHAN_TEST_REDIS</c> 时跳过。宿主的最多投递次数是 2，回收阈值 1 秒（维护也是每秒一次）；每个用例一个键前缀，结束时删掉
/// </remarks>
public sealed class RedisStreamBehaviorTests : IAsyncDisposable
{
    /// <summary>
    /// 竞争订阅用例的主题
    /// </summary>
    private const string Topic = "redis.orders";

    /// <summary>
    /// 广播订阅用例的主题
    /// </summary>
    private const string BroadcastTopic = "redis.notices";

    /// <summary>
    /// 缺省的竞争组名
    /// </summary>
    private const string Group = "xihan";

    /// <summary>
    /// 等待异步结果的超时
    /// </summary>
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 本用例的键前缀
    /// </summary>
    private readonly string _prefix = RedisTestServer.NewPrefix();

    /// <summary>
    /// 主题的流键
    /// </summary>
    private RedisKey Key => _prefix + Topic;

    /// <summary>
    /// 主题的死信流键
    /// </summary>
    private RedisKey DeadLetterKey => _prefix + Topic + ".dlq";

    [Fact]
    public async Task Publish_ToATopicNobodySubscribes_DoesNotCreateItsStream()
    {
        var database = RedisTestServer.Database();
        await using var host = await StartAsync(new DeliveryLog(), [new(Topic, SubscriptionMode.Competing)]);

        (await PublishAsync(host, "redis.nobody", [Envelope("nobody-1")]))[0].ThrowIfFailure();
        Assert.All(await PublishAsync(host, "redis.nobody", [Envelope("nobody-2"), Envelope("nobody-3")]), static r => Assert.True(r.IsSuccess, r.ToString()));

        Assert.False(await database.KeyExistsAsync(_prefix + "redis.nobody"));
        // 订阅方启动时就建好了流，订阅的主题照常写入
        Assert.True(await database.KeyExistsAsync(Key));
    }

    [Fact]
    public async Task FailedMessage_IsDeadLetteredWithItsAttemptAndError_AndTheOriginalIsAcknowledged()
    {
        var database = RedisTestServer.Database();
        var log = new DeliveryLog();
        log.FailingIds["bad-1"] = 0;
        await using var host = await StartAsync(log, [new(Topic, SubscriptionMode.Competing)]);

        (await PublishAsync(host, Topic, [Envelope("bad-1")]))[0].ThrowIfFailure();

        var attempts = await log.WaitAsync(static r => r.Delivery.Envelope.Id == "bad-1", 2, _timeout);
        Assert.Equal([1, 2], attempts.Select(static r => r.Delivery.Attempt));
        var deadLetter = Assert.Single(await WaitForEntriesAsync(DeadLetterKey, 1));
        Assert.Equal("bad-1", Decode(deadLetter).Id);
        Assert.Equal("2", (string?)deadLetter["attempt"]);
        Assert.Equal("CONFORMANCE_VALIDATION_001", (string?)deadLetter["error"]);
        await WaitUntilAsync(async () => (await database.StreamPendingAsync(Key, Group)).PendingMessageCount == 0);
    }

    [Fact]
    public async Task UndecodableEntry_IsDeadLetteredWhenCompeting_AndDroppedWhenBroadcast_WithoutCallingTheHandler()
    {
        var database = RedisTestServer.Database();
        var log = new DeliveryLog();
        await using var host = await StartAsync(log, [new(Topic, SubscriptionMode.Competing), new(BroadcastTopic, SubscriptionMode.Broadcast)]);
        var broadcastGroup = BroadcastGroupOf(host);

        await database.StreamAddAsync(Key, "envelope", "not json");
        await database.StreamAddAsync(_prefix + BroadcastTopic, "envelope", "not json");

        var deadLetter = Assert.Single(await WaitForEntriesAsync(DeadLetterKey, 1));
        Assert.Equal("not json", (string?)deadLetter["envelope"]);
        Assert.Equal(MessagingErrors.InvalidEnvelope, (string?)deadLetter["error"]);
        // 广播组读过这条坏条目（最后投递的 ID 前进了）并确认掉了它
        await WaitUntilAsync(async () =>
        {
            var group = (await database.StreamGroupInfoAsync(_prefix + BroadcastTopic)).Single(g => g.Name == broadcastGroup);
            return group.PendingMessageCount == 0 && group.LastDeliveredId?.ToString() is { Length: > 0 } last && last != "0-0";
        });
        Assert.False(await database.KeyExistsAsync(_prefix + BroadcastTopic + ".dlq"));
        Assert.Empty(log.All);
    }

    [Fact]
    public async Task UnacknowledgedEntry_OfAVanishedConsumer_IsReclaimedWithTheNextAttempt_AndTheConsumerIsRemoved()
    {
        var database = RedisTestServer.Database();
        // 一个读走了条目就消失的消费者：条目留在它的待确认列表里，投递次数 1
        await database.StreamCreateConsumerGroupAsync(Key, Group, StreamPosition.NewMessages, createStream: true);
        await database.StreamAddAsync(Key, "envelope", CloudEventsFormat.Encode(Envelope("lost-1")));
        Assert.Single(await database.StreamReadGroupAsync(Key, Group, "ghost", StreamPosition.NewMessages, 10));
        var log = new DeliveryLog();

        await using var host = await StartAsync(log, [new(Topic, SubscriptionMode.Competing)]);

        var received = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "lost-1", 1, _timeout));
        Assert.Equal(2, received.Delivery.Attempt);
        await WaitUntilAsync(async () => (await database.StreamPendingAsync(Key, Group)).PendingMessageCount == 0);
        await WaitUntilAsync(async () => (await database.StreamConsumerInfoAsync(Key, Group)).All(static c => c.Name != "ghost"));
    }

    [Fact]
    public async Task ReclaimedEntry_PastMaxAttempts_IsDeadLetteredWithoutCallingTheHandler()
    {
        var database = RedisTestServer.Database();
        // 已投递过两次（上限）的条目：再被回收时投递次数是 3，直接进死信
        await database.StreamCreateConsumerGroupAsync(Key, Group, StreamPosition.NewMessages, createStream: true);
        await database.StreamAddAsync(Key, "envelope", CloudEventsFormat.Encode(Envelope("late-1")));
        var taken = Assert.Single(await database.StreamReadGroupAsync(Key, Group, "ghost", StreamPosition.NewMessages, 10));
        Assert.Single(await database.StreamClaimAsync(Key, Group, "ghost", 0, [taken.Id]));
        var log = new DeliveryLog();

        await using var host = await StartAsync(log, [new(Topic, SubscriptionMode.Competing)]);

        var deadLetter = Assert.Single(await WaitForEntriesAsync(DeadLetterKey, 1));
        Assert.Equal("late-1", Decode(deadLetter).Id);
        Assert.Equal("3", (string?)deadLetter["attempt"]);
        Assert.True(deadLetter["error"].IsNull);
        Assert.Empty(log.All);
    }

    [Fact]
    public async Task DeliveryInterruptedByStop_StaysPending_AndTheNextInstanceReclaimsIt()
    {
        var database = RedisTestServer.Database();
        var first = new DeliveryLog();
        var host = await StartAsync(first, [new(Topic, SubscriptionMode.Competing)], handler: typeof(BlockingHandler));
        string firstConsumer;
        try
        {
            firstConsumer = host.Services.GetRequiredService<MessagingInstance>().Id;
            (await PublishAsync(host, Topic, [Envelope("slow-1")]))[0].ThrowIfFailure();
            Assert.Single(await first.WaitAsync(static r => r.Delivery.Envelope.Id == "slow-1", 1, _timeout));
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 停止时处理器被取消：条目没有确认，停下的消费者因为还有待确认条目没被删除
        // 被打断的这一次不计尝试：读取算上的那一次投递已经退回（ADR-0284）
        var pending = Assert.Single(await database.StreamPendingMessagesAsync(Key, Group, 10, RedisValue.Null));
        Assert.Equal(firstConsumer, (string?)pending.ConsumerName);
        Assert.Equal(0, pending.DeliveryCount);

        var second = new DeliveryLog();
        await using var next = await StartAsync(second, [new(Topic, SubscriptionMode.Competing)]);

        // 回收让投递次数加一：仍是第 1 次尝试（以前是第 2 次，被打断的那次白白用掉一次）
        var received = Assert.Single(await second.WaitAsync(static r => r.Delivery.Envelope.Id == "slow-1", 1, _timeout));
        Assert.Equal(1, received.Delivery.Attempt);
        await WaitUntilAsync(async () => (await database.StreamPendingAsync(Key, Group)).PendingMessageCount == 0);
    }

    [Fact]
    public async Task LastAttemptInterruptedByStop_IsReclaimedWithTheSameAttempt_AndHandledInsteadOfDeadLettered()
    {
        var database = RedisTestServer.Database();
        var first = new DeliveryLog();
        first.FailingIds["last-1"] = 0;
        first.BlockingAttempts["last-1"] = 2;
        var host = await StartAsync(first, [new(Topic, SubscriptionMode.Competing)]);
        try
        {
            (await PublishAsync(host, Topic, [Envelope("last-1")]))[0].ThrowIfFailure();

            // 第 1 次失败后原位重试；第 2 次（上限）到达处理器后等在取消令牌上
            var attempts = await first.WaitAsync(static r => r.Delivery.Envelope.Id == "last-1", 2, _timeout);
            Assert.Equal([1, 2], attempts.Select(static r => r.Delivery.Attempt));
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 停止打断了最后一次尝试：它不计尝试，投递次数由 2 退回 1，没有进死信
        Assert.Equal(1, Assert.Single(await database.StreamPendingMessagesAsync(Key, Group, 10, RedisValue.Null)).DeliveryCount);
        Assert.False(await database.KeyExistsAsync(DeadLetterKey));

        var second = new DeliveryLog();
        await using var next = await StartAsync(second, [new(Topic, SubscriptionMode.Competing)]);

        // 接手的实例回收后仍是第 2 次尝试，处理器再执行一次并成功；以前回收到的投递次数是 3，超过上限，不派发就进死信
        var received = Assert.Single(await second.WaitAsync(static r => r.Delivery.Envelope.Id == "last-1", 1, _timeout));
        Assert.Equal(2, received.Delivery.Attempt);
        await WaitUntilAsync(async () => (await database.StreamPendingAsync(Key, Group)).PendingMessageCount == 0);
        Assert.False(await database.KeyExistsAsync(DeadLetterKey));
    }

    [Fact]
    public async Task LastAttemptOfAVanishedConsumer_StaysCounted_AndThePoisonMessageIsStillDeadLettered()
    {
        var database = RedisTestServer.Database();
        // 一个在最后一次尝试上消失的消费者（进程被强行终止，来不及退回投递次数）：投递次数 2 留在待确认列表里
        await database.StreamCreateConsumerGroupAsync(Key, Group, StreamPosition.NewMessages, createStream: true);
        await database.StreamAddAsync(Key, "envelope", CloudEventsFormat.Encode(Envelope("killed-1")));
        var taken = Assert.Single(await database.StreamReadGroupAsync(Key, Group, "ghost", StreamPosition.NewMessages, 10));
        Assert.Single(await database.StreamClaimAsync(Key, Group, "ghost", 0, [taken.Id]));
        var log = new DeliveryLog();

        await using var host = await StartAsync(log, [new(Topic, SubscriptionMode.Competing)]);

        // 没有退回就照旧计入：让进程崩溃的消息每崩一次用掉一次尝试，到上限进死信，不会无限地投
        var deadLetter = Assert.Single(await WaitForEntriesAsync(DeadLetterKey, 1));
        Assert.Equal("killed-1", Decode(deadLetter).Id);
        Assert.Empty(log.All);
    }

    [Fact]
    public async Task EntriesReadButNotDispatchedBeforeStop_AreReclaimedWithTheirFirstAttempt()
    {
        var database = RedisTestServer.Database();
        var first = new DeliveryLog();
        first.BlockingIds["batch-1"] = 0;
        var host = await StartAsync(first, [new(Topic, SubscriptionMode.Competing)]);
        try
        {
            // 一个事务里写入三条：消费循环一次读到，第一条处理时停止，后两条没有派发
            Assert.All(await PublishAsync(host, Topic, [Envelope("batch-1"), Envelope("batch-2"), Envelope("batch-3")]), static r => Assert.True(r.IsSuccess, r.ToString()));
            Assert.Single(await first.WaitAsync(static r => r.Delivery.Envelope.Id == "batch-1", 1, _timeout));
        }
        finally
        {
            await host.DisposeAsync();
        }

        Assert.Single(first.All);
        // 三条都还待确认，读取算上的那一次都已退回
        var pending = await database.StreamPendingMessagesAsync(Key, Group, 10, RedisValue.Null);
        Assert.Equal([0, 0, 0], pending.Select(static p => p.DeliveryCount));

        var second = new DeliveryLog();
        await using var next = await StartAsync(second, [new(Topic, SubscriptionMode.Competing)]);

        // 接手的实例从第 1 次尝试起投递每一条；以前没派发过的两条也从第 2 次起，上限是 1 时不经处理就进死信
        var received = await second.WaitAsync(static r => r.Delivery.Envelope.Id.StartsWith("batch-", StringComparison.Ordinal), 3, _timeout);
        Assert.Equal(["batch-1", "batch-2", "batch-3"], received.Select(static r => r.Delivery.Envelope.Id));
        Assert.All(received, static r => Assert.Equal(1, r.Delivery.Attempt));
        await WaitUntilAsync(async () => (await database.StreamPendingAsync(Key, Group)).PendingMessageCount == 0);
        Assert.False(await database.KeyExistsAsync(DeadLetterKey));
    }

    [Fact]
    public async Task StopDuringAReclaim_ReadsNoNewEntries_AndLeavesThemInTheStreamForTheNextInstance()
    {
        var database = RedisTestServer.Database();
        // 一个读走了条目就消失的消费者；它消失之后又写入两条新条目，还没有任何消费者读过
        await database.StreamCreateConsumerGroupAsync(Key, Group, StreamPosition.NewMessages, createStream: true);
        await database.StreamAddAsync(Key, "envelope", CloudEventsFormat.Encode(Envelope("reclaimed-1")));
        var lost = Assert.Single(await database.StreamReadGroupAsync(Key, Group, "ghost", StreamPosition.NewMessages, 10));
        await database.StreamAddAsync(Key, "envelope", CloudEventsFormat.Encode(Envelope("fresh-1")));
        await database.StreamAddAsync(Key, "envelope", CloudEventsFormat.Encode(Envelope("fresh-2")));
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        var first = new DeliveryLog();
        first.BlockingIds["reclaimed-1"] = 0;
        var host = await StartAsync(first, [new(Topic, SubscriptionMode.Competing)]);
        try
        {
            // 启动时的维护回收了消失的消费者留下的条目（空闲过了 1 秒的回收阈值），处理器等在取消令牌上
            Assert.Single(await first.WaitAsync(static r => r.Delivery.Envelope.Id == "reclaimed-1", 1, _timeout));
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 回收途中停止：不再读新条目，它们留在流里，没有投递给任何消费者，组的读取位置仍停在回收到的那一条上
        // 以前消费循环退出之前还会再读一批：两条新条目挂在停下的消费者名下，要等别的实例回收（ADR-0298）
        var pending = Assert.Single(await database.StreamPendingMessagesAsync(Key, Group, 10, RedisValue.Null));
        Assert.Equal(lost.Id, pending.MessageId);
        Assert.Equal(lost.Id, (await database.StreamGroupInfoAsync(Key)).Single(static g => g.Name == Group).LastDeliveredId);
        Assert.Single(first.All);

        // 接手的实例：回收到的条目仍是第 2 次尝试（回收加一，被打断的那一次已退回），两条新条目直接读到、从第 1 次尝试起
        var second = new DeliveryLog();
        await using var next = await StartAsync(second, [new(Topic, SubscriptionMode.Competing)]);
        var received = await second.WaitAsync(static r => r.Delivery.Envelope.Id is "reclaimed-1" or "fresh-1" or "fresh-2", 3, _timeout);
        Assert.Equal([("fresh-1", 1), ("fresh-2", 1), ("reclaimed-1", 2)], received.Select(static r => (r.Delivery.Envelope.Id, r.Delivery.Attempt)).Order());
        await WaitUntilAsync(async () => (await database.StreamPendingAsync(Key, Group)).PendingMessageCount == 0);
        Assert.False(await database.KeyExistsAsync(DeadLetterKey));
    }

    [Fact]
    public async Task DeliveryAbandonedByStop_IsTakenOverAtOnce_WithoutWaitingForTheClaimThreshold()
    {
        var database = RedisTestServer.Database();
        // 回收阈值 60 秒，比等待的上限长得多：接手的实例要是还得等满阈值，这个用例就等不到
        (string, string) claimIdle = ("XiHan:Messaging:Redis:ClaimIdleTimeSeconds", "60");
        var first = new DeliveryLog();
        var host = await StartAsync(first, [new(Topic, SubscriptionMode.Competing)], typeof(BlockingHandler), claimIdle);
        try
        {
            (await PublishAsync(host, Topic, [Envelope("slow-1")]))[0].ThrowIfFailure();
            Assert.Single(await first.WaitAsync(static r => r.Delivery.Envelope.Id == "slow-1", 1, _timeout));
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 退回投递次数时空闲时间设到了回收阈值（ADR-0298）：以前照原样写回，只有处理器等着的那一小会儿
        var pending = Assert.Single(await database.StreamPendingMessagesAsync(Key, Group, 10, RedisValue.Null));
        Assert.Equal(0, pending.DeliveryCount);
        Assert.True(pending.IdleTimeInMilliseconds >= 60_000, $"The idle time is {pending.IdleTimeInMilliseconds} ms.");

        // 接手的实例启动时的第一次维护就回收它，仍是第 1 次尝试
        var second = new DeliveryLog();
        await using var next = await StartAsync(second, [new(Topic, SubscriptionMode.Competing)], typeof(RecordingHandler), claimIdle);
        var received = Assert.Single(await second.WaitAsync(static r => r.Delivery.Envelope.Id == "slow-1", 1, _timeout));
        Assert.Equal(1, received.Delivery.Attempt);
        await WaitUntilAsync(async () => (await database.StreamPendingAsync(Key, Group)).PendingMessageCount == 0);
    }

    [Fact]
    public async Task Stop_RemovesTheBroadcastGroupAndItsStream_AndTheCompetingConsumer_ButKeepsTheCompetingGroup()
    {
        var database = RedisTestServer.Database();
        string instance;
        await using (var host = await StartAsync(new DeliveryLog(), [new(Topic, SubscriptionMode.Competing), new(BroadcastTopic, SubscriptionMode.Broadcast)]))
        {
            instance = host.Services.GetRequiredService<MessagingInstance>().Id;
            Assert.Contains(await database.StreamGroupInfoAsync(_prefix + BroadcastTopic), g => g.Name == $"{Group}@{instance}");
            Assert.Contains(await database.StreamConsumerInfoAsync(Key, Group), c => c.Name == instance);
        }

        // 广播组是这个实例独占的，删掉后流上一个组也不剩，流一起删除；竞争组留着，离线期间的消息还要进它
        Assert.False(await database.KeyExistsAsync(_prefix + BroadcastTopic));
        var group = Assert.Single(await database.StreamGroupInfoAsync(Key));
        Assert.Equal(Group, group.Name);
        Assert.Equal(0, group.ConsumerCount);
    }

    [Fact]
    public async Task Maintenance_RemovesAbandonedBroadcastGroupsAndIdleConsumers_AndLeavesOtherGroupsAlone()
    {
        var database = RedisTestServer.Database();
        await database.StreamCreateConsumerGroupAsync(Key, Group, StreamPosition.NewMessages, createStream: true);
        await database.StreamReadGroupAsync(Key, Group, "old", StreamPosition.NewMessages, 1);
        await database.StreamCreateConsumerGroupAsync(Key, $"{Group}@ghost", StreamPosition.NewMessages);
        await database.StreamReadGroupAsync(Key, $"{Group}@ghost", "ghost", StreamPosition.NewMessages, 1);
        await database.StreamCreateConsumerGroupAsync(Key, $"{Group}@empty", StreamPosition.NewMessages);
        await database.StreamCreateConsumerGroupAsync(Key, "other@x", StreamPosition.NewMessages);
        await database.StreamReadGroupAsync(Key, "other@x", "x", StreamPosition.NewMessages, 1);
        // 让这些消费者都空闲过回收阈值（1 秒）
        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        await using var host = await StartAsync(new DeliveryLog(), [new(Topic, SubscriptionMode.Competing)]);
        var instance = host.Services.GetRequiredService<MessagingInstance>().Id;

        // 别的应用的组（前缀不是 xihan@）不归本实例清理
        await WaitUntilAsync(async () => (await database.StreamGroupInfoAsync(Key)).Select(static g => (string)g.Name!).Order(StringComparer.Ordinal).SequenceEqual(["other@x", Group]));
        await WaitUntilAsync(async () => (await database.StreamConsumerInfoAsync(Key, Group)).Select(static c => (string)c.Name!).SequenceEqual([instance]));
    }

    [Fact]
    public async Task Maintenance_TrimsEntriesEveryGroupHasAcknowledged()
    {
        var database = RedisTestServer.Database();
        var log = new DeliveryLog();
        await using var host = await StartAsync(log, [new(Topic, SubscriptionMode.Competing)]);
        var ids = Enumerable.Range(0, 300).Select(static i => $"trim-{i:D3}").ToList();

        Assert.All(await PublishAsync(host, Topic, [.. ids.Select(Envelope)]), static r => Assert.True(r.IsSuccess, r.ToString()));
        await log.WaitAsync(static r => r.Delivery.Envelope.Id.StartsWith("trim-", StringComparison.Ordinal), ids.Count, _timeout);

        // XTRIM MINID ~ 只整块删除宏节点（每块最多 100 条）：留下的不超过最后一块
        await WaitUntilAsync(async () => await database.StreamLengthAsync(Key) <= 100);
    }

    [Fact]
    public async Task MaxLength_CapsTheStream_WhileNobodyConsumes()
    {
        var database = RedisTestServer.Database();
        // 订阅方建过组之后离线：流在，没有消费者读
        await database.StreamCreateConsumerGroupAsync(Key, Group, StreamPosition.NewMessages, createStream: true);
        await using var publisher = await StartAsync(new DeliveryLog(), [], ("XiHan:Messaging:Redis:MaxLength", "50"));

        Assert.All(await PublishAsync(publisher, Topic, [.. Enumerable.Range(0, 1000).Select(static i => Envelope($"cap-{i}"))]), static r => Assert.True(r.IsSuccess, r.ToString()));

        // MAXLEN ~ 按宏节点裁剪，可以超出上限不到一块（每块最多 100 条）
        var length = await database.StreamLengthAsync(Key);
        Assert.InRange(length, 50, 150);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await RedisTestServer.DeleteKeysAsync(_prefix);

    /// <summary>
    /// 用例信封
    /// </summary>
    private static MessageEnvelope Envelope(string id) => new() { Id = id, Source = "/redis-tests", Type = "tests.redis", Data = Encoding.UTF8.GetBytes("{}") };

    /// <summary>
    /// 解码条目里的信封
    /// </summary>
    private static MessageEnvelope Decode(StreamEntry entry) => CloudEventsFormat.Decode((byte[])entry["envelope"]!).ValueOrThrow();

    /// <summary>
    /// 本宿主的广播组名
    /// </summary>
    private static string BroadcastGroupOf(TestHost host) => $"{Group}@{host.Services.GetRequiredService<MessagingInstance>().Id}";

    /// <summary>
    /// 装配并启动宿主：Redis 消息特性、给定订阅的处理器、最多投递 2 次、回收阈值 1 秒
    /// </summary>
    private Task<TestHost> StartAsync(DeliveryLog log, IReadOnlyList<MessageSubscription> subscriptions, params (string Key, string Value)[] settings)
        => StartAsync(log, subscriptions, typeof(RecordingHandler), settings);

    /// <summary>
    /// 装配并启动宿主，处理器类型由调用方给出
    /// </summary>
    /// <remarks>
    /// <paramref name="settings"/> 覆盖同名的缺省配置
    /// </remarks>
    private Task<TestHost> StartAsync(DeliveryLog log, IReadOnlyList<MessageSubscription> subscriptions, Type handler, params (string Key, string Value)[] settings)
    {
        var configuration = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["XiHan:Messaging:Redis:Configuration"] = RedisTestServer.RequireConfiguration(),
            ["XiHan:Messaging:Redis:KeyPrefix"] = _prefix,
            ["XiHan:Messaging:Redis:ClaimIdleTimeSeconds"] = "1",
            ["XiHan:Messaging:Redis:PollIntervalSeconds"] = "0.05",
            [$"{MessagingOptions.ConfigurationSection}:MaxAttempts"] = "2",
        };
        foreach (var (key, value) in settings)
        {
            configuration[key] = value;
        }

        return TestHost.StartAsync(
            static b => b.UseFeature<RedisMessagingFeature>(),
            configuration,
            services =>
            {
                services.AddSingleton(log);
                foreach (var subscription in subscriptions)
                {
                    if (handler == typeof(BlockingHandler))
                    {
                        services.AddMessageHandler<BlockingHandler>(subscription.Topic, subscription.Mode);
                    }
                    else
                    {
                        services.AddMessageHandler<RecordingHandler>(subscription.Topic, subscription.Mode);
                    }
                }
            });
    }

    /// <summary>
    /// 在新作用域内批量发布
    /// </summary>
    private static async Task<IReadOnlyList<Result>> PublishAsync(TestHost host, string topic, IReadOnlyList<MessageEnvelope> envelopes)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishManyAsync(topic, envelopes, CancellationToken.None);
    }

    /// <summary>
    /// 等到流里至少有给定条数的条目
    /// </summary>
    private static async Task<StreamEntry[]> WaitForEntriesAsync(RedisKey key, int count)
    {
        var database = RedisTestServer.Database();
        StreamEntry[] entries = [];
        await WaitUntilAsync(async () => (entries = await database.StreamRangeAsync(key)).Length >= count);
        return entries;
    }

    /// <summary>
    /// 轮询到条件成立，超时失败
    /// </summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!await condition())
        {
            Assert.True(stopwatch.Elapsed < _timeout, "The condition was not met in time.");
            await Task.Delay(50);
        }
    }
}
