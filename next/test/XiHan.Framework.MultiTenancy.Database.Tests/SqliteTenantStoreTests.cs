// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Sharding;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.MultiTenancy.Database.Tenants;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Database.Tests;

/// <summary>
/// 对着真实的 SQLite（<c>Persistence.SqlSugar</c>，应用自注册客户端）验证 <see cref="TenantRecord"/> 的映射：写入 → 另一个宿主读取 → 属性 JSON 往返 → Tenant 阶段解析与分片 → 改与删。
/// </summary>
public sealed class SqliteTenantStoreTests
{
    [Fact]
    public async Task Upsert_Find_List_Remove_RoundTripThroughSqlite()
    {
        await using var database = new SqliteDatabase();
        var writer = await database.StartHostAsync();
        var acme = new TenantInfo
        {
            Id = "acme",
            DisplayName = "Acme Corp",
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TenantShardKeys.ConnectionString] = "Host=acme;Database=acme",
                [TenantShardKeys.DatabaseProvider] = "PostgreSQL",
                [TenantShardKeys.PropertyPrefix + "schema"] = "acme",
                ["Tier"] = "gold",
                ["tier"] = "lower",
                ["Region"] = "华东",
            },
        };
        var dormant = new TenantInfo { Id = "Dormant-01", IsEnabled = false };
        await TenantHosts.SeedAsync(writer, [acme, dormant]);

        // 另一个宿主从同一个文件读：显示名、禁用标记与属性 JSON（含大小写不同的键与非 ASCII 值）都经真实数据库往返
        var reader = await database.StartHostAsync();
        var store = reader.Services.GetRequiredService<ITenantStore>();
        var found = (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow();
        Assert.Equal("Acme Corp", found.DisplayName);
        Assert.True(found.IsEnabled);
        Assert.Equal(acme.Properties.OrderBy(static p => p.Key, StringComparer.Ordinal), found.Properties.OrderBy(static p => p.Key, StringComparer.Ordinal));
        var dormantFound = (await store.FindAsync("Dormant-01", CancellationToken.None)).ValueOrThrow();
        Assert.False(dormantFound.IsEnabled);
        Assert.Null(dormantFound.DisplayName);
        Assert.Empty(dormantFound.Properties);
        Assert.False((await store.FindAsync("dormant-01", CancellationToken.None)).HasValue);
        Assert.Equal(["Dormant-01", "acme"], (await store.ListAsync(CancellationToken.None)).Select(static t => t.Id));

        // Tenant 阶段解析到数据库里的租户，并把从属性推出的分片写进调用上下文
        TenantContext? tenant = null;
        TenantShard? shard = null;
        var ok = await reader.InvokeAsync(ctx =>
        {
            tenant = ctx.GetSecurity().Tenant;
            shard = ctx.GetTenantShard().ValueOrThrow();
            return ValueTask.CompletedTask;
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" });
        Assert.True(ok.IsSuccess, ok.ToString());
        Assert.Equal("Acme Corp", tenant?.DisplayName);
        Assert.Equal("Host=acme;Database=acme", shard?.ConnectionString);
        Assert.Equal("postgresql", shard?.Provider);
        Assert.Equal("acme", shard?.Properties["schema"]);
        var disabled = await reader.InvokeAsync(static _ => ValueTask.CompletedTask, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "Dormant-01" });
        Assert.Equal(MultiTenancyErrors.Disabled, disabled.Error?.Code);

        // 写宿主改与删并提交后，读宿主的缓存还是旧值，失效后才看到
        var written = await writer.InvokeAsync(async context =>
        {
            var records = context.Services.GetRequiredService<TenantRecordStore>();
            Assert.All(await records.UpsertAsync([acme with { DisplayName = "Acme 2" }], context.CancellationToken), static r => Assert.True(r.Outcome.IsSuccess, r.Outcome.ToString()));
            Assert.All(await records.RemoveAsync(["Dormant-01"], context.CancellationToken), static r => Assert.True(r.Outcome.IsSuccess, r.Outcome.ToString()));
        });
        Assert.True(written.IsSuccess, written.ToString());
        Assert.Equal("Acme Corp", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        reader.Services.GetRequiredService<DatabaseTenantStore>().Invalidate();
        Assert.Equal("Acme 2", (await store.FindAsync("acme", CancellationToken.None)).ValueOrThrow().DisplayName);
        Assert.False((await store.FindAsync("Dormant-01", CancellationToken.None)).HasValue);
        Assert.Equal(["acme"], (await store.ListAsync(CancellationToken.None)).Select(static t => t.Id));
    }
}
