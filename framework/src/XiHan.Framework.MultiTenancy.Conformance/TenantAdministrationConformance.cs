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
/// 继承并实现 <see cref="StartAsync"/> 与 <see cref="AdministerAsync"/>，全部用例随之执行。每个用例先 <see cref="StartAsync"/> 得到一个空的租户表与读它的租户目录，再经 <see cref="AdministerAsync"/> 在一次调用里使用租户管理：持久化的实现要在调用的事务里写入（ADR-0160）
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
}
