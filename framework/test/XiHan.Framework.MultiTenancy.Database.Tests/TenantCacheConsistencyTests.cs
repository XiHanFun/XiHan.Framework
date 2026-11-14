// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.Abstractions;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.Hybrid;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Caching.Redis;
using XiHan.Framework.Kernel;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 租户目录走 ICache、分片解析器不再另行缓存：多实例一致与缓存后端故障（ADR-0229）
/// </summary>
/// <remarks>
/// 两个宿主共用一个库与一份缓存时，一个实例停用租户、改连接串、删除租户，另一个实例的下一次解析就用上新结果；缓存后端故障时解析直接查库，写入时的失效失败让写入回滚
/// </remarks>
public sealed class TenantCacheConsistencyTests
{
    [Fact]
    public async Task InstancesSharingTheCache_UseTheNewTenantAndShard_WithoutWaitingForExpiry()
    {
        await using var database = new SqliteDatabase();
        var shared = new FaultyCache(NewInMemoryCache());
        var first = await database.StartHostAsync(services: s => s.AddSingleton<ICache>(shared));
        var second = await database.StartHostAsync(services: s => s.AddSingleton<ICache>(shared));
        await TenantHosts.SeedAsync(first, [Acme("Host=old")]);
        await AssertSeesAsync(second, enabled: true, "Host=old");

        // 迁库：第一个实例改连接串，第二个实例的下一次解析就指向新库
        await TenantHosts.SeedAsync(first, [Acme("Host=new")]);
        await AssertSeesAsync(second, enabled: true, "Host=new");

        // 停用：第二个实例立即拒绝该租户的调用
        await TenantHosts.SeedAsync(first, [Acme("Host=new") with { IsEnabled = false }]);
        Assert.Equal(MultiTenancyErrors.Disabled, (await second.InvokeAsync(static _ => ValueTask.CompletedTask, baggage: Tenant("acme"))).Error?.Code);

        // 删除：第二个实例看不到租户，也解析不出分片
        (await first.InvokeAsync(static async context => (await context.Services.GetRequiredService<TenantRecordStore>().RemoveAsync(["acme"], context.CancellationToken)).Single().ThrowIfFailure())).ThrowIfFailure();
        Assert.False((await second.Services.GetRequiredService<ITenantStore>().FindAsync("acme", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.False((await second.Services.GetRequiredService<ITenantShardResolver>().ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().HasValue);
    }

    [Fact]
    public async Task CacheOutage_ResolutionReadsTheDatabase()
    {
        var cache = new FaultyCache(NewInMemoryCache());
        await using var host = await TenantHosts.StartAsync(services: s => s.AddSingleton<ICache>(cache));
        await TenantHosts.SeedAsync(host, [Acme("Host=a")]);

        // 缓存后端不可达：目录直接查库，解析与分片照常
        cache.Failing = true;
        await AssertSeesAsync(host, enabled: true, "Host=a");
        Assert.False((await host.Services.GetRequiredService<ITenantStore>().FindAsync("ghost", CancellationToken.None)).ValueOrThrow().HasValue);
    }

    [Fact]
    public async Task CacheOutageDuringTheWrite_FailsTheCall_AndRollsTheWriteBack()
    {
        var cache = new FaultyCache(NewInMemoryCache());
        await using var host = await TenantHosts.StartAsync(services: s => s.AddSingleton<ICache>(cache));
        await TenantHosts.SeedAsync(host, [Acme("Host=a")]);

        // 停用传不到别的实例：调用失败、事务回滚，租户仍是启用
        cache.Failing = true;
        var disabled = await host.InvokeAsync(static async context => await context.Services.GetRequiredService<TenantRecordStore>().UpsertAsync([Acme("Host=a") with { IsEnabled = false }], context.CancellationToken));
        Assert.Equal(KernelErrors.Unexpected, disabled.Error?.Code);
        Assert.Equal(CachingErrors.StoreUnavailable, disabled.Error?.Inner?.Code);

        cache.Failing = false;
        Assert.True((await host.Services.GetRequiredService<ITenantStore>().ListAsync(new TenantQuery(), CancellationToken.None)).ValueOrThrow().Items.Single().IsEnabled);
        await AssertSeesAsync(host, enabled: true, "Host=a");
    }

    [Fact]
    public async Task TwoInstancesOverRedisAndHybridCaching_UseTheNewTenantAndShard()
    {
        var redis = Environment.GetEnvironmentVariable("XIHAN_TEST_REDIS");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(redis), "Set the environment variable 'XIHAN_TEST_REDIS' to a Redis instance to run this test.");
        await using var database = new SqliteDatabase();
        // 存活时间都设得很长：另一个实例用上新结果只能靠失效，不能靠过期
        KeyValuePair<string, string?>[] settings =
        [
            new("XiHan:Caching:Redis:Configuration", redis),
            new("XiHan:Caching:Redis:KeyPrefix", $"xihan-test:{Guid.NewGuid():N}:"),
            new("XiHan:Caching:Hybrid:LocalTimeToLiveSeconds", "600"),
            new("XiHan:MultiTenancy:Database:CacheTimeToLiveSeconds", "600"),
        ];
        var first = await database.StartHostAsync(settings, static b => b.UseFeature<RedisCachingFeature>().UseFeature<HybridCachingFeature>());
        var second = await database.StartHostAsync(settings, static b => b.UseFeature<RedisCachingFeature>().UseFeature<HybridCachingFeature>());
        try
        {
            Assert.True(Assert.IsType<HybridObjectCache>(second.Services.GetRequiredService<ICache>()).InvalidatesAcrossInstances);
            await TenantHosts.SeedAsync(first, [Acme("Host=old")]);
            await AssertSeesAsync(second, enabled: true, "Host=old");
            await AssertSeesAsync(second, enabled: true, "Host=old");

            // 迁库与停用：Redis 频道把失效带到第二个实例（通知是异步的，最多等几秒，远小于存活时间）
            await TenantHosts.SeedAsync(first, [Acme("Host=new")]);
            await WaitUntilAsync(async () => await ConnectionOfAsync(second) == "Host=new");
            await TenantHosts.SeedAsync(first, [Acme("Host=new") with { IsEnabled = false }]);
            await WaitUntilAsync(async () => (await second.InvokeAsync(static _ => ValueTask.CompletedTask, baggage: Tenant("acme"))).Error?.Code == MultiTenancyErrors.Disabled);
        }
        finally
        {
            // 清掉本用例在 Redis 里的键
            var inspector = Assert.IsAssignableFrom<ICacheStoreInspector>(first.Services.GetRequiredService<ICacheStore>());
            _ = (await inspector.RemoveByPrefixAsync(DatabaseTenantStore.CacheRegion + ":", CancellationToken.None)).ValueOrThrow();
        }
    }

    /// <summary>
    /// 自报租户的 Baggage
    /// </summary>
    private static Dictionary<string, string> Tenant(string tenantId) => new() { [BaggageKeys.Tenant] = tenantId };

    /// <summary>
    /// 租户 acme，分库到给定的连接串
    /// </summary>
    private static TenantInfo Acme(string connectionString) => new()
    {
        Id = "acme",
        DisplayName = "Acme",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = connectionString },
    };

    /// <summary>
    /// 一份独立的内存缓存，可登记进几个宿主共用
    /// </summary>
    private static InMemoryObjectCache NewInMemoryCache()
    {
        var options = new InMemoryCachingOptions();
        return new InMemoryObjectCache(new InMemoryCacheStore(options, TimeProvider.System), options, TimeProvider.System);
    }

    /// <summary>
    /// 宿主经 Tenant 阶段解析 acme，核对启用标记与分片的连接串
    /// </summary>
    private static async Task AssertSeesAsync(TestHost host, bool enabled, string connectionString)
    {
        Assert.Equal(enabled, (await host.Services.GetRequiredService<ITenantStore>().FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().IsEnabled);
        Assert.Equal(connectionString, await ConnectionOfAsync(host));
    }

    /// <summary>
    /// 一次调用里 Tenant 阶段为 acme 绑定的分片的连接串
    /// </summary>
    private static async Task<string?> ConnectionOfAsync(TestHost host)
    {
        string? connection = null;
        (await host.InvokeAsync(ctx =>
        {
            connection = ctx.FindTenantShard().ValueOrThrow().ConnectionString;
            return ValueTask.CompletedTask;
        }, baggage: Tenant("acme"))).ThrowIfFailure();
        return connection;
    }

    /// <summary>
    /// 轮询到条件成立，最多 5 秒
    /// </summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The other instance did not see the change within 5 seconds.");
            await Task.Delay(20);
        }
    }
}
