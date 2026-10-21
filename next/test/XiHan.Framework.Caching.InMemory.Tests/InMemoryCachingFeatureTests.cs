// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.InMemory.Tests;

public sealed class InMemoryCachingFeatureTests
{
    /// <summary>
    /// 模拟一个显式的共享缓存 Provider（如 Redis），只声明能力，不注册任何服务。
    /// </summary>
    private sealed class ExplicitSharedCacheFeature : IFeature
    {
        public Manifest Manifest { get; } = TestManifests.Create("xihan.caching.fake-shared", provides: ["caching.distributed@1"], requires: ["kernel.hosting@1"]);

        public void Configure(FeatureContext context)
        {
        }
    }

    [Fact]
    public async Task AsSoleProvider_RegistersLocalAndSharedCache_LockAndIdempotency()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>());

        var local = host.Services.GetRequiredService<ICache>();
        var shared = host.Services.GetRequiredService<ISharedCache>();
        Assert.Same(local.Store, shared.Store);
        Assert.IsType<InMemoryCacheStore>(shared.Store);
        Assert.NotNull(host.Services.GetService<IDistributedLock>());
        Assert.Contains(nameof(IdempotencyMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);

        await shared.SetAsync(new CacheKey("greeting"), "hi", CacheEntryOptions.Default, CancellationToken.None);
        Assert.Equal("hi", (await local.GetAsync<string>(new CacheKey("greeting"), CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task WhenExplicitProviderWinsDistributed_RegistersOnlyLocalCache()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>().UseFeature<ExplicitSharedCacheFeature>());

        Assert.NotNull(host.Services.GetService<ICache>());
        Assert.Null(host.Services.GetService<ISharedCache>());
        Assert.Null(host.Services.GetService<IDistributedLock>());
        Assert.DoesNotContain(nameof(IdempotencyMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.Equal(["caching.local"], host.App.Graph["xihan.caching.inmemory"].SelectedCapabilities);
    }

    [Fact]
    public async Task Idempotency_EndToEnd_ExecutesHandlerOnce()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>());
        var executions = 0;
        var baggage = new Dictionary<string, string> { [BaggageKeys.IdempotencyKey] = "order-42" };

        var first = await host.InvokeAsync(ctx =>
        {
            executions++;
            ctx.SetOutcome(Error.Conflict("ORDERS_CONFLICT_001", "已下单"));
            return ValueTask.CompletedTask;
        }, baggage: baggage);
        var second = await host.InvokeAsync(ctx =>
        {
            executions++;
            return ValueTask.CompletedTask;
        }, baggage: baggage);
        var other = await host.InvokeAsync(ctx =>
        {
            executions++;
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.IdempotencyKey] = "order-43" });

        Assert.Equal(2, executions);
        Assert.Equal("ORDERS_CONFLICT_001", first.Error?.Code);
        Assert.Equal("ORDERS_CONFLICT_001", second.Error?.Code);
        Assert.True(other.IsSuccess);
    }
}
