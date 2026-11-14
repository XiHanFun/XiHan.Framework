// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Caching.Abstractions.Caches;
using XiHan.Framework.Caching.Abstractions.Stores;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Resolution;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 数据库 Provider 特有的行为
/// </summary>
/// <remarks>
/// 显式 Provider 顶替内存实现、Tenant 阶段解析数据库里的租户、目录缓存的命中 / 失效 / 存活时间 / 容量、批量写入与删除及其对分片解析器的失效、枚举上限、基础设施异常原样抛出、选项解析
/// </remarks>
public sealed class DatabaseMultiTenancyTests
{
    /// <summary>
    /// 配置节前缀
    /// </summary>
    private const string Section = DatabaseMultiTenancyOptions.ConfigurationSection + ":";

    /// <summary>
    /// 带 Baggage 租户的调用
    /// </summary>
    private static Dictionary<string, string> Tenant(string tenantId) => new() { [BaggageKeys.Tenant] = tenantId };

    /// <summary>
    /// 分库或共库的 acme 租户
    /// </summary>
    private static TenantInfo Acme(string displayName = "Acme", string? connectionString = null)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (connectionString is not null)
        {
            properties[TenantShardKeys.ConnectionString] = connectionString;
        }

        return new TenantInfo { Id = "acme", DisplayName = displayName, Properties = properties };
    }

    /// <summary>
    /// 用内存键值对构建配置
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(Section + v.Key, v.Value))).Build();

    [Fact]
    public async Task Feature_OverridesInMemory_AndResolvesTenantsFromDatabase()
    {
        await using var host = await TestHost.StartAsync(static b => b
            .UseFeature<InMemoryPersistenceFeature>()
            .UseFeature<InMemoryLocalizationFeature>()
            .UseFeature<InMemoryMultiTenancyFeature>()
            .UseFeature<InMemoryCachingFeature>().UseFeature<DatabaseMultiTenancyFeature>());

        // 显式 Provider 拿到两个能力，默认的内存实现一个都没选中、不被装配
        Assert.Equal(
            [MultiTenancyCapabilities.Context, MultiTenancyCapabilities.Sharding],
            host.App.Graph["xihan.multitenancy.database"].SelectedCapabilities.OrderBy(static c => c, StringComparer.Ordinal));
        Assert.Null(host.Services.GetService<InMemoryTenantStore>());
        Assert.IsType<DatabaseTenantStore>(host.Services.GetRequiredService<ITenantStore>());
        Assert.IsType<TenantStoreShardResolver>(host.Services.GetRequiredService<ITenantShardResolver>());
        Assert.Equal(2, host.Services.GetServices<ITenantResolver>().Count());
        Assert.Contains(nameof(TenantResolutionMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);

        await TenantHosts.SeedAsync(host, [Acme(), new TenantInfo { Id = "beta", IsEnabled = false }]);

        TenantContext? seen = null;
        var ok = await host.InvokeAsync(ctx =>
        {
            seen = ctx.GetSecurity().Tenant;
            return ValueTask.CompletedTask;
        }, baggage: Tenant("acme"));
        var disabled = await host.InvokeAsync(static _ => ValueTask.CompletedTask, baggage: Tenant("beta"));
        var missing = await host.InvokeAsync(static _ => ValueTask.CompletedTask, baggage: Tenant("nobody"));
        var none = await host.InvokeAsync(static _ => ValueTask.CompletedTask);

        Assert.True(ok.IsSuccess, ok.ToString());
        Assert.Equal("acme", seen?.Id);
        Assert.Equal("Acme", seen?.DisplayName);
        Assert.Equal(MultiTenancyErrors.Disabled, disabled.Error?.Code);
        Assert.Equal(MultiTenancyErrors.NotFound, missing.Error?.Code);
        Assert.True(none.IsSuccess, none.ToString());
    }

    [Fact]
    public async Task Start_FailsFast_WithoutPersistenceProvider()
    {
        // 清单不能声明 persistence.relational@1（会与持久化 Provider 的可选分片依赖成环），所以在启动期核对
        var exception = await Assert.ThrowsAsync<FrameworkException>(static async () =>
            await TestHost.StartAsync(static b => b.UseFeature<InMemoryLocalizationFeature>().UseFeature<InMemoryCachingFeature>().UseFeature<DatabaseMultiTenancyFeature>()));

        // 宿主把特性启动失败包成 FeatureStartFailed，本包的缺失能力错误在内部异常里
        Assert.Equal(KernelErrors.FeatureStartFailed, exception.Code);
        var inner = Assert.IsType<FrameworkException>(exception.InnerException);
        Assert.Equal(KernelErrors.MissingCapability, inner.Code);
        Assert.Contains("persistence.relational@1", inner.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Find_CachesHitsAndMisses_UntilInvalidated()
    {
        await using var host = await TenantHosts.StartAsync();
        var store = host.Services.GetRequiredService<DatabaseTenantStore>();
        await TenantHosts.SeedAsync(host, [Acme()]);

        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);
        Assert.False((await store.FindAsync("ghost", CancellationToken.None)).ValueOrThrow().HasValue);

        // 绕过 TenantRecordStore 直接改表：命中缓存看不到变化，“不存在”同样被缓存
        await TenantHosts.EditRowAsync(host, "acme", static row => row.DisplayName = "Acme 2");
        await TenantHosts.AddRowAsync(host, new TenantRecord { Id = "ghost", DisplayName = "Ghost" });
        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);
        Assert.False((await store.FindAsync("ghost", CancellationToken.None)).ValueOrThrow().HasValue);

        // 单个失效只重查该租户（空集合什么也不做）；整体失效后两者都重查
        await store.InvalidateAsync([], CancellationToken.None);
        await store.InvalidateAsync(["acme"], CancellationToken.None);
        Assert.Equal("Acme 2", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);
        Assert.False((await store.FindAsync("ghost", CancellationToken.None)).ValueOrThrow().HasValue);
        await store.InvalidateAllAsync(CancellationToken.None);
        Assert.Equal("Ghost", (await store.FindAsync("ghost", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);

        // 不合法的 ID 不查库也不占缓存；枚举不走缓存
        var cache = host.Services.GetRequiredService<InMemoryObjectCache>();
        var before = cache.Count;
        Assert.False((await store.FindAsync("bad id", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.Equal(before, cache.Count);
        Assert.Equal(["acme", "ghost"], (await store.ListAsync(new TenantQuery(), CancellationToken.None)).ValueOrThrow().Items.Select(static t => t.Id));
    }

    [Fact]
    public async Task Find_ExpiresCachedEntries_ByTimeToLive()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));
        await using var host = await TenantHosts.StartAsync(services: services => services.AddSingleton<TimeProvider>(clock));
        var store = host.Services.GetRequiredService<DatabaseTenantStore>();
        await TenantHosts.SeedAsync(host, [Acme()]);
        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);
        await TenantHosts.EditRowAsync(host, "acme", static row => row.DisplayName = "Acme 2");

        // 默认 30 秒：29 秒后仍命中，到期后重查
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("Acme 2", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);

        // 存活时间不再接受 0（不过期）：装配即失败
        await Assert.ThrowsAsync<ArgumentException>(static async () => await TenantHosts.StartAsync([new(Section + "CacheTimeToLiveSeconds", "0")]));
    }

    [Fact]
    public async Task Find_KeepsEntriesInTheHostsICache_UnderItsRegion()
    {
        await using var host = await TenantHosts.StartAsync();
        var store = host.Services.GetRequiredService<DatabaseTenantStore>();
        var cache = host.Services.GetRequiredService<InMemoryObjectCache>();
        Assert.Same(cache, host.Services.GetRequiredService<ICache>());
        Assert.Contains("caching.local@1", host.App.Graph["xihan.multitenancy.database"].Manifest.Requires.Select(static r => r.ToString()));
        await TenantHosts.SeedAsync(host, [new TenantInfo { Id = "a" }]);

        // 区代数、两个租户的范围代数与两个条目（含不存在的租户），键都在区名之下；容量由内存缓存的上限管
        Assert.True((await store.FindAsync("a", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.False((await store.FindAsync("b", CancellationToken.None)).ValueOrThrow().HasValue);
        var page = (await cache.ListKeysAsync(new CacheKeyQuery { Prefix = DatabaseTenantStore.CacheRegion + ":" }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(5, page.Items.Count);
    }

    [Fact]
    public async Task Upsert_InvalidatesCatalogAndShards_AndRemoveReportsMissing()
    {
        await using var host = await TenantHosts.StartAsync();
        var store = host.Services.GetRequiredService<ITenantStore>();
        var shards = host.Services.GetRequiredService<ITenantShardResolver>();
        await TenantHosts.SeedAsync(host, [Acme(connectionString: "Host=a")]);
        Assert.Equal("Host=a", (await shards.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);

        // 同一批里后者整份覆盖前者，三项结果都成功
        var written = await host.InvokeAsync(async context =>
        {
            var records = context.Services.GetRequiredService<TenantRecordStore>();
            var results = await records.UpsertAsync([Acme("Acme 1", "Host=a1"), new TenantInfo { Id = "beta" }, Acme("Acme 2", "Host=a2")], context.CancellationToken);
            Assert.All(results, static r => Assert.True(r.IsSuccess, r.ToString()));
            Assert.Equal(3, results.Count);
        });
        Assert.True(written.IsSuccess, written.ToString());

        // 提交后经租户目录列出：按 ID 排序分页，同一 ID 在一批里写两次只留一行
        Assert.Equal(["acme", "beta"], (await store.ListAsync(new TenantQuery(), CancellationToken.None)).ValueOrThrow().Items.Select(static t => t.Id));
        Assert.Single((await store.ListAsync(new TenantQuery { PageSize = 1 }, CancellationToken.None)).ValueOrThrow().Items);

        // 写入即失效：目录与分片解析器都看到新值
        Assert.Equal("Acme 2", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);
        Assert.Equal("Host=a2", (await shards.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        Assert.True((await store.FindAsync("beta", CancellationToken.None)).ValueOrThrow().HasValue);

        var removed = await host.InvokeAsync(async context =>
        {
            var records = context.Services.GetRequiredService<TenantRecordStore>();
            var results = await records.RemoveAsync(["acme", "nobody", "acme"], context.CancellationToken);
            Assert.True(results[0].IsSuccess, results[0].ToString());
            Assert.Equal(MultiTenancyErrors.NotFound, results[1].Error?.Code);
            Assert.Equal(MultiTenancyErrors.NotFound, results[2].Error?.Code);
            Assert.Contains("nobody", context.Services.GetRequiredService<IMessageLocalizer>().Localize(results[1].Error!, "zh-CN"), StringComparison.Ordinal);
            // 不合法的 ID 是编程错误：整批在触库前抛出
            await Assert.ThrowsAsync<ArgumentException>(async () => await records.RemoveAsync(["beta", "bad id"], context.CancellationToken));
        });
        Assert.True(removed.IsSuccess, removed.ToString());
        Assert.False((await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.False((await shards.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.Equal(["beta"], (await store.ListAsync(new TenantQuery(), CancellationToken.None)).ValueOrThrow().Items.Select(static t => t.Id));
    }

    [Fact]
    public async Task List_PagesByTheContinuationToken_InsteadOfTruncating()
    {
        await using var host = await TenantHosts.StartAsync();
        await TenantHosts.SeedAsync(host, [new TenantInfo { Id = "c" }, new TenantInfo { Id = "a" }, new TenantInfo { Id = "b" }]);
        var store = host.Services.GetRequiredService<ITenantStore>();

        // 按 ID 排序逐页读：第一页满页给出令牌，读到不满一页时令牌为空，不再有截断
        var first = (await store.ListAsync(new TenantQuery { PageSize = 2 }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["a", "b"], first.Items.Select(static t => t.Id));
        Assert.NotNull(first.ContinuationToken);
        var second = (await store.ListAsync(new TenantQuery { PageSize = 2, ContinuationToken = first.ContinuationToken }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["c"], second.Items.Select(static t => t.Id));
        Assert.Null(second.ContinuationToken);
    }

    [Fact]
    public async Task Find_PropagatesCorruptProperties_InsteadOfReportingMissing()
    {
        await using var host = await TenantHosts.StartAsync();
        var store = host.Services.GetRequiredService<DatabaseTenantStore>();
        await TenantHosts.AddRowAsync(host, new TenantRecord { Id = "broken", PropertiesJson = "[1]" });

        // 属性列不是 JSON 对象：异常原样抛出、不缓存；Tenant 阶段据此让调用失败而不是当作不存在
        await Assert.ThrowsAsync<JsonException>(async () => await store.FindAsync("broken", CancellationToken.None));
        await Assert.ThrowsAsync<JsonException>(async () => await store.FindAsync("broken", CancellationToken.None));
        var outcome = await host.InvokeAsync(static _ => ValueTask.CompletedTask, baggage: Tenant("broken"));
        Assert.True(outcome.IsFailure);
        Assert.NotEqual(MultiTenancyErrors.NotFound, outcome.Error?.Code);
    }

    [Fact]
    public void Options_FromConfiguration_ParseAndValidate()
    {
        Assert.Equal(TimeSpan.FromSeconds(2.5), DatabaseMultiTenancyOptions.FromConfiguration(Build(("CacheTimeToLiveSeconds", "2.5"))).CacheTimeToLive);
        Assert.Equal(TimeSpan.FromSeconds(30), DatabaseMultiTenancyOptions.FromConfiguration(Build()).CacheTimeToLive);
        // 容量交给缓存 Provider：旧的 CacheSize 键不再读取
        Assert.Equal(new DatabaseMultiTenancyOptions(), DatabaseMultiTenancyOptions.FromConfiguration(Build(("CacheSize", "5"))));

        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseMultiTenancyOptions { CacheTimeToLive = TimeSpan.Zero });
        Assert.Throws<ArgumentException>(static () => DatabaseMultiTenancyOptions.FromConfiguration(Build(("CacheTimeToLiveSeconds", "0"))));
        Assert.Throws<ArgumentException>(static () => DatabaseMultiTenancyOptions.FromConfiguration(Build(("CacheTimeToLiveSeconds", "-1"))));
    }
}
