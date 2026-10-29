// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.InMemory.Tests;

/// <summary>
/// 健康项 <c>security.inmemory.capacity</c>。
/// </summary>
/// <remarks>
/// 两个存储都有余量时 Ready，主体存储填满后 Degraded 并带 <see cref="SecurityErrors.CapacityExceeded"/>。
/// </remarks>
public sealed class InMemorySecurityHealthTests
{
    /// <summary>
    /// 两个主体的配置。
    /// </summary>
    private static readonly KeyValuePair<string, string?>[] _principals =
    [
        new("XiHan:Security:InMemory:Principals:alice:ApiKey", "dev-alice-key"),
        new("XiHan:Security:InMemory:Principals:bob:ApiKey", "dev-bob-key"),
    ];

    [Fact]
    public async Task Health_IsReady_WhenBothStoresHaveRoom()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>(), _principals);

        var status = Assert.Single(host.App.Health().Contributions, static c => c.Name == InMemorySecurityFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
    }

    [Fact]
    public async Task Health_IsDegraded_WhenThePrincipalStoreIsFull()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemorySecurityFeature>(), [.. _principals, new("XiHan:Security:InMemory:MaxPrincipals", "2")]);

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == InMemorySecurityFeature.HealthName);
        Assert.Equal(LifecycleState.Degraded, status.State);
        Assert.Equal(SecurityErrors.CapacityExceeded, status.Error?.Code);
        Assert.Equal(SecurityMessages.PrincipalStoreCapacityExceeded, status.Error?.MessageKey);
        Assert.Equal(2, status.Error?.Details?["capacity"]);
        Assert.True(health.Ready);
    }
}
