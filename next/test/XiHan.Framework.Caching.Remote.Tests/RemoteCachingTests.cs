// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Caching.Remote.Tests;

/// <summary>
/// 远程缓存 Provider 的装配：胜过内存默认实现、共享缓存与分布式锁经 Go 插件工作、插件缺席时按不可用失败。
/// </summary>
public sealed class RemoteCachingTests
{
    [Fact]
    public async Task Feature_WinsOverTheInMemoryDefault_AndServesSharedCacheAndLockThroughTheGoPlugin()
    {
        await using var host = await CacheGoPluginHost.StartAsync(static b => b.UseFeature<InMemoryCachingFeature>());

        var shared = host.Services.GetRequiredService<ISharedCache>();
        Assert.IsType<SerializingCache>(shared);
        await shared.SetAsync("greeting", "hello from go", CacheEntryOptions.Expires(TimeSpan.FromMinutes(1)), CancellationToken.None);
        var cached = await shared.GetAsync<string>("greeting", CancellationToken.None);
        Assert.Equal("hello from go", cached.ValueOrThrow());
        Assert.False((await shared.GetAsync<string>("missing", CancellationToken.None)).HasValue);

        var locks = host.Services.GetRequiredService<IDistributedLock>();
        var first = await locks.TryAcquireAsync("orders", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.True(first.HasValue);
        Assert.False((await locks.TryAcquireAsync("orders", TimeSpan.FromSeconds(30), CancellationToken.None)).HasValue);
        Assert.True(await first.ValueOrThrow().ReleaseAsync(CancellationToken.None));
        Assert.True((await locks.TryAcquireAsync("orders", TimeSpan.FromSeconds(30), CancellationToken.None)).HasValue);

        var local = host.Services.GetRequiredService<ICache>();
        Assert.IsType<InMemoryObjectCache>(local);
    }

    [Fact]
    public async Task Store_FailsAsUnavailable_WhenNoPluginProvidesTheOperations()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<RemoteCachingFeature>(), [new("XiHan:Caching:Remote:TimeoutMilliseconds", "500")]);
        var store = host.Services.GetRequiredService<RemoteCacheStore>();

        var exception = await Assert.ThrowsAsync<FrameworkException>(async () => await store.GetAsync("orders:1", CancellationToken.None));

        Assert.Equal(CachingErrors.StoreUnavailable, exception.Code);
        Assert.Contains("KERNEL_NOTFOUND_001", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_ReadTheTimeoutFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([new("XiHan:Caching:Remote:TimeoutMilliseconds", "1500")]).Build();

        Assert.Equal(TimeSpan.FromMilliseconds(1500), RemoteCachingOptions.FromConfiguration(configuration).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(5), RemoteCachingOptions.FromConfiguration(new ConfigurationBuilder().Build()).Timeout);
        Assert.Throws<ArgumentOutOfRangeException>(static () => new RemoteCachingOptions { Timeout = TimeSpan.Zero });
    }

    [Fact]
    public void Messages_CoverTheOperationSetErrors()
    {
        var catalog = RemoteCachingMessages.Catalog;
        Assert.Equal(RemoteCachingMessages.CatalogName, catalog.Name);
        Assert.Equal(8, RemoteCacheOperations.All.Count);
        Assert.All(RemoteCacheOperations.All, static id => Assert.StartsWith("cache-entries.", id, StringComparison.Ordinal));
    }
}
