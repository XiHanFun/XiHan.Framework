// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Tests.Fakes;

namespace XiHan.Framework.Caching.Tests.Distributed;

/// <summary>
/// 分布式缓存应用级键前缀测试
/// </summary>
/// <remarks>
/// KeyPrefix 过去在选项里挂着但全仓没有读取点，配了没有任何效果，多应用共用同一个 Redis 实例时无法隔离。
/// 前缀一旦生效就必须同时作用于「写键」「查键模式」「剥前缀还原业务键」三条路径，
/// 少覆盖任何一条，按模式清理都会把别的应用的键当成自己的业务键。这里逐条锁住三条路径，
/// 并用未配置前缀的反例保证默认键格式与历史逐字节一致。
/// </remarks>
public class DistributedCacheKeyPrefixTests
{
    /// <summary>
    /// 配置了键前缀后，后端键在规范化键外层再带上该前缀
    /// </summary>
    [Fact]
    public void Set_WithKeyPrefix_WritesPrefixedNormalizedKey()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store, new XiHanDistributedCacheOptions { KeyPrefix = "app1:" });
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Set("k1", new SampleCacheItem { Value = "v1" });

        Assert.Contains("app1:0:sample:k1", store.StoredKeys);
        Assert.DoesNotContain("0:sample:k1", store.StoredKeys);
    }

    /// <summary>
    /// 带前缀写入后仍可按同一业务键读回
    /// </summary>
    [Fact]
    public void Set_WithKeyPrefix_ThenGet_RoundTrips()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store, new XiHanDistributedCacheOptions { KeyPrefix = "app1:" });
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Set("k1", new SampleCacheItem { Value = "v1" });

        Assert.Equal("v1", cache.Get("k1")?.Value);
    }

    /// <summary>
    /// 未配置前缀时键格式与历史一致
    /// </summary>
    /// <remarks>
    /// 反例：前缀默认空串，拼接不得凭空补出分隔符，否则升级期间新旧实例读不到彼此写的数据。
    /// </remarks>
    [Fact]
    public void Set_WithoutKeyPrefix_KeepsLegacyNormalizedKey()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store);
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Set("k1", new SampleCacheItem { Value = "v1" });

        Assert.Contains("0:sample:k1", store.StoredKeys);
    }

    /// <summary>
    /// 前缀不同的两个应用写同一业务键时互不覆盖
    /// </summary>
    /// <remarks>
    /// 这正是该选项存在的理由：同一个缓存实例上多应用靠前缀隔离。
    /// </remarks>
    [Fact]
    public void Set_WithDifferentKeyPrefixes_DoesNotCollideOnSameBusinessKey()
    {
        var store = new FakeDistributedCacheStore();
        using var firstContext = new DistributedCacheTestContext(store, new XiHanDistributedCacheOptions { KeyPrefix = "app1:" });
        using var secondContext = new DistributedCacheTestContext(store, new XiHanDistributedCacheOptions { KeyPrefix = "app2:" });
        var firstCache = firstContext.CreateStringKeyed<SampleCacheItem>();
        var secondCache = secondContext.CreateStringKeyed<SampleCacheItem>();

        firstCache.Set("k1", new SampleCacheItem { Value = "one" });
        secondCache.Set("k1", new SampleCacheItem { Value = "two" });

        Assert.Equal("one", firstCache.Get("k1")?.Value);
        Assert.Equal("two", secondCache.Get("k1")?.Value);
    }

    /// <summary>
    /// 按模式查键时下发给后端的模式同样带上前缀
    /// </summary>
    [Fact]
    public void GetKeys_WithKeyPrefix_PassesPrefixedPatternToBackend()
    {
        var store = new FakeCapableDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store, new XiHanDistributedCacheOptions { KeyPrefix = "app1:" });
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.GetKeys("user:*");

        Assert.Equal("app1:0:sample:user:*", store.LastPattern);
    }

    /// <summary>
    /// 按模式取回的键把含应用前缀的整段前缀剥掉，只留业务键
    /// </summary>
    [Fact]
    public void GetKeys_WithKeyPrefix_StripsWholePrefix()
    {
        var store = new FakeCapableDistributedCacheStore
        {
            PatternKeys = ["app1:0:sample:a", "app1:0:sample:b"]
        };
        using var context = new DistributedCacheTestContext(store, new XiHanDistributedCacheOptions { KeyPrefix = "app1:" });
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        var keys = cache.GetKeys();

        Assert.Equal(new[] { "a", "b" }, keys);
    }

    /// <summary>
    /// 移除时下发给后端的也是带前缀的键
    /// </summary>
    [Fact]
    public void Remove_WithKeyPrefix_PassesPrefixedKeyToBackend()
    {
        var store = new FakeDistributedCacheStore();
        using var context = new DistributedCacheTestContext(store, new XiHanDistributedCacheOptions { KeyPrefix = "app1:" });
        var cache = context.CreateStringKeyed<SampleCacheItem>();

        cache.Remove("k1");

        Assert.Equal(["app1:0:sample:k1"], store.RemovedKeys);
    }
}
