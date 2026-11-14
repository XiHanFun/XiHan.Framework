// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 租户分库
/// </summary>
/// <remarks>
/// 存储解析器的缓存与容量、存储故障的错误码、Tenant 阶段把分片写进调用上下文，以及分片绑定的合成
/// </remarks>
public sealed class TenantShardingTests
{
    /// <summary>
    /// 分库租户的配置键前缀
    /// </summary>
    private const string Acme = "XiHan:MultiTenancy:Tenants:acme:";

    [Fact]
    public async Task Resolver_ReadsTheStoreEachTime_SoAdministrationIsVisibleAtOnce()
    {
        var store = new InMemoryTenantStore(new InMemoryMultiTenancyOptions
        {
            Tenants =
            [
                new TenantInfo { Id = "a", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = "Host=a" } },
                new TenantInfo { Id = "b" },
            ],
        });
        var resolver = new TenantStoreShardResolver(store, NullLoggerFactory.Instance);

        Assert.Equal("Host=a", (await resolver.ResolveAsync("a", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        Assert.False((await resolver.ResolveAsync("b", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.False((await resolver.ResolveAsync("nobody", CancellationToken.None)).ValueOrThrow().HasValue);

        // 解析器自己不缓存（ADR-0229）：存储里的变更不经失效也立即可见
        Assert.True((await new InMemoryTenantAdministration(store, shards: null).UpsertAsync([new TenantInfo { Id = "a", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = "Host=a2" } }], CancellationToken.None))[0].IsSuccess);
        Assert.Equal("Host=a2", (await resolver.ResolveAsync("a", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
    }

    [Fact]
    public async Task Resolver_ReturnsUnavailable_WhenTheStoreIsUnavailable()
    {
        var logs = new LogCapture();
        var resolver = new TenantStoreShardResolver(new ThrowingStore(), logs);

        var result = await resolver.ResolveAsync("acme", CancellationToken.None);

        Assert.Equal(MultiTenancyErrors.ShardUnavailable, result.Error?.Code);
        // 这个错误会返回给调用方：reason 只写存储的错误码，存储的错误作为内部错误保留，失败不进缓存
        Assert.Equal(MultiTenancyErrors.StoreUnavailable, result.Error?.Details?["reason"]);
        Assert.Equal(MultiTenancyErrors.StoreUnavailable, result.Error?.Inner?.Code);
        var logged = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, logged.Level);
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
            acme = ctx.FindTenantShard();
            acmeBinding = TenantShardBinding.Resolve(ctx.Services, "db", "connection:relational", fallback);
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" })).ThrowIfFailure();
        (await host.InvokeAsync(ctx =>
        {
            beta = ctx.FindTenantShard();
            betaBinding = TenantShardBinding.Resolve(ctx.Services, "db", "connection:relational", fallback);
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "beta" })).ThrowIfFailure();
        (await host.InvokeAsync(ctx =>
        {
            none = ctx.FindTenantShard();
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
    /// 查找总是返回存储不可达的租户存储
    /// </summary>
    private sealed class ThrowingStore : ITenantStore
    {
        /// <inheritdoc />
        public ValueTask<Result<Maybe<TenantInfo>>> FindAsync(string tenantId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result.Failure<Maybe<TenantInfo>>(Error.Unavailable(MultiTenancyErrors.StoreUnavailable, MultiTenancyMessages.StoreUnavailable)));
        }

        /// <inheritdoc />
        public ValueTask<Result<TenantPage>> ListAsync(TenantQuery query, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Failure<TenantPage>(Error.Unavailable(MultiTenancyErrors.StoreUnavailable, MultiTenancyMessages.StoreUnavailable)));
    }

    /// <summary>
    /// 总是返回不可用的分片解析器，模拟应用自己的分片 Provider 故障
    /// </summary>
    private sealed class FailingResolver : ITenantShardResolver
    {
        /// <inheritdoc />
        public ValueTask<Result<Maybe<TenantShard>>> ResolveAsync(string tenantId, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<Maybe<TenantShard>>>(Error.Unavailable(MultiTenancyErrors.ShardUnavailable, MultiTenancyMessages.ShardUnavailable, MessageArguments.Of(("tenantId", tenantId), ("reason", "offline"))));

        /// <inheritdoc />
        public void Invalidate(IReadOnlyList<string> tenantIds)
        {
        }

        /// <inheritdoc />
        public void InvalidateAll()
        {
        }
    }
}
