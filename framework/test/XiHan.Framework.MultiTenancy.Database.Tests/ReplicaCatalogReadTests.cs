// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 配了从库时租户目录从主库读（ADR-0122）
/// </summary>
/// <remarks>
/// 两个 SQLite 文件代替一主一从，从库不复制，等于落后：停用还没传过来。读从库的话，停用要多等复制延迟，读回来的旧值还要再缓存一轮
/// </remarks>
public sealed class ReplicaCatalogReadTests
{
    [Fact]
    public async Task DisabledTenant_IsDisabled_WhileTheReplicaStillSaysEnabled()
    {
        // 主库的宿主先停：它的探针还在读从库文件
        await using var replica = new SqliteDatabase();
        await using var primary = new SqliteDatabase();
        await TenantHosts.SeedAsync(await replica.StartHostAsync(), [new TenantInfo { Id = "acme" }]);

        var host = await primary.StartReplicatedHostAsync(replica);
        await TenantHosts.SeedAsync(host, [new TenantInfo { Id = "acme", IsEnabled = false }]);

        var found = (await host.Services.GetRequiredService<ITenantStore>().FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow();
        Assert.False(found.IsEnabled);
    }

    [Fact]
    public async Task RemovedTenant_IsGone_WhileTheReplicaStillHasIt()
    {
        await using var replica = new SqliteDatabase();
        await using var primary = new SqliteDatabase();
        await TenantHosts.SeedAsync(await replica.StartHostAsync(), [new TenantInfo { Id = "acme" }]);

        var host = await primary.StartReplicatedHostAsync(replica);

        Assert.False((await host.Services.GetRequiredService<ITenantStore>().FindAsync("acme", CancellationToken.None)).ValueOrThrow().HasValue);
    }
}
