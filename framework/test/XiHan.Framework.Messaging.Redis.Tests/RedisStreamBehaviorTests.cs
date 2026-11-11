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
        var pending = await database.StreamPendingMessagesAsync(Key, Group, 10, RedisValue.Null);
        Assert.Equal(firstConsumer, (string?)Assert.Single(pending).ConsumerName);

        var second = new DeliveryLog();
        await using var next = await StartAsync(second, [new(Topic, SubscriptionMode.Competing)]);

        var received = Assert.Single(await second.WaitAsync(static r => r.Delivery.Envelope.Id == "slow-1", 1, _timeout));
        Assert.Equal(2, received.Delivery.Attempt);
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
    private Task<TestHost> StartAsync(DeliveryLog log, IReadOnlyList<MessageSubscription> subscriptions, Type handler, params (string Key, string Value)[] settings)
    {
        var configuration = new List<KeyValuePair<string, string?>>
        {
            new("XiHan:Messaging:Redis:Configuration", RedisTestServer.RequireConfiguration()),
            new("XiHan:Messaging:Redis:KeyPrefix", _prefix),
            new("XiHan:Messaging:Redis:ClaimIdleTimeSeconds", "1"),
            new("XiHan:Messaging:Redis:PollIntervalSeconds", "0.05"),
            new($"{MessagingOptions.ConfigurationSection}:MaxAttempts", "2"),
        };
        configuration.AddRange(settings.Select(static s => new KeyValuePair<string, string?>(s.Key, s.Value)));
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
