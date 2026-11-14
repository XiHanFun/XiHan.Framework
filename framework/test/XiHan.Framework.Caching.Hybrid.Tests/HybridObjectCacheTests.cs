// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Caching.Abstractions.Stores;

namespace XiHan.Framework.Caching.Hybrid.Tests;

/// <summary>
/// 两级缓存
/// </summary>
/// <remarks>
/// L1 命中不触 L2、未命中回填、批量只把未命中的键交给 L2、写删先远后近、L1 存活时间上限与容量淘汰、取或建、存储变更失效 L1、选项解析
/// </remarks>
public sealed class HybridObjectCacheTests : IDisposable
{
    /// <summary>
    /// 固定起点的可控时钟
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// L2 替身
    /// </summary>
    private readonly RecordingSharedCache _shared;

    /// <summary>
    /// 被测缓存：L1 容量 3、存活上限 30 秒
    /// </summary>
    private readonly HybridObjectCache _cache;

    /// <summary>
    /// 创建夹具
    /// </summary>
    public HybridObjectCacheTests()
    {
        _shared = new RecordingSharedCache(_clock);
        _cache = new HybridObjectCache(_shared, new HybridCachingOptions { LocalCapacity = 3, LocalTimeToLive = TimeSpan.FromSeconds(30) }, _clock);
    }

