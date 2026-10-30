// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;

namespace XiHan.Framework.Caching.InMemory.Tests;

/// <summary>
/// 对象缓存的键管理。
/// </summary>
/// <remarks>
/// 对象条目与字节条目分在两层、各自列出；按前缀分页、类型与剩余存活、查看不延长滑动过期、按前缀删除（ADR-0058）。
/// </remarks>
public sealed class InMemoryObjectCacheInspectorTests
{
    /// <summary>
    /// 假时钟。
    /// </summary>
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 30, 8, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// 字节存储。
    /// </summary>
    private readonly InMemoryCacheStore _store;

    /// <summary>
    /// 被测的对象缓存，<see cref="ICache.Store"/> 指向 <see cref="_store"/>。
    /// </summary>
    private readonly InMemoryObjectCache _cache;

    /// <summary>
    /// 创建夹具：默认存活 5 分钟。
    /// </summary>
    public InMemoryObjectCacheInspectorTests()
    {
        var options = new InMemoryCachingOptions { DefaultTimeToLive = TimeSpan.FromMinutes(5) };
        _store = new InMemoryCacheStore(options, _clock);
        _cache = new InMemoryObjectCache(_store, options, _clock);
    }

    [Fact]
    public async Task ObjectAndByteEntries_AreListedByTheirOwnLayer()
    {
        for (var i = 0; i < 5; i++)
        {
            await _cache.SetAsync<object>($"access:user:{i}", new Snapshot(i), CacheEntryOptions.Default, CancellationToken.None);
        }

        await _cache.SetAsync<string>("identity:session", "s", CacheEntryOptions.Default, CancellationToken.None);
        await _store.SetAsync("access:lock", Encoding.UTF8.GetBytes("owner"), CacheEntryOptions.Default, CancellationToken.None);

        // 对象层：每页 2 个，走完续页，只有对象条目
        var inspector = Assert.IsAssignableFrom<ICacheInspector>(_cache);
        var listed = new List<string>();
        string? continuation = null;
        do
        {
            var page = await inspector.ListKeysAsync(new CacheKeyQuery { Prefix = "access:", Limit = 2, Continuation = continuation }, CancellationToken.None);
            Assert.InRange(page.Keys.Count, 1, 2);
            listed.AddRange(page.Keys.Select(static k => k.Value));
            continuation = page.Continuation;
        }
        while (continuation is not null);
        Assert.Equal([.. Enumerable.Range(0, 5).Select(static i => $"access:user:{i}")], listed);

        // 字节层：只有直接写进存储的条目
        var bytes = Assert.IsAssignableFrom<ICacheStoreInspector>(_cache.Store);
        Assert.Equal(["access:lock"], (await bytes.ListKeysAsync(new CacheKeyQuery { Prefix = "access:" }, CancellationToken.None)).Keys.Select(static k => k.Value));
        Assert.Equal(["identity:session"], (await inspector.ListKeysAsync(new CacheKeyQuery { Prefix = "identity:" }, CancellationToken.None)).Keys.Select(static k => k.Value));
    }

    [Fact]
    public async Task Inspect_GivesTypeAndRemainingTime_WithoutExtendingSliding()
    {
        await _cache.SetAsync<object>("access:user:1", new Snapshot(1), CacheEntryOptions.Expires(TimeSpan.FromMinutes(2)), CancellationToken.None);
        await _cache.SetAsync<object>("access:window", new Snapshot(2), CacheEntryOptions.Sliding(TimeSpan.FromSeconds(60)), CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(30));

        var info = (await _cache.InspectAsync("access:user:1", CancellationToken.None)).ValueOrThrow();
        Assert.Equal(new CachedObjectInfo("access:user:1", typeof(Snapshot).FullName!, TimeSpan.FromSeconds(90), null), info);
        Assert.Equal(new Snapshot(1), (await _cache.GetAsync<object>("access:user:1", CancellationToken.None)).ValueOrThrow());

        // 查看不算访问：滑动窗口不因查看而延长
        var sliding = (await _cache.InspectAsync("access:window", CancellationToken.None)).ValueOrThrow();
        Assert.Equal((TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)), (sliding.TimeToLive!.Value, sliding.SlidingExpiration!.Value));
        _clock.Advance(TimeSpan.FromSeconds(31));
        Assert.False((await _cache.InspectAsync("access:window", CancellationToken.None)).HasValue);
        Assert.False((await _cache.InspectAsync("missing", CancellationToken.None)).HasValue);
    }

    [Fact]
    public async Task RemoveByPrefix_RemovesOnlyObjectEntriesUnderThePrefix()
    {
        await _cache.SetAsync<object>("access:user:1", new Snapshot(1), CacheEntryOptions.Default, CancellationToken.None);
        await _cache.SetAsync<object>("access:user:2", new Snapshot(2), CacheEntryOptions.Default, CancellationToken.None);
        await _cache.SetAsync<object>("identity:user:1", new Snapshot(3), CacheEntryOptions.Default, CancellationToken.None);
        await _store.SetAsync("access:lock", Encoding.UTF8.GetBytes("owner"), CacheEntryOptions.Default, CancellationToken.None);

        Assert.Equal(2, await _cache.RemoveByPrefixAsync("access:", CancellationToken.None));
        Assert.Empty((await _cache.ListKeysAsync(new CacheKeyQuery { Prefix = "access:" }, CancellationToken.None)).Keys);
        Assert.True((await _cache.GetAsync<Snapshot>("identity:user:1", CancellationToken.None)).HasValue);
        // 字节存储里的同前缀条目不受影响，各层各删
        Assert.True((await _store.GetAsync("access:lock", CancellationToken.None)).HasValue);
        Assert.Equal(0, await _cache.RemoveByPrefixAsync("access:", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () => await _cache.RemoveByPrefixAsync(string.Empty, CancellationToken.None));
    }

    /// <summary>
    /// 套件用的缓存对象。
    /// </summary>
    /// <param name="Version">版本。</param>
    private sealed record Snapshot(int Version);
}
