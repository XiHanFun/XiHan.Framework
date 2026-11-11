// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.MultiTenancy.Conformance;

/// <summary>
/// <see cref="ITenantStore"/> 的一致性套件
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateStoreAsync"/>，全部用例随之执行
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class TenantStoreConformance
{
    /// <summary>
    /// 套件租户：启用、带显示名与属性
    /// </summary>
    public static TenantInfo Acme { get; } = new()
    {
        Id = "acme",
        DisplayName = "Acme Corp",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["db"] = "Host=acme;Database=app", ["Tier"] = "gold" },
    };

    /// <summary>
    /// 套件租户：禁用、无显示名
    /// </summary>
    public static TenantInfo Dormant { get; } = new() { Id = "Dormant-01", IsEnabled = false };

    /// <summary>
    /// 用给定租户创建被测存储
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次
    /// </remarks>
    protected abstract ValueTask<ITenantStore> CreateStoreAsync(IReadOnlyList<TenantInfo> tenants);

    /// <summary>
    /// 用例 1：按 ID 查找返回完整信息
    /// </summary>
    /// <remarks>
    /// 不存在为无值
    /// </remarks>
    [Fact]
    public async Task Find_ReturnsSeededTenant_AndNoneForMissing()
    {
        var store = await CreateStoreAsync([Acme, Dormant]).ConfigureAwait(false);

        var acme = (await FoundAsync(store, "acme").ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("acme", acme.Id);
        Assert.Equal("Acme Corp", acme.DisplayName);
        Assert.True(acme.IsEnabled);
        Assert.Equal("Host=acme;Database=app", acme.Properties["db"]);
        Assert.Equal("gold", acme.Properties["Tier"]);
        Assert.False(acme.Properties.ContainsKey("tier"));

        Assert.False((await FoundAsync(store, "nobody").ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 2：ID 区分大小写
    /// </summary>
    [Fact]
    public async Task Find_IsCaseSensitive()
    {
        var store = await CreateStoreAsync([Acme, Dormant]).ConfigureAwait(false);

        Assert.True((await FoundAsync(store, "Dormant-01").ConfigureAwait(false)).HasValue);
        Assert.False((await FoundAsync(store, "dormant-01").ConfigureAwait(false)).HasValue);
        Assert.False((await FoundAsync(store, "ACME").ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 3：禁用标记与空显示名完整往返
    /// </summary>
    [Fact]
    public async Task Find_PreservesDisabledFlag_AndMissingDisplayName()
    {
        var store = await CreateStoreAsync([Dormant]).ConfigureAwait(false);

        var dormant = (await FoundAsync(store, "Dormant-01").ConfigureAwait(false)).ValueOrThrow();
        Assert.False(dormant.IsEnabled);
        Assert.Null(dormant.DisplayName);
        Assert.Empty(dormant.Properties);
    }

    /// <summary>
    /// 用例 4：分页列出全部租户，每页不超过每页条数，续页令牌为空即读完
    /// </summary>
    /// <remarks>
    /// 只核对集合与分页的边界，不核对跨页的先后：关系库按自己的排序规则排 ID，与序数不一定相同
    /// </remarks>
    [Fact]
    public async Task List_PagesThroughAllTenants_AndFiltersEnabled()
    {
        TenantInfo[] seeded = [Acme, Dormant, new() { Id = "beta" }, new() { Id = "delta" }, new() { Id = "gamma" }];
        var store = await CreateStoreAsync(seeded).ConfigureAwait(false);

        var all = await ListAllAsync(store, new TenantQuery { PageSize = 2 }).ConfigureAwait(false);
        Assert.Equal(seeded.Select(static t => t.Id).Order(StringComparer.Ordinal), all.Select(static t => t.Id).Order(StringComparer.Ordinal));

        var enabled = await ListAllAsync(store, new TenantQuery { PageSize = 2, EnabledOnly = true }).ConfigureAwait(false);
        Assert.DoesNotContain(enabled, static t => t.Id == Dormant.Id);
        Assert.Equal(4, enabled.Count);

        var empty = await CreateStoreAsync([]).ConfigureAwait(false);
        var page = (await empty.ListAsync(new TenantQuery(), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Empty(page.Items);
        Assert.Null(page.ContinuationToken);
    }

    /// <summary>
    /// 用例 5：每页条数越界或续页令牌不是存储给出的，以校验错误返回
    /// </summary>
    [Fact]
    public async Task List_RejectsOutOfRangePagesAndForeignTokens()
    {
        var store = await CreateStoreAsync([Acme]).ConfigureAwait(false);

        foreach (var query in new[] { new TenantQuery { PageSize = 0 }, new TenantQuery { PageSize = TenantQuery.MaxPageSize + 1 }, new TenantQuery { ContinuationToken = "%%%" } })
        {
            var listed = await store.ListAsync(query, CancellationToken.None).ConfigureAwait(false);
            Assert.True(listed.IsFailure, $"The query {query} should be rejected.");
            Assert.Equal(MultiTenancyErrors.QueryInvalid, listed.Error!.Code);
        }
    }

    /// <summary>
    /// 按 ID 查找并要求成功：套件的存储都可达，失败即不符合套件
    /// </summary>
    private static async Task<Maybe<TenantInfo>> FoundAsync(ITenantStore store, string tenantId)
    {
        var found = await store.FindAsync(tenantId, CancellationToken.None).ConfigureAwait(false);
        Assert.True(found.IsSuccess, $"Finding tenant '{tenantId}' failed: {found.Error}.");
        return found.ValueOrThrow();
    }

    /// <summary>
    /// 逐页读完，并核对每页不超过每页条数、同一租户不出现两次
    /// </summary>
    private static async Task<List<TenantInfo>> ListAllAsync(ITenantStore store, TenantQuery query)
    {
        var all = new List<TenantInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var listed = await store.ListAsync(query, CancellationToken.None).ConfigureAwait(false);
            Assert.True(listed.IsSuccess, $"Listing tenants failed: {listed.Error}.");
            var page = listed.ValueOrThrow();
            Assert.True(page.Items.Count <= query.PageSize, $"A page returned {page.Items.Count} tenants, more than the page size {query.PageSize}.");
            Assert.All(page.Items, tenant => Assert.True(seen.Add(tenant.Id), $"Tenant '{tenant.Id}' was listed twice."));
            all.AddRange(page.Items);
            if (page.ContinuationToken is null)
            {
                return all;
            }

            query = query with { ContinuationToken = page.ContinuationToken };
        }
    }
}
