// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.MultiTenancy.Abstractions;
using XiHan.Framework.MultiTenancy.Abstractions.Tenants;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.MultiTenancy.Conformance;

/// <summary>
/// <see cref="ITenantAdministration"/> 的一致性套件
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="StartAsync"/> 与 <see cref="AdministerAsync"/>，全部用例随之执行。每个用例先 <see cref="StartAsync"/> 得到一个空的租户表与读它的租户目录，再经 <see cref="AdministerAsync"/> 在一次调用里使用租户管理：持久化的实现要在调用的事务里写入（ADR-0160）。
/// 目录有缓存的实现，写入之前查过的租户在写入之后也要立即看见变化（用例 4）；多实例部署要一致的实现覆写 <see cref="StartPeerAsync"/>，另一个实例在 <see cref="PeerVisibilityTimeout"/> 内看见变化（用例 5，ADR-0229）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class TenantAdministrationConformance
{
    /// <summary>
    /// 启动一个空的租户表
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次，返回读同一张表的租户目录
    /// </remarks>
    protected abstract ValueTask<ITenantStore> StartAsync();

    /// <summary>
    /// 在一次调用里使用租户管理
    /// </summary>
    protected abstract ValueTask<T> AdministerAsync<T>(Func<ITenantAdministration, CancellationToken, ValueTask<T>> action);

    /// <summary>
    /// 启动读同一张表的另一个实例
    /// </summary>
    /// <remarks>
    /// 在 <see cref="StartAsync"/> 之后调用，返回与那个实例共用持久化与缓存后端的另一个实例的租户目录。默认 <see langword="null"/>：实现不承诺多实例一致，用例 5 跳过
    /// </remarks>
    protected virtual ValueTask<ITenantStore?> StartPeerAsync() => ValueTask.FromResult<ITenantStore?>(null);

    /// <summary>
    /// 另一个实例看见变化的时限
    /// </summary>
    /// <remarks>
    /// 失效经缓存后端的变更通知异步到达另一个实例（如 Redis 频道），给几秒余量；远小于缓存的存活时间，过了时限还看不见说明只靠过期收敛
    /// </remarks>
    protected virtual TimeSpan PeerVisibilityTimeout => TimeSpan.FromSeconds(5);

    /// <summary>
    /// 用例 1：写入逐项同序，同一批里后一项整份覆盖前一项，目录随即看见
    /// </summary>
    /// <remarks>
    /// 空集合返回空列表
    /// </remarks>
    [Fact]
    public async Task Upsert_IsIndexAligned_LaterItemsWin_AndVisibleToTheCatalog()
    {
        var store = await StartAsync().ConfigureAwait(false);
        var first = await AdministerAsync((tenants, ct) => tenants.UpsertAsync([new TenantInfo { Id = "acme", DisplayName = "Acme 1" }, new TenantInfo { Id = "beta" }, new TenantInfo { Id = "acme", DisplayName = "Acme 2" }], ct)).ConfigureAwait(false);
        var empty = await AdministerAsync((tenants, ct) => tenants.UpsertAsync([], ct)).ConfigureAwait(false);

        Assert.Equal(3, first.Count);
        Assert.All(first, static r => Assert.True(r.IsSuccess, r.Error?.ToString()));
        Assert.Empty(empty);
        Assert.Equal("Acme 2", (await store.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().DisplayName);
        Assert.True((await store.FindAsync("beta", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);

        // 替换是整份覆盖：没给的显示名被清空
        await AdministerAsync((tenants, ct) => tenants.UpsertAsync([new TenantInfo { Id = "acme" }], ct)).ConfigureAwait(false);
        Assert.Null((await store.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().DisplayName);
    }

    /// <summary>
    /// 用例 2：删除逐项报告，不存在的是 <see cref="MultiTenancyErrors.NotFound"/>
    /// </summary>
    /// <remarks>
    /// 同一批里同一 ID 第二次删除看见第一次的效果，报告不存在
    /// </remarks>
    [Fact]
    public async Task Remove_ReportsMissingItems_PerItem()
    {
        var store = await StartAsync().ConfigureAwait(false);
        await AdministerAsync((tenants, ct) => tenants.UpsertAsync([new TenantInfo { Id = "acme" }], ct)).ConfigureAwait(false);

        var removed = await AdministerAsync((tenants, ct) => tenants.RemoveAsync(["acme", "acme", "ghost"], ct)).ConfigureAwait(false);

        Assert.Equal(3, removed.Count);
        Assert.True(removed[0].IsSuccess, removed[0].Error?.ToString());
        Assert.Equal(MultiTenancyErrors.NotFound, removed[1].Error?.Code);
        Assert.Equal(MultiTenancyErrors.NotFound, removed[2].Error?.Code);
        Assert.False((await store.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
        Assert.Empty(await AdministerAsync((tenants, ct) => tenants.RemoveAsync([], ct)).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 3：删除的 ID 留墓碑，不能再新建
    /// </summary>
    /// <remarks>
    /// 删除之后读不到它，再新建这一项为 <see cref="MultiTenancyErrors.IdRetired"/>，同一批里别的项照常；再删一次是不存在（ADR-0225）
    /// </remarks>
    [Fact]
    public async Task Remove_RetiresTheId_SoItCannotBeRecreated()
    {
        var store = await StartAsync().ConfigureAwait(false);
        await AdministerAsync((tenants, ct) => tenants.UpsertAsync([new TenantInfo { Id = "acme" }], ct)).ConfigureAwait(false);
        Assert.True((await AdministerAsync((tenants, ct) => tenants.RemoveAsync(["acme"], ct)).ConfigureAwait(false))[0].IsSuccess);

        var recreated = await AdministerAsync((tenants, ct) => tenants.UpsertAsync([new TenantInfo { Id = "acme" }, new TenantInfo { Id = "beta" }], ct)).ConfigureAwait(false);
        Assert.Equal(MultiTenancyErrors.IdRetired, recreated[0].Error?.Code);
        Assert.Equal("acme", recreated[0].Error?.Details?["tenantId"]);
        Assert.True(recreated[1].IsSuccess, recreated[1].Error?.ToString());

        Assert.False((await store.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
        Assert.Equal(["beta"], (await store.ListAsync(new TenantQuery(), CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Items.Select(static t => t.Id));
        Assert.Equal(MultiTenancyErrors.NotFound, (await AdministerAsync((tenants, ct) => tenants.RemoveAsync(["acme"], ct)).ConfigureAwait(false))[0].Error?.Code);
    }

    /// <summary>
    /// 用例 4：写入之前查过的租户，写入之后立即看见新增、停用与删除
    /// </summary>
    /// <remarks>
    /// 目录有缓存时（含缓存「不存在」），写入要让它失效，不能等存活时间
    /// </remarks>
    [Fact]
    public async Task Changes_AreVisibleToLookupsThatReadBeforeTheWrite()
    {
        var store = await StartAsync().ConfigureAwait(false);
        Assert.False((await store.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);

        await AdministerAsync((tenants, ct) => tenants.UpsertAsync([new TenantInfo { Id = "acme" }], ct)).ConfigureAwait(false);
        Assert.True((await store.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().IsEnabled);

        await AdministerAsync((tenants, ct) => tenants.UpsertAsync([new TenantInfo { Id = "acme", IsEnabled = false }], ct)).ConfigureAwait(false);
        Assert.False((await store.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().IsEnabled);

        await AdministerAsync((tenants, ct) => tenants.RemoveAsync(["acme"], ct)).ConfigureAwait(false);
        Assert.False((await store.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);
    }

    /// <summary>
    /// 用例 5：另一个实例在时限内看见新增、停用与删除
    /// </summary>
    /// <remarks>
    /// 另一个实例写入之前已经查过（缓存了旧结果），之后只能靠失效看见变化；<see cref="StartPeerAsync"/> 返回 <see langword="null"/> 时跳过
    /// </remarks>
    [Fact]
    public async Task Changes_AreVisibleToAnotherInstance_WithoutWaitingForExpiry()
    {
        await StartAsync().ConfigureAwait(false);
        var peer = await StartPeerAsync().ConfigureAwait(false);
        Assert.SkipWhen(peer is null, "The implementation does not start a second instance; override StartPeerAsync when it promises consistency across instances.");
        Assert.False((await peer.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue);

        await AdministerAsync((tenants, ct) => tenants.UpsertAsync([new TenantInfo { Id = "acme" }], ct)).ConfigureAwait(false);
        await WithinAsync(async () => (await peer.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue).ConfigureAwait(false);

        await AdministerAsync((tenants, ct) => tenants.UpsertAsync([new TenantInfo { Id = "acme", IsEnabled = false }], ct)).ConfigureAwait(false);
        await WithinAsync(async () => !(await peer.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().ValueOrThrow().IsEnabled).ConfigureAwait(false);

        await AdministerAsync((tenants, ct) => tenants.RemoveAsync(["acme"], ct)).ConfigureAwait(false);
        await WithinAsync(async () => !(await peer.FindAsync("acme", CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().HasValue).ConfigureAwait(false);
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
