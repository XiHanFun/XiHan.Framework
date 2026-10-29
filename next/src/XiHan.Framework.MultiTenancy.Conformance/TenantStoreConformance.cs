// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.MultiTenancy.Conformance;

/// <summary>
/// <see cref="ITenantStore"/> 的一致性套件。
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateStoreAsync"/>，全部用例随之执行。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class TenantStoreConformance
{
    /// <summary>
    /// 套件租户：启用、带显示名与属性。
    /// </summary>
    public static TenantInfo Acme { get; } = new()
    {
        Id = "acme",
        DisplayName = "Acme Corp",
        Properties = new Dictionary<string, string>(StringComparer.Ordinal) { ["db"] = "Host=acme;Database=app", ["Tier"] = "gold" },
    };

    /// <summary>
    /// 套件租户：禁用、无显示名。
    /// </summary>
    public static TenantInfo Dormant { get; } = new() { Id = "Dormant-01", IsEnabled = false };

    /// <summary>
    /// 用给定租户创建被测存储。
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次。
    /// </remarks>
    protected abstract ValueTask<ITenantStore> CreateStoreAsync(IReadOnlyList<TenantInfo> tenants);

    /// <summary>
    /// 用例 1：按 ID 查找返回完整信息。
    /// </summary>
    /// <remarks>
    /// 不存在为无值。
    /// </remarks>
    [Fact]
    public async Task Find_ReturnsSeededTenant_AndNoneForMissing()
    {
        var store = await CreateStoreAsync([Acme, Dormant]).ConfigureAwait(false);

        var acme = (await store.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Equal("acme", acme.Id);
        Assert.Equal("Acme Corp", acme.DisplayName);
        Assert.True(acme.IsEnabled);
        Assert.Equal("Host=acme;Database=app", acme.Properties["db"]);
        Assert.Equal("gold", acme.Properties["Tier"]);
        Assert.False(acme.Properties.ContainsKey("tier"));

        Assert.False((await store.FindAsync("nobody", CancellationToken.None).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 2：ID 区分大小写。
    /// </summary>
    [Fact]
    public async Task Find_IsCaseSensitive()
    {
        var store = await CreateStoreAsync([Acme, Dormant]).ConfigureAwait(false);

        Assert.True((await store.FindAsync("Dormant-01", CancellationToken.None).ConfigureAwait(false)).HasValue);
        Assert.False((await store.FindAsync("dormant-01", CancellationToken.None).ConfigureAwait(false)).HasValue);
        Assert.False((await store.FindAsync("ACME", CancellationToken.None).ConfigureAwait(false)).HasValue);
    }

    /// <summary>
    /// 用例 3：禁用标记与空显示名完整往返。
    /// </summary>
    [Fact]
    public async Task Find_PreservesDisabledFlag_AndMissingDisplayName()
    {
        var store = await CreateStoreAsync([Dormant]).ConfigureAwait(false);

        var dormant = (await store.FindAsync("Dormant-01", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.False(dormant.IsEnabled);
        Assert.Null(dormant.DisplayName);
        Assert.Empty(dormant.Properties);
    }

    /// <summary>
    /// 用例 4：枚举返回全部租户，空存储返回空列表。
    /// </summary>
    [Fact]
    public async Task List_ReturnsAllTenants()
    {
        var store = await CreateStoreAsync([Acme, Dormant]).ConfigureAwait(false);
        var empty = await CreateStoreAsync([]).ConfigureAwait(false);

        var all = await store.ListAsync(CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(["Dormant-01", "acme"], all.Select(static t => t.Id).OrderBy(static id => id, StringComparer.Ordinal));
        Assert.Empty(await empty.ListAsync(CancellationToken.None).ConfigureAwait(false));
    }
}
