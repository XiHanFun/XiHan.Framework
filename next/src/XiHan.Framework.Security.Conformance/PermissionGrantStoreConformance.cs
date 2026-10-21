// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using Xunit;

namespace XiHan.Framework.Security.Conformance;

/// <summary>
/// <see cref="IPermissionGrantStore"/> 的一致性套件。继承并实现 <see cref="CreateStoreAsync"/>，全部用例随之执行。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class PermissionGrantStoreConformance
{
    private static readonly PermissionGrant[] Seed =
    [
        new(GrantSubject.ForRole("editor"), "orders.read"),
        new(GrantSubject.ForRole("editor"), "orders.write"),
        new(GrantSubject.ForRole("admin"), PermissionGrant.All),
        new(GrantSubject.ForUser("alice"), "reports.read"),
        new(GrantSubject.ForUser("alice"), "reports.export", "acme"),
        new(GrantSubject.ForUser("Bob"), "audit.read"),
    ];

    /// <summary>
    /// 用给定授予创建被测存储。每个用例调用一次。
    /// </summary>
    protected abstract ValueTask<IPermissionGrantStore> CreateStoreAsync(IReadOnlyList<PermissionGrant> grants);

    /// <summary>
    /// 用例 1：角色与用户的授予分别返回；未知主体为空。
    /// </summary>
    [Fact]
    public async Task Grants_AreReturnedPerSubject_AndEmptyForUnknown()
    {
        var store = await CreateStoreAsync(Seed).ConfigureAwait(false);

        Assert.Equal(["orders.read", "orders.write"], Sorted(await store.GetGrantsAsync(GrantSubject.ForRole("editor"), null, CancellationToken.None).ConfigureAwait(false)));
        Assert.Equal([PermissionGrant.All], Sorted(await store.GetGrantsAsync(GrantSubject.ForRole("admin"), null, CancellationToken.None).ConfigureAwait(false)));
        Assert.Equal(["reports.read"], Sorted(await store.GetGrantsAsync(GrantSubject.ForUser("alice"), null, CancellationToken.None).ConfigureAwait(false)));
        Assert.Empty(await store.GetGrantsAsync(GrantSubject.ForUser("nobody"), null, CancellationToken.None).ConfigureAwait(false));
        Assert.Empty(await store.GetGrantsAsync(GrantSubject.ForRole("alice"), null, CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 2：全局授予对任何租户生效，租户专属授予只对该租户生效。
    /// </summary>
    [Fact]
    public async Task TenantScopedGrants_ApplyOnlyToThatTenant()
    {
        var store = await CreateStoreAsync(Seed).ConfigureAwait(false);

        Assert.Equal(["reports.export", "reports.read"], Sorted(await store.GetGrantsAsync(GrantSubject.ForUser("alice"), "acme", CancellationToken.None).ConfigureAwait(false)));
        Assert.Equal(["reports.read"], Sorted(await store.GetGrantsAsync(GrantSubject.ForUser("alice"), "other", CancellationToken.None).ConfigureAwait(false)));
        Assert.Equal(["orders.read", "orders.write"], Sorted(await store.GetGrantsAsync(GrantSubject.ForRole("editor"), "acme", CancellationToken.None).ConfigureAwait(false)));
    }

    /// <summary>
    /// 用例 3：主体 ID 区分大小写。
    /// </summary>
    [Fact]
    public async Task SubjectIds_AreCaseSensitive()
    {
        var store = await CreateStoreAsync(Seed).ConfigureAwait(false);

        Assert.Equal(["audit.read"], Sorted(await store.GetGrantsAsync(GrantSubject.ForUser("Bob"), null, CancellationToken.None).ConfigureAwait(false)));
        Assert.Empty(await store.GetGrantsAsync(GrantSubject.ForUser("bob"), null, CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 4：空存储对任何查询返回空。
    /// </summary>
    [Fact]
    public async Task EmptyStore_ReturnsNothing()
    {
        var store = await CreateStoreAsync([]).ConfigureAwait(false);

        Assert.Empty(await store.GetGrantsAsync(GrantSubject.ForRole("admin"), "acme", CancellationToken.None).ConfigureAwait(false));
    }

    private static List<string> Sorted(IReadOnlyCollection<string> grants) => grants.OrderBy(static g => g, StringComparer.Ordinal).ToList();
}
