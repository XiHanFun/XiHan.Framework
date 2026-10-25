// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Resolution;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 内存多租户包的测试：从配置装载租户、存储的容量与增删，以及能力包在宿主里的租户解析。
/// </summary>
public sealed class InMemoryMultiTenancyTests
{
    /// <summary>
    /// 用内存键值对构建配置。
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
        Assert.Equal("replaced", (await store.FindAsync("a", CancellationToken.None)).ValueOrThrow().DisplayName);
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
}
