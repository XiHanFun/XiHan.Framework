// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.MultiTenancy.Conformance;

/// <summary>
/// <see cref="ITenantShardResolver"/> 的一致性套件。继承并实现 <see cref="CreateResolverAsync"/>，全部用例随之执行。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class TenantShardResolverConformance
{
    /// <summary>
    /// 套件租户：分库，带提供者名与透传属性。
    /// </summary>
    public static TenantInfo Sharded { get; } = new()
    {
        Id = "acme",
        DisplayName = "Acme Corp",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TenantShardKeys.ConnectionString] = "Host=acme;Database=acme",
            [TenantShardKeys.DatabaseProvider] = "PostgreSQL",
            [TenantShardKeys.PropertyPrefix + "schema"] = "acme",
            ["Tier"] = "gold",
        },
    };

    /// <summary>
    /// 套件租户：共库，没有分片属性。
    /// </summary>
    public static TenantInfo Shared { get; } = new() { Id = "beta", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["Tier"] = "silver" } };

    /// <summary>
    /// 用给定租户创建被测解析器。每个用例调用一次。
    /// </summary>
    protected abstract ValueTask<ITenantShardResolver> CreateResolverAsync(IReadOnlyList<TenantInfo> tenants);

    /// <summary>
    /// 用例 1：分库租户解析出分片：连接串、小写的提供者名与去掉前缀的透传属性；其他租户属性不进分片。
    /// </summary>
    [Fact]
    public async Task Resolve_ReturnsShard_ForShardedTenant()
    {
        var resolver = await CreateResolverAsync([Sharded, Shared]).ConfigureAwait(false);

        var shard = (await resolver.ResolveAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
        Assert.Equal("acme", shard.TenantId);
        Assert.Equal("Host=acme;Database=acme", shard.ConnectionString);
        Assert.Equal("postgresql", shard.Provider);
        Assert.Equal("acme", shard.Properties["schema"]);
        Assert.False(shard.Properties.ContainsKey("Tier"));
        Assert.False(shard.Properties.ContainsKey(TenantShardKeys.ConnectionString));
    }

    /// <summary>
    /// 用例 2：共库租户与不存在的租户都解析为无分片（成功、无值），不是失败。
    /// </summary>
    [Fact]
    public async Task Resolve_ReturnsNone_ForSharedAndUnknownTenants()
    {
        var resolver = await CreateResolverAsync([Sharded, Shared]).ConfigureAwait(false);

        var shared = await resolver.ResolveAsync("beta", CancellationToken.None).ConfigureAwait(false);
        var unknown = await resolver.ResolveAsync("nobody", CancellationToken.None).ConfigureAwait(false);

        Assert.True(shared.IsSuccess);
        Assert.False(shared.ValueOrThrow().HasValue);
        Assert.True(unknown.IsSuccess);
        Assert.False(unknown.ValueOrThrow().HasValue);
    }

    /// <summary>
    /// 用例 3：重复解析结果一致；失效单个或全部租户后再解析仍得到同样的分片。
    /// </summary>
    [Fact]
    public async Task Resolve_IsStable_AcrossInvalidate()
    {
        var resolver = await CreateResolverAsync([Sharded, Shared]).ConfigureAwait(false);

        var first = (await resolver.ResolveAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
        resolver.Invalidate("acme");
        var second = (await resolver.ResolveAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();
        resolver.Invalidate();
        var third = (await resolver.ResolveAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow();

        Assert.Equal(first.ConnectionString, second.ConnectionString);
        Assert.Equal(first.ConnectionString, third.ConnectionString);
        Assert.Equal(first.Provider, third.Provider);
    }
}
