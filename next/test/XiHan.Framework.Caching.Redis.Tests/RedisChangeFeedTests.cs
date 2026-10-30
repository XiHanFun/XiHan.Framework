// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using StackExchange.Redis;
using XiHan.Framework.Caching.Abstractions.Stores;

namespace XiHan.Framework.Caching.Redis.Tests;

/// <summary>
/// Redis 存储的变更通知。
/// </summary>
/// <remarks>
/// 两个连接模拟两个实例：本实例同步收到、别的实例经频道收到、不回送给发出者、按前缀删除带前缀；没有真实 Redis 时跳过。
/// </remarks>
public sealed class RedisChangeFeedTests
{
    [Fact]
    public async Task Changes_ReachLocalAndOtherInstances_WithoutEcho()
    {
        var configuration = RedisTestServer.RequireConfiguration();
        var options = RedisTestServer.Options(configuration);
        await using var otherConnection = await ConnectionMultiplexer.ConnectAsync(configuration);
        using var local = new RedisCacheStore(RedisTestServer.Connect(), options);
        using var remote = new RedisCacheStore(otherConnection, options);
        var seenLocally = new ConcurrentQueue<CacheChange>();
        var seenRemotely = new ConcurrentQueue<CacheChange>();
        using var localSubscription = local.Subscribe(seenLocally.Enqueue);
        using var remoteSubscription = remote.Subscribe(seenRemotely.Enqueue);

        // 本实例写入：写入返回时已同步通知，别的实例经频道收到
        await local.SetAsync("feed:1", "v"u8.ToArray(), CacheEntryOptions.Default, CancellationToken.None);
        Assert.Equal(["feed:1"], Assert.Single(seenLocally).Keys.Select(static key => key.Value));
        await WaitForCountAsync(seenRemotely, 1);
        Assert.Equal(["feed:1"], seenRemotely.Last().Keys.Select(static key => key.Value));

        // 没删到的不通知；按前缀删除带前缀、不带键
        Assert.False(await local.RemoveAsync("feed:missing", CancellationToken.None));
        Assert.Equal(1, await local.RemoveByPrefixAsync("feed:", CancellationToken.None));
        await WaitForCountAsync(seenRemotely, 2);
        Assert.Equal(2, seenLocally.Count);
        Assert.Equal("feed:", seenRemotely.Last().Prefix);
        Assert.Empty(seenRemotely.Last().Keys);

        // 别的实例写入：本实例经频道收到；发出者只有本地同步的那一次，频道上的消息不回送给它
        await remote.SetManyAsync([new CacheEntry("feed:2", "a"u8.ToArray(), CacheEntryOptions.Default), new CacheEntry("feed:3", "b"u8.ToArray(), CacheEntryOptions.Default)], CancellationToken.None);
        await WaitForCountAsync(seenLocally, 3);
        Assert.Equal(["feed:2", "feed:3"], seenLocally.Last().Keys.Select(static key => key.Value));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(3, seenRemotely.Count);
        Assert.Equal(3, seenLocally.Count);

        // 退订后不再收到
        remoteSubscription.Dispose();
        await local.SetAsync("feed:4", "c"u8.ToArray(), CacheEntryOptions.Default, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(3, seenRemotely.Count);
    }

    [Fact]
    public async Task PublishChangesOff_KeepsLocalNotifications_Only()
    {
        var configuration = RedisTestServer.RequireConfiguration();
        var options = RedisTestServer.Options(configuration);
        await using var otherConnection = await ConnectionMultiplexer.ConnectAsync(configuration);
        using var silent = new RedisCacheStore(RedisTestServer.Connect(), options with { PublishChanges = false });
        using var listener = new RedisCacheStore(otherConnection, options);
        var seenLocally = new ConcurrentQueue<CacheChange>();
        var seenRemotely = new ConcurrentQueue<CacheChange>();
        using var localSubscription = silent.Subscribe(seenLocally.Enqueue);
        using var remoteSubscription = listener.Subscribe(seenRemotely.Enqueue);

        await silent.SetAsync("feed:1", "v"u8.ToArray(), CacheEntryOptions.Default, CancellationToken.None);
        Assert.Single(seenLocally);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Empty(seenRemotely);
    }

    /// <summary>
    /// 等到队列里至少有指定条数。
    /// </summary>
    /// <remarks>
    /// 五秒内没等到即失败。
    /// </remarks>
    private static async Task WaitForCountAsync(ConcurrentQueue<CacheChange> changes, int count)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (changes.Count < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }
}
