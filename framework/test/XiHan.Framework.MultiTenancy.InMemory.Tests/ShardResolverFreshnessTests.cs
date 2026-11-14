// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 分片解析器不另行缓存（ADR-0229，取代 ADR-0193 的存活时间）
/// </summary>
/// <remarks>
/// 缓存与失效只在租户存储一处：存储里的连接串一变，下一次解析就用新库，不需要失效通知，也不等存活时间。解析器经容器构造
/// </remarks>
public sealed class ShardResolverFreshnessTests
{
    [Fact]
    public async Task ChangedConnectionString_IsUsedByTheNextResolution()
    {
        var store = new MutableStore("Data Source=old.db");
        await using var provider = Resolver(store);
        var resolver = provider.GetRequiredService<ITenantShardResolver>();
        Assert.Equal("Data Source=old.db", (await resolver.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);

        // 别的实例把租户迁到新库：本实例没有收到任何失效通知，下一次解析照样用上新库
        store.ConnectionString = "Data Source=new.db";
        Assert.Equal("Data Source=new.db", (await resolver.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);

        // 失效什么也不做，但仍校验入参
        resolver.Invalidate(["acme"]);
        resolver.InvalidateAll();
        Assert.Throws<ArgumentNullException>(() => resolver.Invalidate(null!));
        Assert.Throws<ArgumentNullException>(() => resolver.Invalidate([null!]));
        Assert.Equal(2, store.Reads);
    }

    /// <summary>
    /// 经容器构造解析器
    /// </summary>
    private static ServiceProvider Resolver(ITenantStore store)
        => new ServiceCollection()
            .AddSingleton(store)
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

        /// <summary>
        /// 被查询的次数
        /// </summary>
        public int Reads { get; private set; }

        /// <inheritdoc />
        public ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken)
        {
            Reads++;
            return ValueTask.FromResult(Result.Success(tenantId == "acme"
                ? Maybe.Some(new TenantInfo { Id = "acme", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = ConnectionString } })
                : Maybe.None<TenantInfo>()));
        }

        /// <inheritdoc />
        public ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
