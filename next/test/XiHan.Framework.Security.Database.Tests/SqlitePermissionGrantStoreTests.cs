// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Database.Grants;
using XiHan.Framework.Security.Database.Tests.Fixtures;

namespace XiHan.Framework.Security.Database.Tests;

/// <summary>
/// 授予实体在真实数据库（SQLite，经 <c>Persistence.SqlSugar</c> 建表）上的映射。
/// </summary>
/// <remarks>
/// 授予 → 查询（含空字符串租户列）→ 撤销。
/// </remarks>
public sealed class SqlitePermissionGrantStoreTests
{
    [Fact]
    public async Task Grants_RoundTrip_OnSqlite()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var editor = GrantSubject.ForRole("editor");
        var alice = GrantSubject.ForUser("alice");
        var store = host.Services.GetRequiredService<IPermissionGrantStore>();

        var granted = await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<PermissionGrantRecordStore>().GrantAsync(
            [
                new PermissionGrant(editor, "orders.write"),
                new PermissionGrant(alice, "reports.export", "acme"),
                new PermissionGrant(editor, "orders.write"),
            ], context.CancellationToken);
            Assert.All(results, static r => Assert.True(r.Outcome.IsSuccess));
        });
        granted.ThrowIfFailure();

        Assert.Equal(["orders.write"], await store.GetGrantsAsync(editor, null, CancellationToken.None));
        Assert.Equal(["orders.write"], await store.GetGrantsAsync(editor, "acme", CancellationToken.None));
        Assert.Empty(await store.GetGrantsAsync(alice, null, CancellationToken.None));
        Assert.Equal(["reports.export"], await store.GetGrantsAsync(alice, "acme", CancellationToken.None));
        Assert.Empty(await store.GetGrantsAsync(alice, "other", CancellationToken.None));

        // 全局行的租户列是空字符串，能原样写入与读回；创建时间由仓储写入
        var listed = await host.InvokeAsync(async context =>
        {
            var rows = await context.Services.GetRequiredService<PermissionGrantRecordStore>().ListAsync(null, null, 0, 10, context.CancellationToken);
            Assert.Equal([("role", "editor", "orders.write", PermissionGrantRecord.GlobalTenantId), ("user", "alice", "reports.export", "acme")], rows.Select(static r => (r.SubjectKind, r.SubjectId, r.Permission, r.TenantId)));
            Assert.Null(rows[0].ToGrant().TenantId);
            Assert.All(rows, static r => Assert.NotEqual(default, r.CreatedAt));
        });
        listed.ThrowIfFailure();

        var revoked = await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<PermissionGrantRecordStore>().RevokeAsync(
                [new PermissionGrant(editor, "orders.write"), new PermissionGrant(editor, "orders.write")], context.CancellationToken);
            Assert.True(results[0].Outcome.IsSuccess);
            Assert.Equal(SecurityErrors.GrantNotFound, results[1].Outcome.Error?.Code);
        });
        revoked.ThrowIfFailure();

        Assert.Empty(await store.GetGrantsAsync(editor, null, CancellationToken.None));
        Assert.Equal(["reports.export"], await store.GetGrantsAsync(alice, "acme", CancellationToken.None));
    }
}
