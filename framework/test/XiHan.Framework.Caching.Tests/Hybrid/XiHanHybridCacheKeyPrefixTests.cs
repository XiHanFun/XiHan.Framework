// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.Tests.Fakes;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Threading;

namespace XiHan.Framework.Caching.Tests.Hybrid;

/// <summary>
/// 曦寒混合缓存应用级键前缀测试
/// </summary>
/// <remarks>
/// 与分布式缓存的 KeyPrefix 是同一类问题：选项挂着但没有读取点，配了没有效果。
/// 混合缓存只有 NormalizeKey 一处出键，一级与二级缓存都走它，所以用二级缓存替身的存取来观测最终键：
/// 把值按带前缀的键预置进二级缓存，命中即说明前缀确实拼进去了。
/// </remarks>
public class XiHanHybridCacheKeyPrefixTests
{
    /// <summary>
    /// 配置了键前缀后，二级缓存按带前缀的键命中，不再执行工厂
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GetOrCreateAsync_WithKeyPrefix_ReadsSecondLevelCacheByPrefixedKey()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var store = new FakeDistributedCacheStore();
        store.Set("app1:0:sample:k1", Serialize("from-l2"), new DistributedCacheEntryOptions());
        var cache = CreateCache(provider, store, "app1:");
        var invoked = false;

        var value = await cache.GetOrCreateAsync("k1", () =>
        {
            invoked = true;

            return Task.FromResult(new SampleCacheItem { Value = "built" });
        }, hideErrors: false, considerUow: true, token: token);

        Assert.Equal("from-l2", value?.Value);
        Assert.False(invoked);
    }

    /// <summary>
    /// 配置了键前缀后，不会去读没有前缀的旧键
    /// </summary>
    /// <remarks>
    /// 反例：前缀若没拼上，这条会误命中不属于本应用的数据，正是该选项要避免的串数据场景。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public async Task GetOrCreateAsync_WithKeyPrefix_DoesNotReadUnprefixedKey()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var store = new FakeDistributedCacheStore();
        store.Set("0:sample:k1", Serialize("from-l2"), new DistributedCacheEntryOptions());
        var cache = CreateCache(provider, store, "app1:");

        var value = await cache.GetOrCreateAsync(
            "k1",
            () => Task.FromResult(new SampleCacheItem { Value = "built" }),
            hideErrors: false,
            considerUow: true,
            token: token);

        Assert.Equal("built", value?.Value);
    }

    /// <summary>
    /// 未配置前缀时键格式与历史一致
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GetOrCreateAsync_WithoutKeyPrefix_KeepsLegacyNormalizedKey()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var store = new FakeDistributedCacheStore();
        store.Set("0:sample:k1", Serialize("from-l2"), new DistributedCacheEntryOptions());
        var cache = CreateCache(provider, store, string.Empty);
        var invoked = false;

        var value = await cache.GetOrCreateAsync("k1", () =>
        {
            invoked = true;

            return Task.FromResult(new SampleCacheItem { Value = "built" });
        }, hideErrors: false, considerUow: true, token: token);

        Assert.Equal("from-l2", value?.Value);
        Assert.False(invoked);
    }

    /// <summary>
    /// 前缀不同的两个应用共用同一份底层混合缓存时，同名业务键互不覆盖
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GetOrCreateAsync_WithDifferentKeyPrefixes_DoesNotCollideOnSameBusinessKey()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var store = new FakeDistributedCacheStore();
        var firstCache = CreateCache(provider, store, "app1:");
        var secondCache = CreateCache(provider, store, "app2:");

        var first = await firstCache.GetOrCreateAsync(
            "shared",
            () => Task.FromResult(new SampleCacheItem { Value = "one" }),
            hideErrors: false,
            token: token);
        var second = await secondCache.GetOrCreateAsync(
            "shared",
            () => Task.FromResult(new SampleCacheItem { Value = "two" }),
            hideErrors: false,
            token: token);

        Assert.Equal("one", first?.Value);
        Assert.Equal("two", second?.Value);
    }

    /// <summary>
    /// 按二级缓存的序列化口径产出字节
    /// </summary>
    /// <param name="value">缓存项的值</param>
    /// <returns>字节内容</returns>
    private static byte[] Serialize(string value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new SampleCacheItem { Value = value }, new JsonSerializerOptions());
    }

    /// <summary>
    /// 构建含混合缓存与确定性序列化器的容器
    /// </summary>
    /// <remarks>
    /// 显式注册 <c>IHybridCacheSerializer&lt;SampleCacheItem&gt;</c>，让用例预置的字节与
    /// <c>ResolveSerializer</c> 解析出的序列化器口径一致，不依赖底层默认工厂的实现细节。
    /// 刻意不注册 IDistributedCache，底层混合缓存因此只跑一级缓存，二级缓存替身只由本用例直接传入。
    /// </remarks>
    /// <returns>服务提供者</returns>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        services.AddSingleton<IExceptionNotifier>(new RecordingExceptionNotifier());
        services.AddSingleton<ICurrentTenantAccessor>(new FakeCurrentTenantAccessor());
        services.AddSingleton<IHybridCacheSerializer<SampleCacheItem>>(
            new XiHanHybridCacheJsonSerializer<SampleCacheItem>(new JsonSerializerOptions()));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建指定键前缀的混合缓存
    /// </summary>
    /// <param name="provider">服务提供者</param>
    /// <param name="distributedCache">二级缓存替身</param>
    /// <param name="keyPrefix">应用级键前缀</param>
    /// <returns>混合缓存</returns>
    private static XiHanHybridCache<SampleCacheItem, string> CreateCache(
        ServiceProvider provider,
        IDistributedCache distributedCache,
        string keyPrefix)
    {
        return new XiHanHybridCache<SampleCacheItem, string>(
            provider,
            Microsoft.Extensions.Options.Options.Create(new XiHanHybridCacheOptions { KeyPrefix = keyPrefix }),
            provider.GetRequiredService<HybridCache>(),
            distributedCache,
            NullCancellationTokenProvider.Instance,
            new JsonDistributedCacheSerializer(
                Microsoft.Extensions.Options.Options.Create(new JsonSerializerOptions())),
            new DefaultDistributedCacheKeyNormalizer(provider),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeUnitOfWorkManager());
    }
}