    /// <inheritdoc />
    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task Get_FillsLocal_ThenServesFromLocal()
    {
        await _shared.SetAsync<string>(new CacheKey("k"), "v", CacheEntryOptions.Default, CancellationToken.None);
        _shared.Operations.Clear();

        Assert.Equal("v", (await _cache.GetAsync<string>(new CacheKey("k"), CancellationToken.None)).ValueOrThrow());
        Assert.Equal("v", (await _cache.GetAsync<string>(new CacheKey("k"), CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["get:k"], _shared.Operations);
        Assert.Equal(1, _cache.LocalCount);

        // 未命中也不缓存：每次都问 L2
        Assert.False((await _cache.GetAsync<string>(new CacheKey("missing"), CancellationToken.None)).HasValue);
        Assert.False((await _cache.GetAsync<string>(new CacheKey("missing"), CancellationToken.None)).HasValue);
        Assert.Equal(["get:k", "get:missing", "get:missing"], _shared.Operations);
    }

    [Fact]
    public async Task GetMany_OnlyAsksRemoteForLocalMisses()
    {
        await _cache.SetAsync<string>(new CacheKey("a"), "1", CacheEntryOptions.Default, CancellationToken.None);
        await _shared.SetAsync<string>(new CacheKey("b"), "2", CacheEntryOptions.Default, CancellationToken.None);
        _shared.Operations.Clear();

        var results = await _cache.GetManyAsync<string>([new CacheKey("a"), new CacheKey("b"), new CacheKey("c")], CancellationToken.None);

        Assert.Equal("1", results[0].ValueOrThrow());
        Assert.Equal("2", results[1].ValueOrThrow());
        Assert.False(results[2].HasValue);
        Assert.Equal(["getmany:b,c"], _shared.Operations);
        Assert.Equal(2, _cache.LocalCount);

        _shared.Operations.Clear();
        Assert.All(await _cache.GetManyAsync<string>([new CacheKey("a"), new CacheKey("b")], CancellationToken.None), static r => Assert.True(r.HasValue));
        Assert.Empty(_shared.Operations);
        Assert.Empty(await _cache.GetManyAsync<string>([], CancellationToken.None));
    }

    [Fact]
    public async Task SetAndRemove_GoRemoteFirst_ThenLocal()
    {
        await _cache.SetAsync<string>(new CacheKey("k"), "v", CacheEntryOptions.Expires(TimeSpan.FromMinutes(5)), CancellationToken.None);
        Assert.Equal("v", (await _shared.GetAsync<string>(new CacheKey("k"), CancellationToken.None)).ValueOrThrow());
        Assert.Equal(1, _cache.LocalCount);

        await _cache.SetManyAsync<string>([new(new CacheKey("x"), "1"), new(new CacheKey("y"), "2")], CacheEntryOptions.Default, CancellationToken.None);
        Assert.Equal("setmany:x,y", _shared.Operations[^1]);

        Assert.True(await _cache.RemoveAsync(new CacheKey("k"), CancellationToken.None));
        Assert.False((await _shared.GetAsync<string>(new CacheKey("k"), CancellationToken.None)).HasValue);
        Assert.False(await _cache.RemoveAsync(new CacheKey("k"), CancellationToken.None));
        Assert.Equal([true, true, false], await _cache.RemoveManyAsync([new CacheKey("x"), new CacheKey("y"), new CacheKey("nope")], CancellationToken.None));
        Assert.Equal(0, _cache.LocalCount);
    }

    [Fact]
    public async Task Local_ExpiresByOwnTimeToLive_CappedByLocalLimit()
    {
        // 条目自己 5 秒：5 秒后 L1 过期，重新问 L2（L2 也过期了，所以无值）
        await _cache.SetAsync<string>(new CacheKey("short"), "v", CacheEntryOptions.Expires(TimeSpan.FromSeconds(5)), CancellationToken.None);
        // 条目 10 分钟：L1 只留 30 秒上限，之后回到 L2 取
        await _cache.SetAsync<string>(new CacheKey("long"), "v", CacheEntryOptions.Expires(TimeSpan.FromMinutes(10)), CancellationToken.None);
        _shared.Operations.Clear();

        _clock.Advance(TimeSpan.FromSeconds(6));
        Assert.False((await _cache.GetAsync<string>(new CacheKey("short"), CancellationToken.None)).HasValue);
        Assert.Equal("v", (await _cache.GetAsync<string>(new CacheKey("long"), CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["get:short"], _shared.Operations);

        _clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal("v", (await _cache.GetAsync<string>(new CacheKey("long"), CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["get:short", "get:long"], _shared.Operations);
    }

    [Fact]
    public async Task Local_EvictsLeastRecentlyUsed_AtCapacity()
    {
        foreach (var key in new[] { "a", "b", "c" })
        {
            await _cache.SetAsync<string>(new CacheKey(key), key, CacheEntryOptions.Default, CancellationToken.None);
        }

        // 访问 a 让它变成最近使用；再写 d 淘汰的是 b
        _ = await _cache.GetAsync<string>(new CacheKey("a"), CancellationToken.None);
        await _cache.SetAsync<string>(new CacheKey("d"), "d", CacheEntryOptions.Default, CancellationToken.None);
        _shared.Operations.Clear();

        Assert.Equal(3, _cache.LocalCount);
        _ = await _cache.GetAsync<string>(new CacheKey("a"), CancellationToken.None);
        _ = await _cache.GetAsync<string>(new CacheKey("d"), CancellationToken.None);
        _ = await _cache.GetAsync<string>(new CacheKey("b"), CancellationToken.None);
        Assert.Equal(["get:b"], _shared.Operations);
    }

    [Fact]
    public async Task GetOrCreate_RunsFactoryOnce_ThenServesLocally()
    {
        var calls = 0;
        ValueTask<string> Factory(CancellationToken _)
        {
            calls++;
            return ValueTask.FromResult("built");
        }

        Assert.Equal("built", await _cache.GetOrCreateAsync<string>(new CacheKey("k"), Factory, CacheEntryOptions.Default, CancellationToken.None));
        Assert.Equal("built", await _cache.GetOrCreateAsync<string>(new CacheKey("k"), Factory, CacheEntryOptions.Default, CancellationToken.None));
        Assert.Equal(1, calls);
        Assert.Equal(["getorcreate:k"], _shared.Operations);

        // 类型不匹配按未命中处理，交给 L2 判定
        Assert.False((await _cache.GetAsync<int>(new CacheKey("k"), CancellationToken.None)).HasValue);
    }

    [Fact]
    public async Task StoreChanges_InvalidateLocal_ByKeyAndPrefix()
    {
        var feed = new FeedingStore(_clock);
        var shared = new RecordingSharedCache(_clock, feed);
        var cache = new HybridObjectCache(shared, new HybridCachingOptions { LocalTimeToLive = TimeSpan.FromMinutes(5) }, _clock);
        Assert.True(cache.InvalidatesAcrossInstances);
        Assert.False(_cache.InvalidatesAcrossInstances);

        await cache.SetAsync<string>(new CacheKey("user:1"), "a", CacheEntryOptions.Default, CancellationToken.None);
        await cache.SetAsync<string>(new CacheKey("user:2"), "b", CacheEntryOptions.Default, CancellationToken.None);
        await cache.SetAsync<string>(new CacheKey("role:1"), "c", CacheEntryOptions.Default, CancellationToken.None);

        // 别的实例改了 L2：通知到达前 L1 仍是旧值，到达后回 L2 取新值
        await shared.SetAsync<string>(new CacheKey("user:1"), "a2", CacheEntryOptions.Default, CancellationToken.None);
        shared.Operations.Clear();
        Assert.Equal("a", (await cache.GetAsync<string>(new CacheKey("user:1"), CancellationToken.None)).ValueOrThrow());
        feed.Raise(CacheChange.ForKeys([new CacheKey("user:1")]));
        Assert.Equal("a2", (await cache.GetAsync<string>(new CacheKey("user:1"), CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["get:user:1"], shared.Operations);
        Assert.Equal(3, cache.LocalCount);

        // 按前缀删除失效前缀下的全部 L1 条目
        feed.Raise(CacheChange.ForPrefix("user:"));
        Assert.Equal(1, cache.LocalCount);

        // 释放即退订
        cache.Dispose();
        Assert.Equal(0, feed.SubscriberCount);
    }

    [Fact]
    public async Task InvalidationDuringTheRemoteRead_DoesNotBackfillTheStaleValue()
    {
        var feed = new FeedingStore(_clock);
        var shared = new RecordingSharedCache(_clock, feed);
        using var cache = new HybridObjectCache(shared, new HybridCachingOptions { LocalTimeToLive = TimeSpan.FromMinutes(5) }, _clock);
        var user = new CacheKey("user:1");
        await shared.SetAsync<string>(user, "old", CacheEntryOptions.Default, CancellationToken.None);

        // 读 L2 的途中别的实例写了这个键：变更通知先到（L1 里本来没有），读回的旧值后到
        shared.AfterRead = keys =>
        {
            shared.AfterRead = null;
            shared.SetAsync<string>(keys[0], "new", CacheEntryOptions.Default, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            feed.Raise(CacheChange.ForKeys([keys[0]]));
        };
        Assert.Equal("old", (await cache.GetAsync<string>(user, CancellationToken.None)).ValueOrThrow());

        // 修之前旧值回填进 L1，留到 LocalTimeToLive；现在不回填，下一次读取回 L2 取到新值
        Assert.Equal(0, cache.LocalCount);
        Assert.Equal("new", (await cache.GetAsync<string>(user, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(1, cache.LocalCount);

        // 批量读同样：途中失效的键不回填
        var role = new CacheKey("role:1");
        await shared.SetAsync<string>(role, "old", CacheEntryOptions.Default, CancellationToken.None);
        shared.AfterRead = keys =>
        {
            shared.AfterRead = null;
            feed.Raise(CacheChange.ForKeys([role]));
        };
        Assert.Equal(["old"], (await cache.GetManyAsync<string>([role], CancellationToken.None)).Select(static v => v.ValueOrThrow()));
        shared.Operations.Clear();
        await cache.GetAsync<string>(role, CancellationToken.None);
        Assert.Equal(["get:role:1"], shared.Operations);

        // 按前缀失效挡住所有键的回填；途中没有失效时照常回填
        var other = new CacheKey("other:1");
        await shared.SetAsync<string>(other, "x", CacheEntryOptions.Default, CancellationToken.None);
        shared.AfterRead = _ =>
        {
            shared.AfterRead = null;
            feed.Raise(CacheChange.ForPrefix("nothing-matches:"));
        };
        await cache.GetAsync<string>(other, CancellationToken.None);
        shared.Operations.Clear();
        await cache.GetAsync<string>(other, CancellationToken.None);
        Assert.Equal(["get:other:1"], shared.Operations);
        shared.Operations.Clear();
        await cache.GetAsync<string>(other, CancellationToken.None);
        Assert.Empty(shared.Operations);
    }

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        var defaults = HybridCachingOptions.FromConfiguration(Build());
        Assert.Equal(10_000, defaults.LocalCapacity);
        Assert.Equal(TimeSpan.FromSeconds(30), defaults.LocalTimeToLive);

        var configured = HybridCachingOptions.FromConfiguration(Build(("XiHan:Caching:Hybrid:LocalCapacity", "50"), ("XiHan:Caching:Hybrid:LocalTimeToLiveSeconds", "2.5")));
        Assert.Equal(50, configured.LocalCapacity);
        Assert.Equal(TimeSpan.FromSeconds(2.5), configured.LocalTimeToLive);

        Assert.Throws<ArgumentException>(static () => HybridCachingOptions.FromConfiguration(Build(("XiHan:Caching:Hybrid:LocalCapacity", "0"))));
        Assert.Throws<ArgumentException>(static () => HybridCachingOptions.FromConfiguration(Build(("XiHan:Caching:Hybrid:LocalTimeToLiveSeconds", "-1"))));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new HybridCachingOptions { LocalCapacity = 0 });
    }
}
