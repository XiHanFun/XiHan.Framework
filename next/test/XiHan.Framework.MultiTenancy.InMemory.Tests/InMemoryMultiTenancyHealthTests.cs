// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.MultiTenancy.InMemory.Tests;

/// <summary>
/// 健康项 <c>multitenancy.inmemory.capacity</c>：有余量时 Ready，租户数达到容量后 Degraded 并带 <see cref="MultiTenancyErrors.CapacityExceeded"/>。
/// </summary>
public sealed class InMemoryMultiTenancyHealthTests
{
    [Fact]
    public async Task Health_IsReady_WhenTheStoreHasRoom()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryMultiTenancyFeature>(), [new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme")]);

        var status = Assert.Single(host.App.Health().Contributions, static c => c.Name == InMemoryMultiTenancyFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
    }

    [Fact]
    public async Task Health_IsDegraded_WhenTheStoreIsFull()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMultiTenancyFeature>(),
            [new("XiHan:MultiTenancy:InMemory:MaxTenants", "1"), new("XiHan:MultiTenancy:Tenants:acme:DisplayName", "Acme")]);

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == InMemoryMultiTenancyFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(MultiTenancyErrors.CapacityExceeded, status.Error?.Code);
        Assert.Equal(1, status.Error?.Details?["capacity"]);
        Assert.True(health.Ready);
    }
}
