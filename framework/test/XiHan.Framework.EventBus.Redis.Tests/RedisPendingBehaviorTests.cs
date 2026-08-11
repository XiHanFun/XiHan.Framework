// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using StackExchange.Redis;

namespace XiHan.Framework.EventBus.Redis.Tests;

/// <summary>
/// Redis Stream 待处理消息行为的验证
/// </summary>
/// <remarks>
/// 修复方案建立在几条对 Redis 的假设上，这里对真实 Redis 逐条验证，避免方案基于臆测：
/// 读取但未确认的消息会留在待处理列表、可被另一消费者接管、接管后投递次数递增、确认后不再出现。
/// 地址取环境变量 <c>XIHAN_TEST_REDIS</c>，缺省 <c>localhost:6379,user=redis,password=redis</c>；
/// 不可达时整类跳过。
/// </remarks>
[Collection("Redis")]
public class RedisPendingBehaviorTests : IAsyncLifetime
{
    private static readonly string Configuration =
        Environment.GetEnvironmentVariable("XIHAN_TEST_REDIS") ?? "localhost:6379,user=redis,password=redis";

    private const string Group = "xihan-test-group";

    private readonly string _streamKey = $"xihan:test:stream:{Guid.NewGuid():N}";

    private IConnectionMultiplexer? _connection;

    /// <summary>
    /// 初始化
    /// </summary>
    /// <returns></returns>
    public async ValueTask InitializeAsync()
    {
        try
        {
            _connection = await ConnectionMultiplexer.ConnectAsync(Configuration);
        }
        catch (Exception)
        {
            _connection = null;
        }
    }

    /// <summary>
    /// 释放
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (_connection is null)
        {
            return;
        }

