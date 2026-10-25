// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Caching.Abstractions.Stores;

namespace XiHan.Framework.Caching.Hybrid.Tests;

/// <summary>
/// 两级缓存：L1 命中不触 L2、未命中回填、批量只把未命中的键交给 L2、写删先远后近、L1 存活时间上限与容量淘汰、取或建、选项解析。
/// </summary>
public sealed class HybridCacheTests
{
    /// <summary>
    /// 固定起点的可控时钟。
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// L2 替身。
    /// </summary>
    private readonly RecordingSharedCache _shared;

    /// <summary>
    /// 被测缓存：L1 容量 3、存活上限 30 秒。
    /// </summary>
    private readonly HybridCache _cache;

    /// <summary>
    /// 创建夹具。
    /// </summary>
    public HybridCacheTests()
    {
        _shared = new RecordingSharedCache(_clock);
        _cache = new HybridCache(_shared, new HybridCachingOptions { LocalCapacity = 3, LocalTimeToLive = TimeSpan.FromSeconds(30) }, _clock);
    }

    [Fact]
    public async Task Get_FillsLocal_ThenServesFromLocal()
    {
        await _shared.SetAsync<string>("k", "v", CacheEntryOptions.Default, CancellationToken.None);
        _shared.Operations.Clear();

        Assert.Equal("v", (await _cache.GetAsync<string>("k", CancellationToken.None)).ValueOrThrow());
        Assert.Equal("v", (await _cache.GetAsync<string>("k", CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["get:k"], _shared.Operations);
        Assert.Equal(1, _cache.LocalCount);

        // 未命中也不缓存：每次都问 L2
        Assert.False((await _cache.GetAsync<string>("missing", CancellationToken.None)).HasValue);
        Assert.False((await _cache.GetAsync<string>("missing", CancellationToken.None)).HasValue);
        Assert.Equal(["get:k", "get:missing", "get:missing"], _shared.Operations);
    }

    [Fact]
    public async Task GetMany_OnlyAsksRemoteForLocalMisses()
    {
        await _cache.SetAsync<string>("a", "1", CacheEntryOptions.Default, CancellationToken.None);
        await _shared.SetAsync<string>("b", "2", CacheEntryOptions.Default, CancellationToken.None);
        _shared.Operations.Clear();

        var results = await _cache.GetManyAsync<string>(["a", "b", "c"], CancellationToken.None);

        Assert.Equal("1", results[0].ValueOrThrow());
        Assert.Equal("2", results[1].ValueOrThrow());
        Assert.False(results[2].HasValue);
        Assert.Equal(["getmany:b,c"], _shared.Operations);
        Assert.Equal(2, _cache.LocalCount);

        _shared.Operations.Clear();
        Assert.All(await _cache.GetManyAsync<string>(["a", "b"], CancellationToken.None), static r => Assert.True(r.HasValue));
        Assert.Empty(_shared.Operations);
        Assert.Empty(await _cache.GetManyAsync<string>([], CancellationToken.None));
    }

    [Fact]
    public async Task SetAndRemove_GoRemoteFirst_ThenLocal()
    {
        await _cache.SetAsync<string>("k", "v", CacheEntryOptions.Expires(TimeSpan.FromMinutes(5)), CancellationToken.None);
        Assert.Equal("v", (await _shared.GetAsync<string>("k", CancellationToken.None)).ValueOrThrow());
        Assert.Equal(1, _cache.LocalCount);

        await _cache.SetManyAsync<string>([new("x", "1"), new("y", "2")], CacheEntryOptions.Default, CancellationToken.None);
        Assert.Equal("setmany:x,y", _shared.Operations[^1]);

        Assert.True(await _cache.RemoveAsync("k", CancellationToken.None));
        Assert.False((await _shared.GetAsync<string>("k", CancellationToken.None)).HasValue);
        Assert.False(await _cache.RemoveAsync("k", CancellationToken.None));
        Assert.Equal([true, true, false], await _cache.RemoveManyAsync(["x", "y", "nope"], CancellationToken.None));
        Assert.Equal(0, _cache.LocalCount);
    }

    [Fact]
    public async Task Local_ExpiresByOwnTimeToLive_CappedByLocalLimit()
    {
        // 条目自己 5 秒：5 秒后 L1 过期，重新问 L2（L2 也过期了，所以无值）
        await _cache.SetAsync<string>("short", "v", CacheEntryOptions.Expires(TimeSpan.FromSeconds(5)), CancellationToken.None);
        // 条目 10 分钟：L1 只留 30 秒上限，之后回到 L2 取
        await _cache.SetAsync<string>("long", "v", CacheEntryOptions.Expires(TimeSpan.FromMinutes(10)), CancellationToken.None);
        _shared.Operations.Clear();

        _clock.Advance(TimeSpan.FromSeconds(6));
        Assert.False((await _cache.GetAsync<string>("short", CancellationToken.None)).HasValue);
        Assert.Equal("v", (await _cache.GetAsync<string>("long", CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["get:short"], _shared.Operations);

        _clock.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal("v", (await _cache.GetAsync<string>("long", CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["get:short", "get:long"], _shared.Operations);
    }

    [Fact]
    public async Task Local_EvictsLeastRecentlyUsed_AtCapacity()
    {
        foreach (var key in new[] { "a", "b", "c" })
        {
            await _cache.SetAsync<string>(key, key, CacheEntryOptions.Default, CancellationToken.None);
        }

        // 访问 a 让它变成最近使用；再写 d 淘汰的是 b
        _ = await _cache.GetAsync<string>("a", CancellationToken.None);
        await _cache.SetAsync<string>("d", "d", CacheEntryOptions.Default, CancellationToken.None);
        _shared.Operations.Clear();

        Assert.Equal(3, _cache.LocalCount);
        _ = await _cache.GetAsync<string>("a", CancellationToken.None);
        _ = await _cache.GetAsync<string>("d", CancellationToken.None);
        _ = await _cache.GetAsync<string>("b", CancellationToken.None);
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

        Assert.Equal("built", await _cache.GetOrCreateAsync<string>("k", Factory, CacheEntryOptions.Default, CancellationToken.None));
        Assert.Equal("built", await _cache.GetOrCreateAsync<string>("k", Factory, CacheEntryOptions.Default, CancellationToken.None));
        Assert.Equal(1, calls);
        Assert.Equal(["getorcreate:k"], _shared.Operations);

        // 类型不匹配按未命中处理，交给 L2 判定
        Assert.False((await _cache.GetAsync<int>("k", CancellationToken.None)).HasValue);
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
