// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions;
using XiHan.Framework.Security.Abstractions.Authentication;
using XiHan.Framework.Security.Abstractions.Permissions;
using XiHan.Framework.Security.Database.Grants;
using XiHan.Framework.Security.Database.Tests.Fixtures;
using XiHan.Framework.Security.InMemory;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Security.Database.Tests;

/// <summary>
/// 授予实体在真实数据库（SQLite，经 <c>Persistence.SqlSugar</c> 建表）上的映射
/// </summary>
/// <remarks>
/// 授予 → 查询（含空字符串租户列）→ 撤销；数据库不可达时整条判定链以 <c>SECURITY_UNAVAILABLE_001</c> 结束
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
            Assert.All(results, static r => Assert.True(r.IsSuccess));
        });
        granted.ThrowIfFailure();

        Assert.Equal(["orders.write"], (await store.GetGrantsAsync(editor, null, CancellationToken.None)).ValueOrThrow());
        // 平台授予不落到租户里（ADR-0197）
        Assert.Empty((await store.GetGrantsAsync(editor, "acme", CancellationToken.None)).ValueOrThrow());
        Assert.Empty((await store.GetGrantsAsync(alice, null, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["reports.export"], (await store.GetGrantsAsync(alice, "acme", CancellationToken.None)).ValueOrThrow());
        Assert.Empty((await store.GetGrantsAsync(alice, "other", CancellationToken.None)).ValueOrThrow());

        // 平台行的租户列是空字符串，能原样写入与读回；创建时间由仓储写入
        var listed = await host.InvokeAsync(async context =>
        {
            var rows = await context.Services.GetRequiredService<IRepository<PermissionGrantRecord, Guid>>().ListAsync(
                EntityQuery.All<PermissionGrantRecord>().OrderBy(static r => r.SubjectKind).OrderBy(static r => r.SubjectId),
                context.CancellationToken);
            Assert.Equal([("role", "editor", "orders.write", PermissionGrantRecord.PlatformTenantId), ("user", "alice", "reports.export", "acme")], rows.Select(static r => (r.SubjectKind, r.SubjectId, r.Permission, r.TenantId)));
            Assert.Null(rows[0].ToGrant().TenantId);
            Assert.All(rows, static r => Assert.NotEqual(default, r.CreatedAt));
        });
        listed.ThrowIfFailure();

        var revoked = await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<PermissionGrantRecordStore>().RevokeAsync(
                [new PermissionGrant(editor, "orders.write"), new PermissionGrant(editor, "orders.write")], context.CancellationToken);
            Assert.True(results[0].IsSuccess);
            Assert.Equal(SecurityErrors.GrantNotFound, results[1].Error?.Code);
        });
        revoked.ThrowIfFailure();

        Assert.Empty((await store.GetGrantsAsync(editor, null, CancellationToken.None)).ValueOrThrow());
        Assert.Equal(["reports.export"], (await store.GetGrantsAsync(alice, "acme", CancellationToken.None)).ValueOrThrow());
    }

    [Fact]
    public async Task UnreachableDatabase_EndsTheCheckAsUnavailable_NotAsPermissionMissing()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync(
            [
                new("XiHan:Security:InMemory:Principals:alice:ApiKey", "dev-alice-key"),
                new("XiHan:Security:InMemory:Principals:alice:Roles", "editor"),
            ],
            static b => b.UseFeature<InMemorySecurityFeature>().UseFeature<OrdersFeature>());
        var store = Assert.IsType<DatabasePermissionGrantStore>(host.Services.GetRequiredService<IPermissionGrantStore>());
        var granted = await host.InvokeAsync(async context =>
        {
            var results = await context.Services.GetRequiredService<PermissionGrantRecordStore>().GrantAsync([new PermissionGrant(GrantSubject.ForRole("editor"), WriteOrderOperation.Permission)], context.CancellationToken);
            Assert.All(results, static r => Assert.True(r.IsSuccess));
        });
        granted.ThrowIfFailure();
        Assert.True((await WriteOrderAsync(host)).IsSuccess);

        // 数据库断开且缓存已失效：存储返回 SECURITY_UNAVAILABLE_001，不是空集合
        database.Unreachable = true;
        await store.InvalidateAllAsync(CancellationToken.None);
        var read = await store.GetGrantsAsync(GrantSubject.ForUser("alice"), null, CancellationToken.None);
        Assert.Equal(SecurityErrors.StoreUnavailable, read.Error?.Code);

        // 整条判定链传递失败：调用以 UNAVAILABLE（HTTP 503）结束，不落到 SECURITY_DENIED_004（403）
        var denied = await WriteOrderAsync(host);
        Assert.Equal(SecurityErrors.StoreUnavailable, denied.Error?.Code);
        Assert.Equal("UNAVAILABLE", denied.Error?.Kind);

        // 失败不进缓存：数据库恢复后下一次判定立即重新查库并放行
        database.Unreachable = false;
        Assert.True((await WriteOrderAsync(host)).IsSuccess);
    }

    /// <summary>
    /// 以 alice 的 ApiKey 经完整管道调用写订单操作
    /// </summary>
    private static async Task<Result<OrderResponse>> WriteOrderAsync(TestHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        context.SetCredential(new AuthenticationCredential(ApiKeyAuthenticationHandler.SchemeName, "dev-alice-key"));
        return await scope.ServiceProvider.GetRequiredService<IOperationInvoker>().InvokeAsync<OrderRequest, OrderResponse>(context, WriteOrderOperation.Id, new OrderRequest("o-1"));
    }
}
