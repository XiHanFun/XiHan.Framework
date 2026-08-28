// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using XiHan.Framework.Caching.Distributed;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Core.Exceptions.Abstracts;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Threading;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 曦寒混合缓存测试
/// </summary>
/// <remarks>
/// 容器里不注册二级分布式缓存，让底层混合缓存只跑进程内一级缓存，断言因此不依赖任何外部服务。
/// 重点是「业务键 → 规范化键」这层包装：不同缓存项类型共用同一个底层缓存实例时不能互相覆盖。
/// </remarks>
public class XiHanHybridCacheTests
{
    /// <summary>
    /// 首次取值执行工厂，再次取值直接命中
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GetOrCreateAsync_SecondCall_DoesNotInvokeFactory()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var cache = CreateCache<SampleCacheItem>(provider);
        var calls = 0;

        var first = await cache.GetOrCreateAsync("k1", () =>
        {
            calls++;
            return Task.FromResult(new SampleCacheItem { Value = "v1" });
        }, token: token);
        var second = await cache.GetOrCreateAsync("k1", () =>
        {
            calls++;
            return Task.FromResult(new SampleCacheItem { Value = "v2" });
        }, token: token);

        Assert.Equal("v1", first?.Value);
        Assert.Equal("v1", second?.Value);
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// 不同业务键各自取值
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task GetOrCreateAsync_ForDifferentKeys_KeepsValuesSeparate()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var cache = CreateCache<SampleCacheItem>(provider);

        var first = await cache.GetOrCreateAsync("k1", () => Task.FromResult(new SampleCacheItem { Value = "a" }), token: token);
        var second = await cache.GetOrCreateAsync("k2", () => Task.FromResult(new SampleCacheItem { Value = "b" }), token: token);

