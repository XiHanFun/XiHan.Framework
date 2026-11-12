// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 分片解析缓存的存活时间（ADR-0193）
/// </summary>
/// <remarks>
/// 另一个实例改了租户的连接串，本实例收不到失效通知：缓存过了存活时间就重新问租户存储，不会一直往旧库写。解析器经容器构造，时钟取容器里的 <see cref="TimeProvider"/>
/// </remarks>
public sealed class ShardCacheExpiryTests
{
    [Fact]
    public async Task ChangedConnectionString_IsPickedUpAfterTheTimeToLive_WithoutInvalidation()
    {
        var clock = new FakeTimeProvider();
        var store = new MutableStore("Data Source=old.db");
        await using var provider = Resolver(clock, store, new MultiTenancyOptions());
        var resolver = provider.GetRequiredService<ITenantShardResolver>();
        Assert.Equal("Data Source=old.db", (await resolver.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);

        // 别的实例把租户迁到新库：本实例没有收到失效通知
        store.ConnectionString = "Data Source=new.db";
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal("Data Source=old.db", (await resolver.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        clock.Advance(TimeSpan.FromSeconds(2));

        // 修之前缓存从不过期，一直解析出旧库
        Assert.Equal("Data Source=new.db", (await resolver.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
    }

    [Fact]
    public async Task ZeroTimeToLive_KeepsEntriesUntilInvalidated()
    {
        var clock = new FakeTimeProvider();
        var store = new MutableStore("Data Source=old.db");
        await using var provider = Resolver(clock, store, new MultiTenancyOptions { ShardCacheTimeToLive = null });
        var resolver = provider.GetRequiredService<ITenantShardResolver>();
        (await resolver.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow();

        store.ConnectionString = "Data Source=new.db";
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal("Data Source=old.db", (await resolver.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);

        resolver.Invalidate(["acme"]);
        Assert.Equal("Data Source=new.db", (await resolver.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
    }

    /// <summary>
    /// 经容器构造解析器
    /// </summary>
    private static ServiceProvider Resolver(TimeProvider clock, ITenantStore store, MultiTenancyOptions options)
        => new ServiceCollection()
            .AddSingleton(clock)
            .AddSingleton(store)
            .AddSingleton(options)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton<ITenantShardResolver, TenantStoreShardResolver>()
            .BuildServiceProvider();

    /// <summary>
    /// 一个租户、连接串可改的存储
    /// </summary>
    private sealed class MutableStore(string connectionString) : ITenantStore
    {
        /// <summary>
        /// 租户 acme 的连接串
        /// </summary>
        public string ConnectionString { get; set; } = connectionString;

        /// <inheritdoc />
        public ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken)
            => ValueTask.FromResult(Result.Success(tenantId == "acme"
                ? Maybe.Some(new TenantInfo { Id = "acme", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = ConnectionString } })
                : Maybe.None<TenantInfo>()));

        /// <inheritdoc />
        public ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
