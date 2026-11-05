// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Database.Grants;
using XiHan.Framework.Security.Database.Tests.Fixtures;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Database.Tests;

/// <summary>
/// 配了从库时授予从主库读（ADR-0122）
/// </summary>
/// <remarks>
/// 两个 SQLite 文件代替一主一从，从库不复制，等于落后：撤销还没传过来。读从库的话，撤销要多等复制延迟，读回来的旧值还要再缓存一轮
/// </remarks>
public sealed class ReplicaGrantReadTests
{
    [Fact]
    public async Task RevokedGrant_IsGone_WhileTheReplicaStillHasIt()
    {
        // 主库的宿主先停：它的探针还在读从库文件
        await using var replica = new SqliteDatabase();
        await using var primary = new SqliteDatabase();
        var editor = GrantSubject.ForRole("editor");
        await GrantAsync(await replica.StartHostAsync(), editor);

        var host = await primary.StartReplicatedHostAsync(replica);

        var store = host.Services.GetRequiredService<IPermissionGrantStore>();
        Assert.Empty((await store.GetGrantsAsync(editor, null, CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task NewGrant_IsVisible_BeforeTheReplicaHasIt()
    {
        await using var replica = new SqliteDatabase();
        await using var primary = new SqliteDatabase();
        var editor = GrantSubject.ForRole("editor");
        await replica.StartHostAsync();

        var host = await primary.StartReplicatedHostAsync(replica);
        await GrantAsync(host, editor);

        var store = host.Services.GetRequiredService<IPermissionGrantStore>();
        Assert.Equal(["orders.write"], (await store.GetGrantsAsync(editor, null, CancellationToken.None)).ValueOrThrow());
    }

    /// <summary>
    /// 在一次调用里写入授予
    /// </summary>
    private static async Task GrantAsync(TestHost host, GrantSubject subject)
    {
        var granted = await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<PermissionGrantRecordStore>().GrantAsync([new PermissionGrant(subject, "orders.write")], context.CancellationToken);
            Assert.All(results, static r => Assert.True(r.Outcome.IsSuccess));
        });
        granted.ThrowIfFailure();
    }
}