        Assert.Equal("a", first?.Value);
        Assert.Equal("b", second?.Value);
    }

    /// <summary>
    /// 不同缓存项类型共用底层缓存时，同名业务键互不覆盖
    /// </summary>
    /// <remarks>
    /// 底层混合缓存只认字符串键，隔离完全依赖上层把缓存名拼进规范化键。
    /// 这层包装一旦失效，两个类型会读到对方的负载，而且因为字段同形往往不会抛错，只会静默返回错数据。
    /// </remarks>
    [Fact(Timeout = 60_000)]
    public async Task GetOrCreateAsync_ForDifferentItemTypes_DoesNotCollideOnSameBusinessKey()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var sampleCache = CreateCache<SampleCacheItem>(provider);
        var neutralCache = CreateCache<TenantNeutralCacheItem>(provider);

        var sample = await sampleCache.GetOrCreateAsync(
            "shared",
            () => Task.FromResult(new SampleCacheItem { Value = "sample" }),
            token: token);
        var neutral = await neutralCache.GetOrCreateAsync(
            "shared",
            () => Task.FromResult(new TenantNeutralCacheItem { Value = "neutral" }),
            token: token);

        Assert.Equal("sample", sample?.Value);
        Assert.Equal("neutral", neutral?.Value);
    }

    /// <summary>
    /// 显式写入后取值直接命中，不执行工厂
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task SetAsync_ThenGetOrCreateAsync_ReturnsStoredValue()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var cache = CreateCache<SampleCacheItem>(provider);
        var invoked = false;

        await cache.SetAsync("k1", new SampleCacheItem { Value = "stored" }, token: token);
        var value = await cache.GetOrCreateAsync("k1", () =>
        {
            invoked = true;
            return Task.FromResult(new SampleCacheItem { Value = "built" });
        }, token: token);

        Assert.Equal("stored", value?.Value);
        Assert.False(invoked);
    }

    /// <summary>
    /// 移除后再次取值重新执行工厂
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task RemoveAsync_InvalidatesEntry()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var cache = CreateCache<SampleCacheItem>(provider);
        await cache.SetAsync("k1", new SampleCacheItem { Value = "stored" }, token: token);

        await cache.RemoveAsync("k1", token: token);
        var value = await cache.GetOrCreateAsync("k1", () => Task.FromResult(new SampleCacheItem { Value = "rebuilt" }), token: token);

        Assert.Equal("rebuilt", value?.Value);
    }

    /// <summary>
    /// 批量移除让每个键都失效
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task RemoveManyAsync_InvalidatesEveryKey()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var cache = CreateCache<SampleCacheItem>(provider);
        await cache.SetAsync("k1", new SampleCacheItem { Value = "a" }, token: token);
        await cache.SetAsync("k2", new SampleCacheItem { Value = "b" }, token: token);

        await cache.RemoveManyAsync(["k1", "k2"], token: token);

        var first = await cache.GetOrCreateAsync("k1", () => Task.FromResult(new SampleCacheItem { Value = "a2" }), token: token);
        var second = await cache.GetOrCreateAsync("k2", () => Task.FromResult(new SampleCacheItem { Value = "b2" }), token: token);

        Assert.Equal("a2", first?.Value);
        Assert.Equal("b2", second?.Value);
    }

    /// <summary>
    /// 移除只作用于给定的键
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task RemoveAsync_LeavesOtherKeysIntact()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var cache = CreateCache<SampleCacheItem>(provider);
        await cache.SetAsync("k1", new SampleCacheItem { Value = "a" }, token: token);
        await cache.SetAsync("k2", new SampleCacheItem { Value = "b" }, token: token);

        await cache.RemoveAsync("k1", token: token);

        var kept = await cache.GetOrCreateAsync("k2", () => Task.FromResult(new SampleCacheItem { Value = "rebuilt" }), token: token);

        Assert.Equal("b", kept?.Value);
    }

    /// <summary>
    /// 单泛型混合缓存把操作转发给内部缓存
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task SingleGenericCache_DelegatesToInternalCache()
    {
        var token = TestContext.Current.CancellationToken;
        using var provider = BuildProvider();
        var inner = CreateCache<SampleCacheItem>(provider);
        var facade = new XiHanHybridCache<SampleCacheItem>(inner);

        await facade.SetAsync("k1", new SampleCacheItem { Value = "stored" }, token: token);
        var value = await facade.GetOrCreateAsync("k1", () => Task.FromResult(new SampleCacheItem { Value = "built" }), token: token);
        await facade.RemoveAsync("k1", token: token);
        var rebuilt = await facade.GetOrCreateAsync("k1", () => Task.FromResult(new SampleCacheItem { Value = "rebuilt" }), token: token);

        Assert.Same(inner, facade.InternalCache);
        Assert.Equal("stored", value?.Value);
        Assert.Equal("rebuilt", rebuilt?.Value);
    }

    /// <summary>
    /// 构建含混合缓存与异常通知的容器
    /// </summary>
    /// <remarks>
    /// 刻意不注册 IDistributedCache，底层混合缓存因此只跑一级缓存，用例不会触碰任何外部存储。
    /// </remarks>
    /// <returns>服务提供者</returns>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        services.AddSingleton<IExceptionNotifier>(new RecordingExceptionNotifier());
        services.AddSingleton<ICurrentTenantAccessor>(new FakeCurrentTenantAccessor());

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建指定缓存项类型的混合缓存
    /// </summary>
    /// <typeparam name="TCacheItem">缓存项类型</typeparam>
    /// <param name="provider">服务提供者</param>
    /// <returns>混合缓存</returns>
    private static XiHanHybridCache<TCacheItem, string> CreateCache<TCacheItem>(ServiceProvider provider)
        where TCacheItem : class
    {
        return new XiHanHybridCache<TCacheItem, string>(
            provider,
            Microsoft.Extensions.Options.Options.Create(new XiHanHybridCacheOptions()),
            provider.GetRequiredService<HybridCache>(),
            new FakeDistributedCacheStore(),
            NullCancellationTokenProvider.Instance,
            new JsonDistributedCacheSerializer(
                Microsoft.Extensions.Options.Options.Create(new JsonSerializerOptions())),
            new DefaultDistributedCacheKeyNormalizer(provider),
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FakeUnitOfWorkManager());
    }
}
