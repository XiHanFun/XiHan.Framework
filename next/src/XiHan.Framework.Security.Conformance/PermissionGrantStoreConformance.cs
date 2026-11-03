// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;
using Xunit;

namespace XiHan.Framework.Security.Conformance;

/// <summary>
/// <see cref="IPermissionGrantStore"/> 的一致性套件
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateStoreAsync"/>，全部用例随之执行；访问外部存储的 Provider 还要覆写 <see cref="CreateUnavailableStoreAsync"/>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class PermissionGrantStoreConformance
{
    /// <summary>
    /// 套件的种子授予：角色与用户、全局与租户专属、仅大小写不同的用户 ID 各占一例
    /// </summary>
    private static readonly PermissionGrant[] _seed =
    [
        new(GrantSubject.ForRole("editor"), "orders.read"),
        new(GrantSubject.ForRole("editor"), "orders.write"),
        new(GrantSubject.ForRole("admin"), PermissionGrant.All),
        new(GrantSubject.ForUser("alice"), "reports.read"),
        new(GrantSubject.ForUser("alice"), "reports.export", "acme"),
        new(GrantSubject.ForUser("Bob"), "audit.read"),
    ];

    /// <summary>
    /// 用给定授予创建被测存储
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次
    /// </remarks>
    protected abstract ValueTask<IPermissionGrantStore> CreateStoreAsync(IReadOnlyList<PermissionGrant> grants);

    /// <summary>
    /// 创建背后存储不可达的被测存储（连接指向不存在的数据库、服务已停止等）
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="null"/>，用例 5 跳过：只有进程内的存储（读取不会失败）才可以不覆写。访问外部存储的 Provider 必须覆写，证明故障返回 <see cref="SecurityErrors.StoreUnavailable"/> 而不是空集合。在完整的应用宿主上实现时，不要让整个宿主的库不可达（删库、断开默认连接）：宿主里别的子系统的后台循环——工作流轮询与它的领导权、发件箱投递、调度、翻译加载等——在服务状态下会如实报存储不可达（如 <c>WORKFLOW_UNAVAILABLE_001</c>），那是真实的故障信号。只装被测特性的最小宿主（框架 <c>Security.Database</c> 的测试就是这样），或只让被测存储用的连接不可达
    /// </remarks>
    protected virtual ValueTask<IPermissionGrantStore?> CreateUnavailableStoreAsync() => ValueTask.FromResult<IPermissionGrantStore?>(null);

    /// <summary>
    /// 用例 1：角色与用户的授予分别返回
    /// </summary>
    /// <remarks>
    /// 未知主体为空
    /// </remarks>
    [Fact]
    public async Task Grants_AreReturnedPerSubject_AndEmptyForUnknown()
    {
        var store = await CreateStoreAsync(_seed).ConfigureAwait(false);

        Assert.Equal(["orders.read", "orders.write"], Sorted(await GrantsAsync(store, GrantSubject.ForRole("editor"), null, CancellationToken.None).ConfigureAwait(false)));
        Assert.Equal([PermissionGrant.All], Sorted(await GrantsAsync(store, GrantSubject.ForRole("admin"), null, CancellationToken.None).ConfigureAwait(false)));
        Assert.Equal(["reports.read"], Sorted(await GrantsAsync(store, GrantSubject.ForUser("alice"), null, CancellationToken.None).ConfigureAwait(false)));
        Assert.Empty(await GrantsAsync(store, GrantSubject.ForUser("nobody"), null, CancellationToken.None).ConfigureAwait(false));
        Assert.Empty(await GrantsAsync(store, GrantSubject.ForRole("alice"), null, CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 2：全局授予对任何租户生效，租户专属授予只对该租户生效
    /// </summary>
    [Fact]
    public async Task TenantScopedGrants_ApplyOnlyToThatTenant()
    {
        var store = await CreateStoreAsync(_seed).ConfigureAwait(false);

        Assert.Equal(["reports.export", "reports.read"], Sorted(await GrantsAsync(store, GrantSubject.ForUser("alice"), "acme", CancellationToken.None).ConfigureAwait(false)));
        Assert.Equal(["reports.read"], Sorted(await GrantsAsync(store, GrantSubject.ForUser("alice"), "other", CancellationToken.None).ConfigureAwait(false)));
        Assert.Equal(["orders.read", "orders.write"], Sorted(await GrantsAsync(store, GrantSubject.ForRole("editor"), "acme", CancellationToken.None).ConfigureAwait(false)));
    }

    /// <summary>
    /// 用例 3：主体 ID 区分大小写
    /// </summary>
    [Fact]
    public async Task SubjectIds_AreCaseSensitive()
    {
        var store = await CreateStoreAsync(_seed).ConfigureAwait(false);

        Assert.Equal(["audit.read"], Sorted(await GrantsAsync(store, GrantSubject.ForUser("Bob"), null, CancellationToken.None).ConfigureAwait(false)));
        Assert.Empty(await GrantsAsync(store, GrantSubject.ForUser("bob"), null, CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 4：空存储对任何查询返回空
    /// </summary>
    [Fact]
    public async Task EmptyStore_ReturnsNothing()
    {
        var store = await CreateStoreAsync([]).ConfigureAwait(false);

        Assert.Empty(await GrantsAsync(store, GrantSubject.ForRole("admin"), "acme", CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 5：存储不可达时返回 <see cref="SecurityErrors.StoreUnavailable"/>，不能以空集合代替
    /// </summary>
    /// <remarks>
    /// 空集合会被检查器判定为不持有（403）；存储故障必须以失败返回，调用以 503 结束（RFC-0008，ADR-0071）
    /// </remarks>
    [Fact]
    public async Task UnavailableStore_ReturnsStoreUnavailable_InsteadOfNoGrants()
    {
        var store = await CreateUnavailableStoreAsync().ConfigureAwait(false);
        Assert.SkipWhen(store is null, "No unavailable store is provided; override CreateUnavailableStoreAsync when the store reads external storage.");

        var read = await store!.GetGrantsAsync(GrantSubject.ForRole("admin"), "acme", CancellationToken.None).ConfigureAwait(false);

        Assert.True(read.IsFailure);
        Assert.Equal(SecurityErrors.StoreUnavailable, read.Error!.Code);
        Assert.Equal("UNAVAILABLE", read.Error.Kind);
    }

    /// <summary>
    /// 读取授予并要求成功：前四个用例的存储都可达，失败即不符合套件
    /// </summary>
    private static async Task<IReadOnlyCollection<string>> GrantsAsync(IPermissionGrantStore store, GrantSubject subject, string? tenantId, CancellationToken cancellationToken)
    {
        var read = await store.GetGrantsAsync(subject, tenantId, cancellationToken).ConfigureAwait(false);
        Assert.True(read.IsSuccess, $"Reading the grants of {subject} failed: {read.Error}.");
        return read.ValueOrThrow();
    }

    /// <summary>
    /// 按序数排序，便于与期望列表逐项比较
    /// </summary>
    private static List<string> Sorted(IReadOnlyCollection<string> grants) => grants.OrderBy(static g => g, StringComparer.Ordinal).ToList();
}
