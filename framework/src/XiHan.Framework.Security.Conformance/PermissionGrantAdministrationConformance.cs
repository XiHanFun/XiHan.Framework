// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;
using Xunit;

namespace XiHan.Framework.Security.Conformance;

/// <summary>
/// <see cref="IPermissionGrantAdministration"/> 的一致性套件
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="StartAsync"/> 与 <see cref="AdministerAsync"/>，全部用例随之执行。每个用例先 <see cref="StartAsync"/> 得到一个空的授予表与读它的判定存储，再经 <see cref="AdministerAsync"/> 在一次调用里使用授予管理：持久化的实现要在调用的事务里写入（ADR-0158）。
/// 判定存储有缓存的实现，写入之前读过的判定在写入之后也要立即看见变化（用例 5）；多实例部署要一致的实现覆写 <see cref="StartPeerAsync"/>，另一个实例在 <see cref="PeerVisibilityTimeout"/> 内看见变化（用例 6，ADR-0229）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class PermissionGrantAdministrationConformance
{
    /// <summary>
    /// 每个租户的角色授予
    /// </summary>
    private static readonly PermissionGrant _editorRead = new(GrantSubject.ForRole("editor"), "orders.read", PermissionGrant.AnyTenant);

    /// <summary>
    /// 租户专属的用户授予
    /// </summary>
    private static readonly PermissionGrant _aliceExport = new(GrantSubject.ForUser("alice"), "reports.export", "acme");

    /// <summary>
    /// 启动一个空的授予表
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次，返回读同一张表的判定存储
    /// </remarks>
    protected abstract ValueTask<IPermissionGrantStore> StartAsync();

    /// <summary>
    /// 在一次调用里使用授予管理
    /// </summary>
    /// <remarks>
    /// 用 <see cref="StartAsync"/> 启动的那张表；持久化的实现在调用的工作单元里执行，调用结束即提交
    /// </remarks>
    protected abstract ValueTask<T> AdministerAsync<T>(Func<IPermissionGrantAdministration, CancellationToken, ValueTask<T>> action);

    /// <summary>
    /// 启动读同一张表的另一个实例
    /// </summary>
    /// <remarks>
    /// 在 <see cref="StartAsync"/> 之后调用，返回与那个实例共用持久化与缓存后端的另一个实例的判定存储。默认 <see langword="null"/>：实现不承诺多实例一致，用例 6 跳过
    /// </remarks>
    protected virtual ValueTask<IPermissionGrantStore?> StartPeerAsync() => ValueTask.FromResult<IPermissionGrantStore?>(null);

    /// <summary>
    /// 另一个实例看见变化的时限
    /// </summary>
    /// <remarks>
    /// 失效经缓存后端的变更通知异步到达另一个实例（如 Redis 频道），给几秒余量；远小于缓存的存活时间，过了时限还看不见说明只靠过期收敛
    /// </remarks>
    protected virtual TimeSpan PeerVisibilityTimeout => TimeSpan.FromSeconds(5);

    /// <summary>
    /// 用例 1：授予逐项同序、已存在的幂等成功，判定立即看见
    /// </summary>
    /// <remarks>
    /// 同一批里重复的项同样成功；空集合返回空列表
    /// </remarks>
    [Fact]
    public async Task Grant_IsIndexAligned_Idempotent_AndVisibleToChecks()
    {
        var store = await StartAsync().ConfigureAwait(false);

        var granted = await AdministerAsync((grants, ct) => grants.GrantAsync([_editorRead, _editorRead, _aliceExport], ct)).ConfigureAwait(false);
        var again = await AdministerAsync((grants, ct) => grants.GrantAsync([_editorRead], ct)).ConfigureAwait(false);
        var empty = await AdministerAsync((grants, ct) => grants.GrantAsync([], ct)).ConfigureAwait(false);

        Assert.Equal(3, granted.Count);
        Assert.All(granted, static r => Assert.True(r.IsSuccess, r.Error?.ToString()));
        Assert.True(Assert.Single(again).IsSuccess);
        Assert.Empty(empty);
        Assert.Equal(["orders.read"], (await store.GetGrantsAsync(GrantSubject.ForRole("editor"), "other", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Empty((await store.GetGrantsAsync(GrantSubject.ForRole("editor"), null, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Equal(["reports.export"], (await store.GetGrantsAsync(GrantSubject.ForUser("alice"), "acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Empty((await store.GetGrantsAsync(GrantSubject.ForUser("alice"), "other", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 2：撤销逐项报告，不存在的是 <see cref="SecurityErrors.GrantNotFound"/>
    /// </summary>
    /// <remarks>
    /// 同一批里同一授予第二次撤销看见第一次的效果，报告不存在；平台、每个租户与租户专属是不同的授予
    /// </remarks>
    [Fact]
    public async Task Revoke_ReportsMissingItems_PerItem()
    {
        var store = await StartAsync().ConfigureAwait(false);
        await AdministerAsync((grants, ct) => grants.GrantAsync([_editorRead, _aliceExport], ct)).ConfigureAwait(false);

        var revoked = await AdministerAsync((grants, ct) => grants.RevokeAsync([_editorRead, _editorRead, new PermissionGrant(GrantSubject.ForUser("alice"), "reports.export", "acme"), new PermissionGrant(GrantSubject.ForUser("alice"), "reports.export", PermissionGrant.AnyTenant)], ct)).ConfigureAwait(false);

        Assert.Equal(4, revoked.Count);
        Assert.True(revoked[0].IsSuccess, revoked[0].Error?.ToString());
        Assert.Equal(SecurityErrors.GrantNotFound, revoked[1].Error?.Code);
        Assert.True(revoked[2].IsSuccess, revoked[2].Error?.ToString());
        Assert.Equal(SecurityErrors.GrantNotFound, revoked[3].Error?.Code);
        Assert.Empty((await store.GetGrantsAsync(GrantSubject.ForRole("editor"), "acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
        Assert.Empty((await store.GetGrantsAsync(GrantSubject.ForUser("alice"), "acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 3：列出按主体与范围过滤、序数顺序分页，总数不受分页影响
    /// </summary>
    /// <remarks>
    /// 顺序是主体种类、主体 ID、权限码、范围（平台在前、每个租户其次，然后按租户 ID）
    /// </remarks>
    [Fact]
    public async Task List_FiltersBySubjectAndTenant_AndPagesInOrdinalOrder()
    {
        await StartAsync().ConfigureAwait(false);
        var adminAll = new PermissionGrant(GrantSubject.ForRole("admin"), PermissionGrant.All, null);
        var aliceRead = new PermissionGrant(GrantSubject.ForUser("alice"), "reports.read", PermissionGrant.AnyTenant);
        var aliceExportEverywhere = new PermissionGrant(GrantSubject.ForUser("alice"), "reports.export", PermissionGrant.AnyTenant);
        var aliceExportPlatform = new PermissionGrant(GrantSubject.ForUser("alice"), "reports.export", null);
        await AdministerAsync((grants, ct) => grants.GrantAsync([_aliceExport, aliceRead, _editorRead, adminAll, aliceExportEverywhere, aliceExportPlatform], ct)).ConfigureAwait(false);

        var all = await ListAsync(new PermissionGrantQuery()).ConfigureAwait(false);
        var alice = await ListAsync(new PermissionGrantQuery { Subject = GrantSubject.ForUser("alice") }).ConfigureAwait(false);
        var platform = await ListAsync(new PermissionGrantQuery { Tenant = GrantTenantFilter.Platform }).ConfigureAwait(false);
        var everyTenant = await ListAsync(new PermissionGrantQuery { Tenant = GrantTenantFilter.Of(PermissionGrant.AnyTenant) }).ConfigureAwait(false);
        var acme = await ListAsync(new PermissionGrantQuery { Tenant = GrantTenantFilter.Of("acme") }).ConfigureAwait(false);
        var page = await ListAsync(new PermissionGrantQuery { Skip = 1, Take = 2 }).ConfigureAwait(false);

        Assert.Equal([adminAll, _editorRead, aliceExportPlatform, aliceExportEverywhere, _aliceExport, aliceRead], all.Items);
        Assert.Equal(6, all.Total);
        Assert.Equal([aliceExportPlatform, aliceExportEverywhere, _aliceExport, aliceRead], alice.Items);
        Assert.Equal([adminAll, aliceExportPlatform], platform.Items);
        Assert.Equal([_editorRead, aliceExportEverywhere, aliceRead], everyTenant.Items);
        Assert.Equal([_aliceExport], acme.Items);
        Assert.Equal([_editorRead, aliceExportPlatform], page.Items);
        Assert.Equal(6, page.Total);
    }

    /// <summary>
    /// 用例 4：分页越界返回 <see cref="SecurityErrors.GrantQueryInvalid"/>，参数与成员同名
    /// </summary>
    [Fact]
    public async Task List_OutOfRangePage_ReturnsValidationError()
    {
        await StartAsync().ConfigureAwait(false);

        foreach (var (skip, take) in new[] { (0, 0), (0, PermissionGrantQuery.MaxTake + 1), (-1, 10) })
        {
            var rejected = await AdministerAsync((grants, ct) => grants.ListAsync(new PermissionGrantQuery { Skip = skip, Take = take }, ct)).ConfigureAwait(false);
            Assert.Equal(SecurityErrors.GrantQueryInvalid, rejected.Error?.Code);
            Assert.Equal(SecurityMessages.GrantQueryPageInvalid, rejected.Error!.MessageKey);
            Assert.Equal<object?>(take, rejected.Error.Details!["take"]);
            Assert.Equal<object?>(PermissionGrantQuery.MaxTake, rejected.Error.Details!["max"]);
        }
    }

    /// <summary>
    /// 列出并要求成功
    /// </summary>
    private async Task<PermissionGrantPage> ListAsync(PermissionGrantQuery query)
    {
        var listed = await AdministerAsync((grants, ct) => grants.ListAsync(query, ct)).ConfigureAwait(false);
        Assert.True(listed.IsSuccess, $"Listing the grants failed: {listed.Error}.");
        return listed.ValueOrThrow();
    }

    /// <summary>
    /// 用例 5：写入之前读过的判定，写入之后立即看见授予与撤销
    /// </summary>
    /// <remarks>
    /// 判定存储有缓存时，写入要让它失效，不能等存活时间
    /// </remarks>
    [Fact]
    public async Task Changes_AreVisibleToChecksThatReadBeforeTheWrite()
    {
        var store = await StartAsync().ConfigureAwait(false);
        var editor = GrantSubject.ForRole("editor");
        Assert.Empty((await store.GetGrantsAsync(editor, "acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

        await AdministerAsync((grants, ct) => grants.GrantAsync([_editorRead], ct)).ConfigureAwait(false);
        Assert.Equal(["orders.read"], (await store.GetGrantsAsync(editor, "acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

        await AdministerAsync((grants, ct) => grants.RevokeAsync([_editorRead], ct)).ConfigureAwait(false);
        Assert.Empty((await store.GetGrantsAsync(editor, "acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());
    }

    /// <summary>
    /// 用例 6：另一个实例在时限内看见授予与撤销
    /// </summary>
    /// <remarks>
    /// 另一个实例写入之前已经读过（缓存了旧结果），之后只能靠失效看见变化；<see cref="StartPeerAsync"/> 返回 <see langword="null"/> 时跳过
    /// </remarks>
    [Fact]
    public async Task Changes_AreVisibleToAnotherInstance_WithoutWaitingForExpiry()
    {
        await StartAsync().ConfigureAwait(false);
        var peer = await StartPeerAsync().ConfigureAwait(false);
        Assert.SkipWhen(peer is null, "The implementation does not start a second instance; override StartPeerAsync when it promises consistency across instances.");
        var editor = GrantSubject.ForRole("editor");
        Assert.Empty((await peer.GetGrantsAsync(editor, "acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow());

        await AdministerAsync((grants, ct) => grants.GrantAsync([_editorRead], ct)).ConfigureAwait(false);
        await WithinAsync(async () => (await peer.GetGrantsAsync(editor, "acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count == 1).ConfigureAwait(false);

        await AdministerAsync((grants, ct) => grants.RevokeAsync([_editorRead], ct)).ConfigureAwait(false);
        await WithinAsync(async () => (await peer.GetGrantsAsync(editor, "acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Count == 0).ConfigureAwait(false);
    }

    /// <summary>
    /// 在 <see cref="PeerVisibilityTimeout"/> 内轮询到条件成立
    /// </summary>
    private async Task WithinAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow + PeerVisibilityTimeout;
        while (!await condition().ConfigureAwait(false))
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, $"The other instance did not see the change within {PeerVisibilityTimeout}.");
            await Task.Delay(20).ConfigureAwait(false);
        }
    }
}
