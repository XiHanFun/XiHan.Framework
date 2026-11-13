// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Locking;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Remote.Tests;

/// <summary>
/// 远程缓存 Provider 的装配
/// </summary>
/// <remarks>
/// 胜过内存默认实现、共享缓存与分布式锁经 Go 插件工作、插件与本包的同名消息目录合并、插件缺席时按不可用失败
/// </remarks>
public sealed class RemoteCachingTests
{
    [Fact]
    public async Task Feature_WinsOverTheInMemoryDefault_AndServesSharedCacheAndLockThroughTheGoPlugin()
    {
        await using var host = await CacheGoPluginHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>());

        var shared = host.Services.GetRequiredService<ISharedCache>();
        Assert.IsType<SerializingCache>(shared);
        await shared.SetAsync(new CacheKey("greeting"), "hello from go", CacheEntryOptions.Expires(TimeSpan.FromMinutes(1)), CancellationToken.None);
        var cached = await shared.GetAsync<string>(new CacheKey("greeting"), CancellationToken.None);
        Assert.Equal("hello from go", cached.ValueOrThrow());
        Assert.False((await shared.GetAsync<string>(new CacheKey("missing"), CancellationToken.None)).HasValue);

        var locks = host.Services.GetRequiredService<IDistributedLock>();
        var first = await locks.TryAcquireAsync(new CacheKey("orders"), TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.True(first.HasValue);
        Assert.False((await locks.TryAcquireAsync(new CacheKey("orders"), TimeSpan.FromSeconds(30), CancellationToken.None)).HasValue);
        Assert.True(await first.ValueOrThrow().ReleaseAsync(CancellationToken.None));
        Assert.True((await locks.TryAcquireAsync(new CacheKey("orders"), TimeSpan.FromSeconds(30), CancellationToken.None)).HasValue);

        var local = host.Services.GetRequiredService<ICache>();
        Assert.IsType<InMemoryObjectCache>(local);

        // 插件握手带来的 caching.remote 目录（两种语言）与本包的同名目录（十种语言）合并为一份；此前先注册的插件目录胜出，本包其余八种语言的译文被静默丢弃
        var catalog = Assert.Single(host.Services.GetServices<MessageCatalog>(), static c => c.Name == RemoteCachingMessages.CatalogName);
        Assert.Equal(SupportedLocales.All.Count, catalog.Locales.Count);
        Assert.Equal("キャッシュストアがいっぱいです（{capacity} 件）。", catalog.Find(RemoteCachingMessages.CapacityReached, "ja-JP").ValueOrThrow());
    }

    [Fact]
    public async Task Feature_RegistersTheRemoteStoreAsICacheStore_InsteadOfTheInMemoryDefault()
    {
        // 解析服务不触及插件：没有插件也能核对登记
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>().UseFeature<RemoteCachingFeature>());

        var store = host.Services.GetRequiredService<ICacheStore>();

        Assert.IsType<RemoteCacheStore>(store);
        Assert.Same(host.Services.GetRequiredService<RemoteCacheStore>(), store);
        Assert.Same(store, host.Services.GetRequiredService<ISharedCache>().Store);
        Assert.Single(host.Services.GetServices<ICacheStore>());
    }

    [Fact]
    public async Task Store_FailsAsUnavailable_WhenNoPluginProvidesTheOperations()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<RemoteCachingFeature>(), [new("XiHan:Caching:Remote:TimeoutMilliseconds", "500")]);
        var store = host.Services.GetRequiredService<RemoteCacheStore>();

        var exception = await Assert.ThrowsAsync<FrameworkException>(async () => await store.GetAsync(new CacheKey("orders:1"), CancellationToken.None));

        Assert.Equal(CachingErrors.StoreUnavailable, exception.Code);
        Assert.Contains("KERNEL_NOTFOUND_001", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Store_PassesThePluginsValidationError_ThroughWithItsCode()
    {
        // .NET 侧的 CacheEntryOptions 已经校验过；插件仍按操作集拒绝时，CACHING_VALIDATION_002 原样成为异常的错误码，不被改写成不可用
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<RemoteCachingFeature>().UseFeature<RejectingPluginFeature>());
        var store = host.Services.GetRequiredService<RemoteCacheStore>();

        var exception = await Assert.ThrowsAsync<FrameworkException>(async () => await store.SetAsync(new CacheKey("orders:1"), new byte[] { 1 }, CacheEntryOptions.Expires(TimeSpan.FromMinutes(1)), CancellationToken.None));

        Assert.Equal(CachingErrors.InvalidOptions, exception.Code);
        Assert.Contains(RemoteCacheOperations.Set, exception.Message, StringComparison.Ordinal);
        // 选项本身不合法时 .NET 侧在调用插件之前就抛 ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.SetAsync(new CacheKey("orders:1"), new byte[] { 1 }, new CacheEntryOptions { TimeToLive = TimeSpan.FromMinutes(1), SlidingExpiration = TimeSpan.FromMinutes(1) }, CancellationToken.None));
    }

    [Fact]
    public void Options_ReadTheTimeoutFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new("XiHan:Caching:Remote:TimeoutMilliseconds", "1500")]).Build();

        Assert.Equal(TimeSpan.FromMilliseconds(1500), RemoteCachingOptions.FromConfiguration(configuration).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(5), RemoteCachingOptions.FromConfiguration(new ConfigurationBuilder().Build()).Timeout);
        Assert.Throws<ArgumentOutOfRangeException>(static () => new RemoteCachingOptions { Timeout = TimeSpan.Zero });

        // 配置了但不合法时抛出，不静默退回 5 秒
        foreach (var invalid in new[] { "0", "-5", "abc", "1.5", "" })
        {
            var bad = new ConfigurationBuilder().AddInMemoryCollection([new("XiHan:Caching:Remote:TimeoutMilliseconds", invalid)]).Build();
            Assert.Throws<ArgumentException>(() => RemoteCachingOptions.FromConfiguration(bad));
        }
    }

    [Fact]
    public void Messages_CoverTheOperationSetErrors()
    {
        var catalog = RemoteCachingMessages.Catalog;
        Assert.Equal(RemoteCachingMessages.CatalogName, catalog.Name);
        Assert.Equal(9, RemoteCacheOperations.All.Count);
        Assert.All(RemoteCacheOperations.All, static id => Assert.StartsWith("cache-entries.", id, StringComparison.Ordinal));
    }
}
