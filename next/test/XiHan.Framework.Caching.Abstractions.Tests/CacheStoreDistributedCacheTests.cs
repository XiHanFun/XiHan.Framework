// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions.Distributed;
using XiHan.Framework.Caching.Abstractions.Stores;

namespace XiHan.Framework.Caching.Abstractions.Tests;

/// <summary>
/// <see cref="CacheStoreDistributedCache"/>：读写删与续期落到存储、键文法、过期选项的换算与登记扩展的幂等。
/// </summary>
public sealed class CacheStoreDistributedCacheTests
{
    /// <summary>
    /// 固定的"现在"。
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Operations_MapToStore_ForAsyncAndSyncMembers()
    {
        var store = new FakeCacheStore();
        var cache = new CacheStoreDistributedCache(store, TimeProvider.System);

        Assert.Null(await cache.GetAsync("user:1"));
        await cache.SetAsync("user:1", [1, 2, 3], new DistributedCacheEntryOptions());
        Assert.Equal([1, 2, 3], await cache.GetAsync("user:1"));
        await cache.RefreshAsync("user:1");
        await cache.RemoveAsync("user:1");
        Assert.Null(await cache.GetAsync("user:1"));

        cache.Set("user:2", [9], new DistributedCacheEntryOptions());
        Assert.Equal([9], cache.Get("user:2"));
        cache.Refresh("user:2");
        cache.Remove("user:2");
        Assert.Null(cache.Get("user:2"));

        // 续期是一次读取，删除不存在的键不报错
        Assert.Equal(["get:user:1", "set:user:1", "get:user:1", "get:user:1", "remove:user:1", "get:user:1", "set:user:2", "get:user:2", "get:user:2", "remove:user:2", "get:user:2"], store.Operations);
    }

    [Fact]
    public async Task InvalidKeys_AndMissingArguments_AreRejected()
    {
        var cache = new CacheStoreDistributedCache(new FakeCacheStore(), TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() => cache.GetAsync("has space"));
        Assert.Throws<ArgumentException>(() => cache.Get(string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.SetAsync("k", null!, new DistributedCacheEntryOptions()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.SetAsync("k", [1], null!));
        Assert.Throws<ArgumentNullException>(static () => new CacheStoreDistributedCache(null!, TimeProvider.System));
    }

    [Fact]
    public void Translate_MapsExpirations_AndPrefersAbsolute()
    {
        Assert.Equal(CacheEntryOptions.Default, CacheStoreDistributedCache.Translate(new DistributedCacheEntryOptions(), _now));
        Assert.Equal(TimeSpan.FromMinutes(5), CacheStoreDistributedCache.Translate(new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, _now).TimeToLive);
        Assert.Equal(TimeSpan.FromMinutes(2), CacheStoreDistributedCache.Translate(new DistributedCacheEntryOptions { AbsoluteExpiration = _now.AddMinutes(2) }, _now).TimeToLive);
        Assert.Equal(TimeSpan.FromSeconds(30), CacheStoreDistributedCache.Translate(new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(30) }, _now).SlidingExpiration);

        // 两个绝对过期取较早者；绝对与滑动同时给时绝对优先，滑动不进存储选项
        var both = CacheStoreDistributedCache.Translate(new DistributedCacheEntryOptions { AbsoluteExpiration = _now.AddMinutes(1), AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3) }, _now);
        Assert.Equal(TimeSpan.FromMinutes(1), both.TimeToLive);
        var mixed = CacheStoreDistributedCache.Translate(new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3), SlidingExpiration = TimeSpan.FromSeconds(10) }, _now);
        Assert.Equal(TimeSpan.FromMinutes(3), mixed.TimeToLive);
        Assert.Null(mixed.SlidingExpiration);

        Assert.Throws<ArgumentOutOfRangeException>(() => CacheStoreDistributedCache.Translate(new DistributedCacheEntryOptions { AbsoluteExpiration = _now.AddSeconds(-1) }, _now));
        Assert.Throws<ArgumentOutOfRangeException>(() => CacheStoreDistributedCache.Translate(new DistributedCacheEntryOptions { AbsoluteExpiration = _now }, _now));
    }

    [Fact]
    public void AddXiHanDistributedCache_RegistersOnce_AndKeepsExistingRegistration()
    {
        var store = new FakeCacheStore();
        var services = new ServiceCollection();
        services.AddXiHanDistributedCache(_ => store);
        services.AddXiHanDistributedCache(static _ => new FakeCacheStore());
        using var provider = services.BuildServiceProvider();

        var cache = Assert.IsType<CacheStoreDistributedCache>(provider.GetRequiredService<IDistributedCache>());
        cache.Set("k", [1], new DistributedCacheEntryOptions());
        Assert.Equal([1], store["k"]);
        Assert.Single(provider.GetServices<IDistributedCache>());

        // 应用自己登记的 IDistributedCache 优先
        var custom = new ServiceCollection();
        custom.AddSingleton<IDistributedCache>(new CacheStoreDistributedCache(store, TimeProvider.System));
        custom.AddXiHanDistributedCache(static _ => new FakeCacheStore());
        Assert.Single(custom, static d => d.ServiceType == typeof(IDistributedCache));
    }
}
