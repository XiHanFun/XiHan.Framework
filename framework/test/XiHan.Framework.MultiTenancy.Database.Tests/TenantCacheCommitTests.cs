// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Database.Tenants;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 租户目录的写入在提交之后才让缓存失效（ADR-0194）
/// </summary>
/// <remarks>
/// 停用写在调用方的事务里，提交之前别的调用读到的还是旧行：那段时间里读回的结果不能留在目录与分片解析器的缓存里
/// </remarks>
public sealed class TenantCacheCommitTests
{
    [Fact]
    public async Task ReadBetweenTheWriteAndItsCommit_DoesNotKeepTheOldTenantInTheCaches()
    {
        await using var host = await TenantHosts.StartAsync();
        var store = host.Services.GetRequiredService<ITenantStore>();
        var shards = host.Services.GetRequiredService<ITenantShardResolver>();
        await TenantHosts.SeedAsync(host, [Acme(enabled: true, "Host=old")]);
        Assert.True((await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().IsEnabled);

        (await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<TenantRecordStore>().UpsertAsync([Acme(enabled: false, "Host=new")], context.CancellationToken);
            Assert.True(results.Single().IsSuccess);

            // 停用还没提交：并发的解析读到旧行，写回目录与分片解析器的缓存
            Assert.True((await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().IsEnabled);
            Assert.Equal("Host=old", (await shards.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
        })).ThrowIfFailure();

        // 修之前提交前读回的旧值留在缓存里，到存活时间结束前租户仍是启用、仍指向旧库
        Assert.False((await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().IsEnabled);
        Assert.Equal("Host=new", (await shards.ResolveAsync("acme", CancellationToken.None)).ValueOrThrow().ValueOrThrow().ConnectionString);
    }

    /// <summary>
    /// 租户 acme
    /// </summary>
    private static TenantInfo Acme(bool enabled, string connectionString) => new()
    {
        Id = "acme",
        DisplayName = "Acme",
        IsEnabled = enabled,
        Properties = new Dictionary<string, string>(StringComparer.Ordinal) { [TenantShardKeys.ConnectionString] = connectionString },
    };
}
