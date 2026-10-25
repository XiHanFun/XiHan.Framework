// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 租户分库：存储解析器的缓存与容量、存储故障的错误码、Tenant 阶段把分片写进调用上下文，以及分片绑定的合成。
/// </summary>
public sealed class TenantShardingTests
{
    /// <summary>
    /// 分库租户的配置键前缀。
    /// </summary>
    private const string Acme = "XiHan:MultiTenancy:Tenants:acme:";

    [Fact]
    public async Task Resolver_CachesPerTenant_AndIsBounded()
    {
        var store = new InMemoryTenantStore(new InMemoryMultiTenancyOptions
        {
            Tenants =
            [
                new TenantInfo { Id = "a", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = "Host=a" } },
                new TenantInfo { Id = "b" },
                new TenantInfo { Id = "c", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = "Host=c" } },
            ],
        });
        var resolver = new TenantStoreShardResolver(store, new MultiTenancyOptions { ShardCacheSize = 2 });

        Assert.Equal("Host=a", (await resolver.ResolveAsync("a", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        Assert.False((await resolver.ResolveAsync("b", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.Equal(2, resolver.CachedCount);

        // 容量到顶整体清空后再装入新租户
        Assert.Equal("Host=c", (await resolver.ResolveAsync("c", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        Assert.Equal(1, resolver.CachedCount);

        // 缓存命中：存储里的变更要经失效才可见
        Assert.True(store.Set(new TenantInfo { Id = "c", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = "Host=c2" } }).IsSuccess);
        Assert.Equal("Host=c", (await resolver.ResolveAsync("c", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        resolver.Invalidate("c");
        Assert.Equal("Host=c2", (await resolver.ResolveAsync("c", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        resolver.Invalidate();
        Assert.Equal(0, resolver.CachedCount);

        Assert.Throws<ArgumentOutOfRangeException>(static () => new MultiTenancyOptions { ShardCacheSize = 0 });
    }

    [Fact]
    public async Task Resolver_ReturnsUnavailable_WhenStoreThrows()
    {
        var resolver = new TenantStoreShardResolver(new ThrowingStore(), new MultiTenancyOptions());

        var result = await resolver.ResolveAsync("acme", CancellationToken.None);

        Assert.Equal(MultiTenancyErrors.ShardUnavailable, result.Error?.Code);
        Assert.Equal(0, resolver.CachedCount);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await resolver.ResolveAsync("acme", new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Feature_PutsShardIntoInvocation_ForShardedTenantOnly()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMultiTenancyFeature>(),
            [
                new(Acme + "DisplayName", "Acme"),
                new(Acme + "Properties:" + TenantShardKeys.ConnectionString, "Host=acme"),
                new(Acme + "Properties:" + TenantShardKeys.DatabaseProvider, "PostgreSQL"),
                new(Acme + "Properties:" + TenantShardKeys.PropertyPrefix + "schema", "acme"),
                new("XiHan:MultiTenancy:Tenants:beta:DisplayName", "Beta"),
            ]);
        Assert.IsType<TenantStoreShardResolver>(host.Services.GetRequiredService<ITenantShardResolver>());

        Maybe<TenantShard> acme = default, beta = default, none = default;
        Maybe<BindingValue> acmeBinding = default, betaBinding = default;
        var fallback = Maybe.Some(new BindingValue("db", "connection:relational", "Host=default", new Dictionary<string, string>(StringComparer.Ordinal) { ["pool"] = "10" }));
        (await host.InvokeAsync(ctx =>
        {
            acme = ctx.GetTenantShard();
            acmeBinding = TenantShardBinding.Resolve(ctx.Services, "db", "connection:relational", fallback);
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" })).ThrowIfFailure();
        (await host.InvokeAsync(ctx =>
        {
            beta = ctx.GetTenantShard();
            betaBinding = TenantShardBinding.Resolve(ctx.Services, "db", "connection:relational", fallback);
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "beta" })).ThrowIfFailure();
        (await host.InvokeAsync(ctx =>
        {
            none = ctx.GetTenantShard();
            return ValueTask.CompletedTask;
        })).ThrowIfFailure();

        var shard = acme.ValueOrThrow();
        Assert.Equal("Host=acme", shard.ConnectionString);
        Assert.Equal("postgresql", shard.Provider);
        Assert.False(beta.HasValue);
        Assert.False(none.HasValue);

        // 分片绑定：主值换成分片连接串，原绑定属性保留，再并入分片属性与租户、提供者
        var binding = acmeBinding.ValueOrThrow();
        Assert.Equal("db", binding.Name);
        Assert.Equal("connection:relational", binding.Type);
        Assert.Equal("Host=acme", binding.Value);
        Assert.Equal("10", binding.Properties["pool"]);
        Assert.Equal("acme", binding.Properties["schema"]);
        Assert.Equal("acme", binding.Properties[TenantShardKeys.TenantProperty]);
        Assert.Equal("postgresql", binding.Properties[TenantShardKeys.ProviderProperty]);
        Assert.False(betaBinding.HasValue);

        // 调用之外的作用域没有当前调用，也没有分片绑定
        using var scope = host.Services.CreateScope();
        Assert.False(TenantShardBinding.Resolve(scope.ServiceProvider, "db", "connection:relational", fallback).HasValue);
    }

    [Fact]
    public async Task Feature_FailsInvocation_WhenShardResolutionFails()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMultiTenancyFeature>(),
            [new(Acme + "DisplayName", "Acme")],
            static services => services.AddSingleton<ITenantShardResolver, FailingResolver>());

        var reached = false;
        var result = await host.InvokeAsync(_ =>
        {
            reached = true;
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" });

        Assert.Equal(MultiTenancyErrors.ShardUnavailable, result.Error?.Code);
        Assert.False(reached);
    }

    /// <summary>
    /// 查找即抛异常的租户存储，模拟存储不可达。
    /// </summary>
    private sealed class ThrowingStore : ITenantStore
    {
        /// <inheritdoc />
        public ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("store offline");
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("store offline");
    }

    /// <summary>
    /// 总是返回不可用的分片解析器，模拟应用自己的分片 Provider 故障。
    /// </summary>
    private sealed class FailingResolver : ITenantShardResolver
    {
        /// <inheritdoc />
        public ValueTask<Result<Maybe<TenantShard>>> ResolveAsync(string tenantId, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<Maybe<TenantShard>>>(Error.Unavailable(MultiTenancyErrors.ShardUnavailable, MultiTenancyMessages.ShardUnavailable, MessageArguments.Of(("tenantId", tenantId), ("reason", "offline"))));

        /// <inheritdoc />
        public void Invalidate(string? tenantId = null)
        {
        }
    }
}
