// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Caching.InMemory;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Database.Grants;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Database.Tests;

/// <summary>
/// 撤销与授予在提交之后才让缓存失效（ADR-0194）
/// </summary>
/// <remarks>
/// 撤销写在调用方的事务里，提交之前别的调用读到的还是旧行：那段时间里读回来的结果不能留在缓存里，提交之后的判定要看到撤销
/// </remarks>
public sealed class GrantCacheCommitTests
{
    /// <summary>
    /// 被撤销的主体
    /// </summary>
    private static readonly GrantSubject _editor = GrantSubject.ForRole("editor");

    [Fact]
    public async Task ReadBetweenTheRevokeAndItsCommit_DoesNotKeepTheRevokedGrantInTheCache()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<InMemoryCachingFeature>().UseFeature<DatabaseSecurityFeature>());
        var store = host.Services.GetRequiredService<IPermissionGrantStore>();
        (await host.InvokeAsync(static async context => await context.Services.GetRequiredService<PermissionGrantRecordStore>().GrantAsync([new PermissionGrant(_editor, "orders.write")], context.CancellationToken))).ThrowIfFailure();
        Assert.Equal(["orders.write"], (await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());

        (await host.InvokeAsync(async context =>
        {
            (await context.Services.GetRequiredService<PermissionGrantRecordStore>().RevokeAsync([new PermissionGrant(_editor, "orders.write")], context.CancellationToken)).Single().ThrowIfFailure();

            // 撤销还没提交：并发的判定读到旧行，写回缓存
            Assert.Equal(["orders.write"], (await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
        })).ThrowIfFailure();

        // 修之前提交前读回的旧值留在缓存里，到存活时间结束前撤销都不生效
        Assert.Empty((await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task ReadBetweenTheGrantAndItsCommit_DoesNotHideTheNewGrant()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<InMemoryCachingFeature>().UseFeature<DatabaseSecurityFeature>());
        var store = host.Services.GetRequiredService<IPermissionGrantStore>();
        Assert.Empty((await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());

        (await host.InvokeAsync(async context =>
        {
            (await context.Services.GetRequiredService<PermissionGrantRecordStore>().GrantAsync([new PermissionGrant(_editor, "orders.write")], context.CancellationToken)).Single().ThrowIfFailure();
            Assert.Empty((await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
        })).ThrowIfFailure();

        Assert.Equal(["orders.write"], (await store.GetGrantsAsync(_editor, null, CancellationToken.None)).ValueOrThrow());
    }
}
