// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.MultiTenancy.Abstractions;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 分布式缓存默认实现测试
/// </summary>
/// <remarks>
/// 用内存替身充当缓存后端，覆盖读写、批量、能力探测回退与 GetOrAdd 的并发去重；
/// 断言里带上规范化键，保证「泛型缓存项 → 缓存名 → 后端键」这条链路不会悄悄改形状。
/// </remarks>
public class DistributedCacheTests
{
    private const string SamplePrefix = "0:sample:";

    /// <summary>
    /// 写入后可按同一业务键读回，且后端落到规范化键上
    /// </summary>
    [Fact]
    public void Set_ThenGet_ReturnsStoredValueUnderNormalizedKey()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Set("k1", new SampleCacheItem { Value = "v1" });

        Assert.Equal("v1", cache.Get("k1")?.Value);
        Assert.Contains(SamplePrefix + "k1", store.StoredKeys);
    }

    /// <summary>
    /// 未写入的键读取返回空
    /// </summary>
    [Fact]
    public void Get_WhenKeyMissing_ReturnsNull()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.Null(cache.Get("missing"));
    }

    /// <summary>
    /// 异步写入后可异步读回
    /// </summary>
    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsStoredValue()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        await cache.SetAsync("k1", new SampleCacheItem { Value = "v1" }, token: token);
        var value = await cache.GetAsync("k1", token: token);

        Assert.Equal("v1", value?.Value);
    }

    /// <summary>
    /// 存在当前租户时，后端键带上租户段
    /// </summary>
    [Fact]
    public void Set_WithCurrentTenant_WritesTenantScopedKey()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store, tenant: new BasicTenantInfo(88));
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Set("k1", new SampleCacheItem { Value = "v1" });

        Assert.Contains("88:sample:k1", store.StoredKeys);
    }

    /// <summary>
    /// 缓存项声明忽略多租户时，后端键不带租户段
    /// </summary>
    /// <remarks>
    /// 这类缓存项通常是全局字典或平台级配置，按租户分段会造成同一份数据被复制 N 份。
    /// </remarks>
    [Fact]
    public void Set_ForIgnoreMultiTenancyItem_WritesTenantNeutralKey()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store, tenant: new BasicTenantInfo(88));
        var cache = context.CreateStringKeyed<TenantNeutralCacheItem>();

        cache.Set("k1", new TenantNeutralCacheItem { Value = "v1" });

        Assert.Contains("0:neutral:k1", store.StoredKeys);
    }

    /// <summary>
    /// 未显式给条目选项时落到全局条目选项
    /// </summary>
    [Fact]
    public void Set_WithoutEntryOptions_UsesGlobalEntryOptions()
    {
        var options = new XiHanDistributedCacheOptions();
        options.GlobalCacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(5);
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store, options);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Set("k1", new SampleCacheItem { Value = "v1" });

        Assert.Same(options.GlobalCacheEntryOptions, store.LastSetOptions);
        Assert.Equal(TimeSpan.FromMinutes(5), store.LastSetOptions?.SlidingExpiration);
    }

    /// <summary>
    /// 显式给条目选项时原样透传给后端
    /// </summary>
    [Fact]
    public void Set_WithEntryOptions_PassesThemToBackend()
    {
        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
        };
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Set("k1", new SampleCacheItem { Value = "v1" }, entryOptions);

        Assert.Same(entryOptions, store.LastSetOptions);
    }

    /// <summary>
    /// 配置器命中当前缓存名时，其条目选项成为默认值
    /// </summary>
    [Fact]
    public void Set_WhenConfiguratorMatchesCacheName_UsesConfiguredEntryOptions()
    {
        var configured = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(3) };
        var options = new XiHanDistributedCacheOptions();
        options.ConfigureCache("other", new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(1) });
        options.ConfigureCache("sample", configured);
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store, options);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Set("k1", new SampleCacheItem { Value = "v1" });

        Assert.Same(configured, store.LastSetOptions);
    }

    /// <summary>
    /// 未命中时执行工厂取值并写入缓存
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenMissing_InvokesFactoryAndStoresValue()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        var calls = 0;

        var value = cache.GetOrAdd("k1", () =>
        {
            calls++;
            return new SampleCacheItem { Value = "built" };
        });

        Assert.Equal("built", value?.Value);
        Assert.Equal(1, calls);
        Assert.Equal("built", cache.Get("k1")?.Value);
    }

    /// <summary>
    /// 已命中时不再执行工厂
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenHit_DoesNotInvokeFactory()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        cache.Set("k1", new SampleCacheItem { Value = "cached" });
        var calls = 0;

        var value = cache.GetOrAdd("k1", () =>
        {
            calls++;
            return new SampleCacheItem { Value = "built" };
        });

        Assert.Equal("cached", value?.Value);
        Assert.Equal(0, calls);
    }

    /// <summary>
    /// 未命中时使用条目选项工厂给出的选项
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenMissing_UsesOptionsFactory()
    {
        var entryOptions = new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(15) };
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.GetOrAdd("k1", () => new SampleCacheItem { Value = "built" }, () => entryOptions);

        Assert.Same(entryOptions, store.LastSetOptions);
    }

    /// <summary>
    /// 同一键的并发获取只会执行一次工厂
    /// </summary>
    /// <remarks>
    /// 缓存击穿防护的核心契约：并发回源必须收敛到一次，否则热点键会把工厂背后的数据源打穿。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public async Task GetOrAddAsync_WithConcurrentCallers_InvokesFactoryOnlyOnce()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        var calls = 0;

        var tasks = Enumerable.Range(0, 32).Select(_ => cache.GetOrAddAsync("hot", async () =>
        {
            Interlocked.Increment(ref calls);
            await Task.Yield();
            return new SampleCacheItem { Value = "built" };
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.All(results, item => Assert.Equal("built", item?.Value));
    }

    /// <summary>
    /// 不同键的并发获取各自执行一次工厂
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GetOrAddAsync_WithDistinctKeys_BuildsEachKeyOnce()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        var calls = 0;

        var tasks = Enumerable.Range(0, 8).Select(index => cache.GetOrAddAsync($"k{index}", async () =>
        {
            Interlocked.Increment(ref calls);
            await Task.Yield();
            return new SampleCacheItem { Value = $"v{index}" };
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(8, Volatile.Read(ref calls));
        Assert.Equal(8, results.Length);
    }

    /// <summary>
    /// 存在性判断跟随实际写入与移除
    /// </summary>
    [Fact]
    public void Exists_FollowsSetAndRemove()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.False(cache.Exists("k1"));

        cache.Set("k1", new SampleCacheItem { Value = "v1" });
        Assert.True(cache.Exists("k1"));

        cache.Remove("k1");
        Assert.False(cache.Exists("k1"));
        Assert.Contains(SamplePrefix + "k1", store.RemovedKeys);
    }

    /// <summary>
    /// 异步存在性判断跟随实际写入
    /// </summary>
    [Fact]
    public async Task ExistsAsync_FollowsSet()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        Assert.False(await cache.ExistsAsync("k1", token: token));

        await cache.SetAsync("k1", new SampleCacheItem { Value = "v1" }, token: token);

        Assert.True(await cache.ExistsAsync("k1", token: token));
    }

    /// <summary>
    /// 后端支持批量时，多键读取走批量接口且结果与键顺序对齐
    /// </summary>
    [Fact]
    public void GetMany_WithBatchCapableBackend_UsesBatchApiAndKeepsOrder()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        cache.Set("b", new SampleCacheItem { Value = "B" });

        var result = cache.GetMany(["a", "b", "c"]);

        Assert.Equal(["a", "b", "c"], result.Select(pair => pair.Key));
        Assert.Null(result[0].Value);
        Assert.Equal("B", result[1].Value?.Value);
        Assert.Null(result[2].Value);
        Assert.Equal(1, store.GetManyCount);
        Assert.Equal(
            new[] { SamplePrefix + "a", SamplePrefix + "b", SamplePrefix + "c" },
            store.LastGetManyKeys);
    }

    /// <summary>
    /// 后端不支持批量时，多键读取逐条回退且结果仍与键顺序对齐
    /// </summary>
    [Fact]
    public void GetMany_WithBasicBackend_FallsBackToSingleReads()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        cache.Set("b", new SampleCacheItem { Value = "B" });

        var result = cache.GetMany(["a", "b"]);

        Assert.Equal(["a", "b"], result.Select(pair => pair.Key));
        Assert.Null(result[0].Value);
        Assert.Equal("B", result[1].Value?.Value);
    }

    /// <summary>
    /// 异步多键读取与同步保持一致的对齐语义
    /// </summary>
    [Fact]
    public async Task GetManyAsync_WithBatchCapableBackend_KeepsOrder()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        await cache.SetAsync("a", new SampleCacheItem { Value = "A" }, token: token);

        var result = await cache.GetManyAsync(["a", "z"], token: token);

        Assert.Equal("A", result[0].Value?.Value);
        Assert.Null(result[1].Value);
    }

    /// <summary>
    /// 后端支持批量时，多键写入走批量接口
    /// </summary>
    [Fact]
    public void SetMany_WithBatchCapableBackend_UsesBatchApi()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.SetMany(
        [
            new KeyValuePair<string, SampleCacheItem>("a", new SampleCacheItem { Value = "A" }),
            new KeyValuePair<string, SampleCacheItem>("b", new SampleCacheItem { Value = "B" })
        ]);

        Assert.Equal(1, store.SetManyCount);
        Assert.Equal("A", cache.Get("a")?.Value);
        Assert.Equal("B", cache.Get("b")?.Value);
    }

    /// <summary>
    /// 批量获取或添加只把缺失的键交给工厂
    /// </summary>
    [Fact]
    public void GetOrAddMany_OnlyPassesMissingKeysToFactory()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        cache.Set("a", new SampleCacheItem { Value = "A" });
        var requested = new List<string>();

        var result = cache.GetOrAddMany(["a", "b"], keys =>
        {
            requested.AddRange(keys);
            return
            [
                new("b", new SampleCacheItem { Value = "B" })
            ];
        });

        Assert.Equal(new[] { "b" }, requested);
        Assert.Equal("A", result.Single(pair => pair.Key == "a").Value?.Value);
        Assert.Equal("B", result.Single(pair => pair.Key == "b").Value?.Value);
        Assert.Equal("B", cache.Get("b")?.Value);
    }

    /// <summary>
    /// 批量获取或添加全部命中时不调用工厂
    /// </summary>
    [Fact]
    public async Task GetOrAddManyAsync_WhenAllHit_DoesNotInvokeFactory()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        await cache.SetAsync("a", new SampleCacheItem { Value = "A" }, token: token);
        var invoked = false;

        var result = await cache.GetOrAddManyAsync(["a"], keys =>
        {
            invoked = true;
            return Task.FromResult(new List<KeyValuePair<string, SampleCacheItem>>());
        }, token: token);

        Assert.False(invoked);
        Assert.Equal("A", result.Single().Value?.Value);
    }

    /// <summary>
    /// 刷新把规范化键透传给后端
    /// </summary>
    [Fact]
    public void Refresh_PassesNormalizedKeyToBackend()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Refresh("k1");

        Assert.Equal([SamplePrefix + "k1"], store.RefreshedKeys);
    }

    /// <summary>
    /// 后端支持批量时，多键刷新走批量接口
    /// </summary>
    [Fact]
    public void RefreshMany_WithBatchCapableBackend_UsesBatchApi()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.RefreshMany(["a", "b"]);

        Assert.Equal(1, store.RefreshManyCount);
        Assert.Equal([SamplePrefix + "a", SamplePrefix + "b"], store.RefreshedKeys);
    }

    /// <summary>
    /// 后端不支持批量时，多键刷新逐条回退
    /// </summary>
    [Fact]
    public void RefreshMany_WithBasicBackend_RefreshesEachKey()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.RefreshMany(["a", "b"]);

        Assert.Equal([SamplePrefix + "a", SamplePrefix + "b"], store.RefreshedKeys);
    }

    /// <summary>
    /// 后端支持批量时，多键移除走批量接口
    /// </summary>
    [Fact]
    public void RemoveMany_WithBatchCapableBackend_UsesBatchApi()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        cache.Set("a", new SampleCacheItem { Value = "A" });
        cache.Set("b", new SampleCacheItem { Value = "B" });

        cache.RemoveMany(["a", "b"]);

        Assert.Equal(1, store.RemoveManyCount);
        Assert.Null(cache.Get("a"));
        Assert.Null(cache.Get("b"));
    }

    /// <summary>
    /// 后端不支持批量时，多键移除逐条回退
    /// </summary>
    [Fact]
    public async Task RemoveManyAsync_WithBasicBackend_RemovesEachKey()
    {
        var token = TestContext.Current.CancellationToken;
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();
        await cache.SetAsync("a", new SampleCacheItem { Value = "A" }, token: token);
        await cache.SetAsync("b", new SampleCacheItem { Value = "B" }, token: token);

        await cache.RemoveManyAsync(["a", "b"], token: token);

        Assert.Equal([SamplePrefix + "a", SamplePrefix + "b"], store.RemovedKeys);
    }

    /// <summary>
    /// 单泛型分布式缓存把全部操作转发给内部缓存
    /// </summary>
    [Fact]
    public void SingleGenericCache_DelegatesToInternalCache()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var inner = context.CreateStringKeyed<SampleCacheItem>();
        var facade = new DistributedCache<SampleCacheItem>(inner);

        facade.Set("k1", new SampleCacheItem { Value = "v1" });

        Assert.Same(inner, facade.InternalCache);
        Assert.Equal("v1", facade.Get("k1")?.Value);
        Assert.True(facade.Exists("k1"));

        facade.Remove("k1");

        Assert.False(facade.Exists("k1"));
    }

    /// <summary>
    /// 不同缓存项类型即便业务键相同也互不覆盖
    /// </summary>
    [Fact]
    public void Caches_ForDifferentItemTypes_DoNotCollideOnSameBusinessKey()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var sampleCache = context.CreateStringKeyed<SampleCacheItem>();
        var neutralCache = context.CreateStringKeyed<TenantNeutralCacheItem>();

        sampleCache.Set("shared", new SampleCacheItem { Value = "sample" });
        neutralCache.Set("shared", new TenantNeutralCacheItem { Value = "neutral" });

        Assert.Equal("sample", sampleCache.Get("shared")?.Value);
        Assert.Equal("neutral", neutralCache.Get("shared")?.Value);
    }
}
