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
/// 管道外的作用域绑定分片。
/// </summary>
/// <remarks>
/// 按租户解析并绑定（停用租户也行、共库租户不绑定、解析失败不绑定），客户端建好后不能再绑定或改绑，显式绑定优先于调用上下文（ADR-0057）。
/// </remarks>
public sealed class TenantShardScopeTests
{
    /// <summary>
    /// 租户 <c>acme</c> 的配置节前缀。
    /// </summary>
    private const string Acme = "XiHan:MultiTenancy:Tenants:acme:";

    /// <summary>
    /// 原绑定。
    /// </summary>
    private static readonly Maybe<BindingValue> _fallback = Maybe.Some(new BindingValue("db", "connection:relational", "Host=default", new Dictionary<string, string>(StringComparer.Ordinal)));

    [Fact]
    public async Task BindTenant_OutsideThePipeline_BindsEvenADisabledTenant()
    {
        await using var host = await StartAsync();
        using var scope = host.Services.CreateScope();
        var shards = scope.ServiceProvider.GetRequiredService<TenantShardScope>();

        // 停用的租户在管道里会被拒绝，后台写入仍要能落到它的库
        var bound = (await shards.BindTenantAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow();
        Assert.Equal("Host=acme", bound.ConnectionString);
        Assert.Same(bound, shards.Shard.ValueOrThrow());
        Assert.False(shards.IsConnected);

        var binding = TenantShardBinding.Resolve(scope.ServiceProvider, "db", "connection:relational", _fallback).ValueOrThrow();
        Assert.Equal("Host=acme", binding.Value);
        Assert.Equal("acme", binding.Properties[TenantShardKeys.TenantProperty]);
        Assert.True(shards.IsConnected);
    }

    [Fact]
    public async Task BindTenant_SharedOrUnknownTenant_SucceedsWithoutBinding()
    {
        await using var host = await StartAsync();
        using var scope = host.Services.CreateScope();
        var shards = scope.ServiceProvider.GetRequiredService<TenantShardScope>();

        Assert.False((await shards.BindTenantAsync("beta", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.False((await shards.BindTenantAsync("missing", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.False(shards.Shard.HasValue);
        Assert.False(TenantShardBinding.Resolve(scope.ServiceProvider, "db", "connection:relational", _fallback).HasValue);
    }

    [Fact]
    public async Task Bind_AfterTheClientWasCreated_OrToAnotherShard_Throws()
    {
        var scope = new TenantShardScope();
        var acme = new TenantShard("acme", "Host=acme");
        scope.Bind(acme);
        // 同一个分片重复绑定无副作用；改绑另一个租户是错误
        scope.Bind(new TenantShard("acme", "Host=acme"));
        Assert.Throws<InvalidOperationException>(() => scope.Bind(new TenantShard("beta", "Host=beta")));

        await using var host = await StartAsync();
        using var services = host.Services.CreateScope();
        var late = services.ServiceProvider.GetRequiredService<TenantShardScope>();
        _ = TenantShardBinding.Resolve(services.ServiceProvider, "db", "connection:relational", _fallback);
        Assert.Throws<InvalidOperationException>(() => late.Bind(acme));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await late.BindTenantAsync("acme", CancellationToken.None));
        Assert.False(late.Shard.HasValue);
    }

    [Fact]
    public async Task BindTenant_WithoutResolver_OrWhenResolutionFails_DoesNotBind()
    {
        // 没有装配分片能力：每个租户都用默认连接
        var plain = new TenantShardScope();
        Assert.False((await plain.BindTenantAsync("acme", CancellationToken.None)).ValueOrThrow().HasValue);
        Assert.Throws<ArgumentException>(() => plain.BindTenantAsync(" ", CancellationToken.None).AsTask().GetAwaiter().GetResult());

        var failing = new TenantShardScope(new TenantStoreShardResolver(new UnreachableStore(), new MultiTenancyOptions()));
        var failed = await failing.BindTenantAsync("acme", CancellationToken.None);
        Assert.Equal(MultiTenancyErrors.ShardUnavailable, failed.Error?.Code);
        Assert.False(failing.Shard.HasValue);
    }

    [Fact]
    public async Task ExplicitBinding_TakesPrecedenceOverTheInvocationShard()
    {
        await using var host = await StartAsync(("XiHan:MultiTenancy:Tenants:gamma:Properties:" + TenantShardKeys.ConnectionString, "Host=gamma"));
        string? chosen = null;
        (await host.InvokeAsync(ctx =>
        {
            ctx.Services.GetRequiredService<TenantShardScope>().Bind(new TenantShard("acme", "Host=acme"));
            chosen = TenantShardBinding.Resolve(ctx.Services, "db", "connection:relational", _fallback).ValueOrThrow().Value;
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "gamma" })).ThrowIfFailure();
        Assert.Equal("Host=acme", chosen);
    }

    /// <summary>
    /// 停用的分库租户 <c>acme</c> 与共库租户 <c>beta</c>。
    /// </summary>
    private static Task<TestHost> StartAsync(params (string Key, string Value)[] extra) => TestHost.StartAsync(
        static b => b.UseFeature<InMemoryMultiTenancyFeature>(),
        [
            new(Acme + "IsEnabled", "false"),
            new(Acme + "Properties:" + TenantShardKeys.ConnectionString, "Host=acme"),
            new("XiHan:MultiTenancy:Tenants:beta:DisplayName", "Beta"),
            .. extra.Select(static e => new KeyValuePair<string, string?>(e.Key, e.Value)),
        ]);

    /// <summary>
    /// 读取即抛出的租户存储。
    /// </summary>
    private sealed class UnreachableStore : ITenantStore
    {
        /// <inheritdoc />
        public ValueTask<Maybe<TenantInfo>> FindAsync(string tenantId, CancellationToken cancellationToken) => throw new TimeoutException("store unreachable");

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken cancellationToken) => throw new TimeoutException("store unreachable");
    }
}
