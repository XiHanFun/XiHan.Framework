// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Hybrid.Tests;

/// <summary>
/// 能力包在宿主里的装配。
/// </summary>
/// <remarks>
/// 顶替内存实现提供 <c>caching.local</c>，L2 取被选中的共享缓存（这里是内存实现的 <c>caching.distributed</c>）。
/// </remarks>
public sealed class HybridCachingFeatureTests
{
    [Fact]
    public async Task Feature_ProvidesLocalCache_OverSelectedSharedCache()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>().UseFeature<HybridCachingFeature>(), [new("XiHan:Caching:Hybrid:LocalCapacity", "5")]);

        var graph = host.App.DescribeGraph();
        Assert.Contains("xihan.caching.hybrid", graph, StringComparison.Ordinal);
        Assert.Contains("requires caching.distributed@1 -> xihan.caching.inmemory", graph, StringComparison.Ordinal);

        var cache = Assert.IsType<HybridCache>(host.Services.GetRequiredService<ICache>());
        var shared = Assert.IsType<InMemoryObjectCache>(host.Services.GetRequiredService<ISharedCache>());
        Assert.Same(shared.Store, cache.Store);

        await cache.SetAsync<string>("k", "v", CacheEntryOptions.Default, CancellationToken.None);
        Assert.Equal("v", (await shared.GetAsync<string>("k", CancellationToken.None)).ValueOrThrow());
        Assert.Equal(1, cache.LocalCount);
    }
}
