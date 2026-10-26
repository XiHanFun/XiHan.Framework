// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.InMemory.Tests;

/// <summary>
/// 健康项 <c>caching.inmemory.capacity</c>：宿主启动后 Ready，任一张 LRU 表填满后 Degraded 并带 <see cref="CachingErrors.CapacityReached"/>。
/// </summary>
public sealed class InMemoryCachingHealthTests
{
    [Fact]
    public async Task Health_IsReadyUntilATableIsFull_ThenDegraded()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>(), [new("XiHan:Caching:InMemory:MaxEntries", "2")]);

        var ready = Assert.Single(host.App.Health().Contributions, static c => c.Name == InMemoryCachingFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, ready.State);

        var cache = host.Services.GetRequiredService<ISharedCache>();
        await cache.SetAsync(new CacheKey("a"), "1", CacheEntryOptions.Default, CancellationToken.None);
        await cache.SetAsync(new CacheKey("b"), "2", CacheEntryOptions.Default, CancellationToken.None);

        var health = host.App.Health();
        var degraded = Assert.Single(health.Contributions, static c => c.Name == InMemoryCachingFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, degraded.State);
        Assert.Equal(CachingErrors.CapacityReached, degraded.Error?.Code);
        Assert.Equal(CachingMessages.CapacityReached, degraded.Error?.MessageKey);
        Assert.Equal(2, degraded.Error?.Details?["capacity"]);
        // 条目 Degraded 只把应用压成 Degraded，仍就绪
        Assert.True(health.Ready);
        Assert.Equal(LifecycleState.Degraded, health.State);
    }
}