        await _connection.GetDatabase().KeyDeleteAsync(_streamKey);
        await _connection.CloseAsync();
        _connection.Dispose();
    }

    /// <summary>
    /// 读取但未确认的消息留在待处理列表，且可被另一消费者接管
    /// </summary>
    /// <remarks>
    /// 这正是崩溃场景：消费者读到消息后在确认前退出。原实现从不查看待处理列表，
    /// 这类消息就永远不会被再次投递。
    /// </remarks>
    [Fact]
    public async Task ReadWithoutAcknowledge_StaysPending_AndCanBeClaimedByAnotherConsumer()
    {
        var db = RequireDatabase();
        await db.StreamCreateConsumerGroupAsync(_streamKey, Group, StreamPosition.Beginning, createStream: true);
        await db.StreamAddAsync(_streamKey, "payload", "value-1");

        // 消费者 A 读取后不确认，模拟崩溃
        var read = await db.StreamReadGroupAsync(_streamKey, Group, "consumer-a", StreamPosition.NewMessages, 10);
        Assert.Single(read);

        var pending = await db.StreamPendingMessagesAsync(_streamKey, Group, 10, RedisValue.Null);
        Assert.Single(pending);
        Assert.Equal("consumer-a", pending[0].ConsumerName.ToString());

        // 消费者 B 以最小空闲 0 接管
        var claimed = await db.StreamClaimAsync(_streamKey, Group, "consumer-b", 0, [pending[0].MessageId]);

        Assert.Single(claimed);
        Assert.Equal("value-1", GetField(claimed[0], "payload"));
    }

    /// <summary>
    /// 接管会让投递次数递增
    /// </summary>
    /// <remarks>
    /// 死信判定依赖这个计数，若它不递增，毒消息将被无限接管。
    /// </remarks>
    [Fact]
    public async Task Claim_IncrementsDeliveryCount()
    {
        var db = RequireDatabase();
        await db.StreamCreateConsumerGroupAsync(_streamKey, Group, StreamPosition.Beginning, createStream: true);
        await db.StreamAddAsync(_streamKey, "payload", "value-1");
        await db.StreamReadGroupAsync(_streamKey, Group, "consumer-a", StreamPosition.NewMessages, 10);

        var before = (await db.StreamPendingMessagesAsync(_streamKey, Group, 10, RedisValue.Null))[0];
        await db.StreamClaimAsync(_streamKey, Group, "consumer-b", 0, [before.MessageId]);
        var after = (await db.StreamPendingMessagesAsync(_streamKey, Group, 10, RedisValue.Null))[0];

        Assert.Equal(1, before.DeliveryCount);
        Assert.True(after.DeliveryCount > before.DeliveryCount, $"投递次数未递增：{before.DeliveryCount} → {after.DeliveryCount}");
    }

    /// <summary>
    /// 空闲时长随时间增长，可用作滞留判定
    /// </summary>
    [Fact]
    public async Task IdleTime_GrowsOverTime()
    {
        var db = RequireDatabase();
        await db.StreamCreateConsumerGroupAsync(_streamKey, Group, StreamPosition.Beginning, createStream: true);
        await db.StreamAddAsync(_streamKey, "payload", "value-1");
        await db.StreamReadGroupAsync(_streamKey, Group, "consumer-a", StreamPosition.NewMessages, 10);

        await Task.Delay(120, TestContext.Current.CancellationToken);
        var pending = await db.StreamPendingMessagesAsync(_streamKey, Group, 10, RedisValue.Null);

        Assert.True(pending[0].IdleTimeInMilliseconds >= 100, $"空闲时长未增长：{pending[0].IdleTimeInMilliseconds}ms");
    }

    /// <summary>
    /// 确认后消息不再出现在待处理列表
    /// </summary>
    [Fact]
    public async Task Acknowledge_RemovesFromPending()
    {
        var db = RequireDatabase();
        await db.StreamCreateConsumerGroupAsync(_streamKey, Group, StreamPosition.Beginning, createStream: true);
        await db.StreamAddAsync(_streamKey, "payload", "value-1");
        var read = await db.StreamReadGroupAsync(_streamKey, Group, "consumer-a", StreamPosition.NewMessages, 10);

        await db.StreamAcknowledgeAsync(_streamKey, Group, read[0].Id);

        Assert.Empty(await db.StreamPendingMessagesAsync(_streamKey, Group, 10, RedisValue.Null));
    }

    /// <summary>
    /// 待处理消息的原始内容仍可读回，死信转移据此保留载荷
    /// </summary>
    [Fact]
    public async Task PendingMessage_ContentIsStillReadable()
    {
        var db = RequireDatabase();
        await db.StreamCreateConsumerGroupAsync(_streamKey, Group, StreamPosition.Beginning, createStream: true);
        await db.StreamAddAsync(_streamKey, "payload", "value-1");
        var read = await db.StreamReadGroupAsync(_streamKey, Group, "consumer-a", StreamPosition.NewMessages, 10);

        var range = await db.StreamRangeAsync(_streamKey, read[0].Id, read[0].Id, 1);

        Assert.Equal("value-1", GetField(range[0], "payload"));
    }

    /// <summary>
    /// 取得数据库，Redis 不可达时跳过
    /// </summary>
    /// <returns>数据库</returns>
    private IDatabase RequireDatabase()
    {
        Assert.SkipWhen(_connection is null, $"Redis 不可达（{Configuration}），跳过该组验证。");

        return _connection!.GetDatabase();
    }

    /// <summary>
    /// 读取条目字段
    /// </summary>
    /// <param name="entry">条目</param>
    /// <param name="name">字段名</param>
    /// <returns>字段值</returns>
    private static string GetField(StreamEntry entry, string name)
    {
        return entry.Values.FirstOrDefault(pair => pair.Name == name).Value.ToString();
    }
}

/// <summary>
/// Redis 测试集合，共享同一实例的用例串行执行
/// </summary>
[CollectionDefinition("Redis", DisableParallelization = true)]
public class RedisTestCollection;
