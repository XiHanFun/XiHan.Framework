// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
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
/// 数据库 Provider 特有的行为。
/// </summary>
/// <remarks>
/// 显式 Provider 顶替内存实现、Tenant 阶段解析数据库里的租户、目录缓存的命中 / 失效 / 存活时间 / 容量、批量写入与删除及其对分片解析器的失效、枚举上限、基础设施异常原样抛出、选项解析。
/// </remarks>
public sealed class DatabaseMultiTenancyTests
{
    /// <summary>
    /// 配置节前缀。
    /// </summary>
    private const string Section = DatabaseMultiTenancyOptions.ConfigurationSection + ":";

    /// <summary>
    /// 带 Baggage 租户的调用。
    /// </summary>
    private static Dictionary<string, string> Tenant(string tenantId) => new() { [BaggageKeys.Tenant] = tenantId };

    /// <summary>
    /// 分库或共库的 acme 租户。
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
    /// 用内存键值对构建配置。
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
            .UseFeature<DatabaseMultiTenancyFeature>());

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
            await TestHost.StartAsync(static b => b.UseFeature<InMemoryLocalizationFeature>().UseFeature<DatabaseMultiTenancyFeature>()));

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

        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        Assert.False((await store.FindAsync("ghost", CancellationToken.None)).HasValue);
        Assert.Equal(2, store.CachedCount);

        // 绕过 TenantRecordStore 直接改表：命中缓存看不到变化，“不存在”同样被缓存
        await TenantHosts.EditRowAsync(host, "acme", static row => row.DisplayName = "Acme 2");
        await TenantHosts.AddRowAsync(host, new TenantRecord { Id = "ghost", DisplayName = "Ghost" });
        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        Assert.False((await store.FindAsync("ghost", CancellationToken.None)).HasValue);

        // 单个失效只重查该租户；整体失效后两者都重查
        store.Invalidate("acme");
        Assert.Equal(1, store.CachedCount);
        Assert.Equal("Acme 2", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        Assert.False((await store.FindAsync("ghost", CancellationToken.None)).HasValue);
        store.Invalidate();
        Assert.Equal(0, store.CachedCount);
        Assert.Equal("Ghost", (await store.FindAsync("ghost", CancellationToken.None)).ValueOrThrow().DisplayName);

        // 不合法的 ID 不查库也不占缓存；枚举不走缓存
        Assert.False((await store.FindAsync("bad id", CancellationToken.None)).HasValue);
        Assert.Equal(1, store.CachedCount);
        Assert.Equal(["acme", "ghost"], (await store.ListAsync(CancellationToken.None)).Select(static t => t.Id));
    }

    [Fact]
    public async Task Find_ExpiresCachedEntries_ByTimeToLive()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 8, 0, 0, TimeSpan.Zero));
        await using var host = await TenantHosts.StartAsync(services: services => services.AddSingleton<TimeProvider>(clock));
        var store = host.Services.GetRequiredService<DatabaseTenantStore>();
        await TenantHosts.SeedAsync(host, [Acme()]);
        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        await TenantHosts.EditRowAsync(host, "acme", static row => row.DisplayName = "Acme 2");

        // 默认 30 秒：29 秒后仍命中，到期后重查
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("Acme 2", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);

        // 0 表示不过期：只靠失效
        var frozen = new FakeTimeProvider(clock.GetUtcNow());
        await using var forever = await TenantHosts.StartAsync([new(Section + "CacheTimeToLiveSeconds", "0")], services => services.AddSingleton<TimeProvider>(frozen));
        var foreverStore = forever.Services.GetRequiredService<DatabaseTenantStore>();
        await TenantHosts.SeedAsync(forever, [Acme()]);
        Assert.Equal("Acme", (await foreverStore.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        await TenantHosts.EditRowAsync(forever, "acme", static row => row.DisplayName = "Acme 2");
        frozen.Advance(TimeSpan.FromDays(365));
        Assert.Equal("Acme", (await foreverStore.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        foreverStore.Invalidate("acme");
        Assert.Equal("Acme 2", (await foreverStore.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
    }

    [Fact]
    public async Task Find_ClearsCache_WhenCapacityIsReached()
    {
        await using var host = await TenantHosts.StartAsync([new(Section + "CacheSize", "2")]);
        var store = host.Services.GetRequiredService<DatabaseTenantStore>();
        await TenantHosts.SeedAsync(host, [new TenantInfo { Id = "a" }, new TenantInfo { Id = "b" }, new TenantInfo { Id = "c" }]);

        Assert.True((await store.FindAsync("a", CancellationToken.None)).HasValue);
        Assert.True((await store.FindAsync("b", CancellationToken.None)).HasValue);
        Assert.Equal(2, store.CachedCount);

        // 容量到顶整体清空后再装入新租户
        Assert.True((await store.FindAsync("c", CancellationToken.None)).HasValue);
        Assert.Equal(1, store.CachedCount);
    }

    [Fact]
    public async Task Upsert_InvalidatesCatalogAndShards_AndRemoveReportsMissing()
    {
        await using var host = await TenantHosts.StartAsync();
        var store = host.Services.GetRequiredService<ITenantStore>();
        var shards = host.Services.GetRequiredService<ITenantShardResolver>();
        await TenantHosts.SeedAsync(host, [Acme(connectionString: "Host=a")]);
        Assert.Equal("Host=a", (await shards.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        Assert.Equal("Acme", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);

        // 同一批里后者整份覆盖前者，三项结果都成功；分页列表不走缓存
        var written = await host.InvokeAsync(async context =>
        {
            var records = context.Services.GetRequiredService<TenantRecordStore>();
            var results = await records.UpsertAsync([Acme("Acme 1", "Host=a1"), new TenantInfo { Id = "beta" }, Acme("Acme 2", "Host=a2")], context.CancellationToken);
            Assert.All(results, static r => Assert.True(r.Outcome.IsSuccess, r.Outcome.ToString()));
            Assert.Equal(["acme", "beta", "acme"], results.Select(static r => r.TenantId));
            Assert.Equal(["acme", "beta"], (await records.ListAsync(0, 10, context.CancellationToken)).Select(static t => t.Id));
            Assert.Single(await records.ListAsync(1, 10, context.CancellationToken));
        });
        Assert.True(written.IsSuccess, written.ToString());

        // 写入即失效：目录与分片解析器都看到新值
        Assert.Equal("Acme 2", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        Assert.Equal("Host=a2", (await shards.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        Assert.True((await store.FindAsync("beta", CancellationToken.None)).HasValue);

        var removed = await host.InvokeAsync(async context =>
        {
            var records = context.Services.GetRequiredService<TenantRecordStore>();
            var results = await records.RemoveAsync(["acme", "nobody", "acme"], context.CancellationToken);
            Assert.True(results[0].Outcome.IsSuccess, results[0].Outcome.ToString());
            Assert.Equal(MultiTenancyErrors.NotFound, results[1].Outcome.Error?.Code);
            Assert.Equal(MultiTenancyErrors.NotFound, results[2].Outcome.Error?.Code);
            Assert.Contains("nobody", context.Services.GetRequiredService<IMessageLocalizer>().Localize(results[1].Outcome.Error!, "zh-CN"), StringComparison.Ordinal);
            // 不合法的 ID 是编程错误：整批在触库前抛出
            await Assert.ThrowsAsync<ArgumentException>(async () => await records.RemoveAsync(["beta", "bad id"], context.CancellationToken));
        });
        Assert.True(removed.IsSuccess, removed.ToString());
        Assert.False((await store.FindAsync("acme", CancellationToken.None)).HasValue);
        Assert.False((await shards.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.Equal(["beta"], (await store.ListAsync(CancellationToken.None)).Select(static t => t.Id));
    }

    [Fact]
    public async Task List_IsBoundedByMaxListedTenants()
    {
        await using var host = await TenantHosts.StartAsync([new(Section + "MaxListedTenants", "2")]);
        await TenantHosts.SeedAsync(host, [new TenantInfo { Id = "c" }, new TenantInfo { Id = "a" }, new TenantInfo { Id = "b" }]);

        // 按 ID 排序只返回前两个
        Assert.Equal(["a", "b"], (await host.Services.GetRequiredService<ITenantStore>().ListAsync(CancellationToken.None)).Select(static t => t.Id));
    }

    [Fact]
    public async Task Find_PropagatesCorruptProperties_InsteadOfReportingMissing()
    {
        await using var host = await TenantHosts.StartAsync();
        var store = host.Services.GetRequiredService<DatabaseTenantStore>();
        await TenantHosts.AddRowAsync(host, new TenantRecord { Id = "broken", PropertiesJson = "[1]" });

        // 属性列不是 JSON 对象：异常原样抛出、不缓存；Tenant 阶段据此让调用失败而不是当作不存在
        await Assert.ThrowsAsync<JsonException>(async () => await store.FindAsync("broken", CancellationToken.None));
        Assert.Equal(0, store.CachedCount);
        var outcome = await host.InvokeAsync(static _ => ValueTask.CompletedTask, baggage: Tenant("broken"));
        Assert.True(outcome.IsFailure);
        Assert.NotEqual(MultiTenancyErrors.NotFound, outcome.Error?.Code);
    }

    [Fact]
    public void Options_FromConfiguration_ParseAndValidate()
    {
        var options = DatabaseMultiTenancyOptions.FromConfiguration(Build(("CacheSize", "5"), ("CacheTimeToLiveSeconds", "0"), ("MaxListedTenants", "7")));
        Assert.Equal((5, (TimeSpan?)null, 7), (options.CacheSize, options.CacheTimeToLive, options.MaxListedTenants));
        Assert.Equal(TimeSpan.FromSeconds(2.5), DatabaseMultiTenancyOptions.FromConfiguration(Build(("CacheTimeToLiveSeconds", "2.5"))).CacheTimeToLive);

        var defaults = DatabaseMultiTenancyOptions.FromConfiguration(Build());
        Assert.Equal((10_000, TimeSpan.FromSeconds(30), 10_000), (defaults.CacheSize, defaults.CacheTimeToLive, defaults.MaxListedTenants));

        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseMultiTenancyOptions { CacheSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseMultiTenancyOptions { CacheTimeToLive = TimeSpan.Zero });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseMultiTenancyOptions { MaxListedTenants = 0 });
        Assert.Throws<ArgumentException>(static () => DatabaseMultiTenancyOptions.FromConfiguration(Build(("CacheSize", "-1"))));
        Assert.Throws<ArgumentException>(static () => DatabaseMultiTenancyOptions.FromConfiguration(Build(("CacheTimeToLiveSeconds", "-1"))));
        Assert.Throws<ArgumentException>(static () => DatabaseMultiTenancyOptions.FromConfiguration(Build(("MaxListedTenants", "many"))));
    }
}
