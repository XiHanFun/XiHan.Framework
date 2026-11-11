// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Resolution;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 内存多租户包的测试
/// </summary>
/// <remarks>
/// 从配置装载租户、存储的容量与增删，以及能力包在宿主里的租户解析
/// </remarks>
public sealed class InMemoryMultiTenancyTests
{
    /// <summary>
    /// 用内存键值对构建配置
    /// </summary>
    private static IConfiguration Build(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void Options_FromConfiguration_LoadsTenantsWithPropertiesAndFlags()
    {
        var options = InMemoryMultiTenancyOptions.FromConfiguration(Build(
            ("XiHan:MultiTenancy:InMemory:MaxTenants", "3"),
            ("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme"),
            ("XiHan:MultiTenancy:Tenants:acme:Properties:db", "Host=acme"),
            ("XiHan:MultiTenancy:Tenants:beta:IsEnabled", "false")));

        Assert.Equal(3, options.MaxTenants);
        Assert.Equal(2, options.Tenants.Count);
        var acme = options.Tenants.Single(static t => t.Id == "acme");
        Assert.Equal("Acme", acme.DisplayName);
        Assert.True(acme.IsEnabled);
        Assert.Equal("Host=acme", acme.Properties["db"]);
        Assert.False(options.Tenants.Single(static t => t.Id == "beta").IsEnabled);

        Assert.Equal(1_000, InMemoryMultiTenancyOptions.FromConfiguration(Build()).MaxTenants);
        Assert.Throws<ArgumentException>(() => InMemoryMultiTenancyOptions.FromConfiguration(Build(("XiHan:MultiTenancy:Tenants:acme:IsEnabled", "maybe"))));
        Assert.Throws<ArgumentException>(() => InMemoryMultiTenancyOptions.FromConfiguration(Build(("XiHan:MultiTenancy:Tenants:bad id:DisplayName", "x"))));
        Assert.Throws<ArgumentException>(() => InMemoryMultiTenancyOptions.FromConfiguration(Build(("XiHan:MultiTenancy:InMemory:MaxTenants", "0"))));
    }

    [Fact]
    public async Task Store_IsBounded_AndSupportsSetAndRemove()
    {
        var store = new InMemoryTenantStore(new InMemoryMultiTenancyOptions { MaxTenants = 2, Tenants = [new TenantInfo { Id = "a" }] });

        Assert.True(store.Set(new TenantInfo { Id = "b" }).IsSuccess);
        var full = store.Set(new TenantInfo { Id = "c" });
        Assert.Equal(MultiTenancyErrors.CapacityExceeded, full.Error?.Code);
        Assert.True(store.Set(new TenantInfo { Id = "a", DisplayName = "replaced" }).IsSuccess);
        Assert.Equal("replaced", (await store.FindAsync("a", CancellationToken.None)).ValueOrThrow().ValueOrThrow().DisplayName);
        Assert.Equal(2, store.Count);

        Assert.True(store.Remove("a"));
        Assert.False(store.Remove("a"));
        Assert.True(store.Set(new TenantInfo { Id = "c" }).IsSuccess);
        Assert.Throws<FrameworkException>(static () => new InMemoryTenantStore(new InMemoryMultiTenancyOptions { MaxTenants = 1, Tenants = [new TenantInfo { Id = "a" }, new TenantInfo { Id = "b" }] }));
    }

    [Fact]
    public async Task Feature_ResolvesTenantFromBaggage_IntoSecurityContext()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMultiTenancyFeature>(),
            [
                new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme"),
                new("XiHan:MultiTenancy:Tenants:beta:IsEnabled", "false"),
            ]);
        Assert.Contains(nameof(TenantResolutionMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);
        Assert.Equal(2, host.Services.GetRequiredService<InMemoryTenantStore>().Count);
        Assert.Equal(2, host.Services.GetServices<ITenantResolver>().Count());

        TenantContext? seen = null;
        var ok = await host.InvokeAsync(ctx =>
        {
            seen = ctx.GetSecurity().Tenant;
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" });
        var disabled = await host.InvokeAsync(static _ => ValueTask.CompletedTask, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "beta" });
        var none = await host.InvokeAsync(static _ => ValueTask.CompletedTask);

        Assert.True(ok.IsSuccess, ok.ToString());
        Assert.Equal("acme", seen?.Id);
        Assert.Equal("Acme", seen?.DisplayName);
        Assert.Equal(MultiTenancyErrors.Disabled, disabled.Error?.Code);
        Assert.True(none.IsSuccess);
    }

    [Fact]
    public async Task Feature_YieldsTenantContext_ToAnExplicitProvider_AndStillProvidesSharding()
    {
        // 应用自带只提供 multitenancy.context@1 的 Provider，复用抽象包的 Tenant 阶段中间件；本包仍被选中提供分片
        var appStore = new InMemoryTenantStore(new InMemoryMultiTenancyOptions
        {
            Tenants = [new TenantInfo { Id = "app-tenant", Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = "Host=app" } }],
        });
        var appProvider = new StubFeature(TestManifests.Create("xihan.app.tenancy", provides: ["multitenancy.context@1"], requires: ["kernel.hosting@1"]))
        {
            OnConfigure = context =>
            {
                context.Services.AddSingleton<ITenantStore>(appStore);
                context.Services.AddSingleton<ITenantResolver, BaggageTenantResolver>();
                context.AddMiddleware<TenantResolutionMiddleware>(PipelineStage.Tenant);
            },
        };

        await using var host = await TestHost.StartAsync(
            b => b.UseFeature<InMemoryMultiTenancyFeature>().UseFeature(appProvider),
            [new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme")]);

        // 中间件只登记一次，存储、解析器与健康项都是应用 Provider 的，本包只留分片
        var node = host.App.Graph.Nodes.Single(static n => n.Manifest.Id == "xihan.multitenancy.inmemory");
        Assert.False(node.IsSelectedFor(MultiTenancyCapabilities.Context));
        Assert.True(node.IsSelectedFor(MultiTenancyCapabilities.Sharding));
        Assert.Single(host.Services.GetServices<ITenantResolver>());
        Assert.Same(appStore, host.Services.GetRequiredService<ITenantStore>());
        Assert.Null(host.Services.GetService<InMemoryTenantStore>());
        Assert.DoesNotContain(host.Services.GetServices<IHealthContributor>(), static c => c is InMemoryTenantStore);
        Assert.IsType<TenantStoreShardResolver>(host.Services.GetRequiredService<ITenantShardResolver>());

        TenantContext? seen = null;
        Maybe<TenantShard> shard = default;
        var ok = await host.InvokeAsync(ctx =>
        {
            seen = ctx.GetSecurity().Tenant;
            shard = ctx.FindTenantShard();
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "app-tenant" });
        var configured = await host.InvokeAsync(static _ => ValueTask.CompletedTask, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" });

        Assert.True(ok.IsSuccess, ok.ToString());
        Assert.Equal("app-tenant", seen?.Id);
        Assert.Equal("Host=app", shard.ValueOrThrow().ConnectionString);
        // 本包配置里的租户不再参与：核对走应用的存储
        Assert.Equal(MultiTenancyErrors.NotFound, configured.Error?.Code);
    }
}
