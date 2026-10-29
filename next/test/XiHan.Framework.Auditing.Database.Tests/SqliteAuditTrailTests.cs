// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Auditing.Database.Tests.Fixtures;
using XiHan.Framework.Auditing.Database.Trail;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Auditing.Database.Tests;

/// <summary>
/// 对着真实数据库（SQLite，<c>Persistence.SqlSugar</c>）。
/// </summary>
/// <remarks>
/// 实体映射、<see cref="DateTimeOffset"/> 按 UTC 往返、各过滤项与续页都是 SQL 条件，停止时刷完的事件另一个宿主能读到。
/// </remarks>
public sealed class SqliteAuditTrailTests
{
    /// <summary>
    /// 基准时刻。
    /// </summary>
    private static readonly DateTimeOffset _epoch = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuditRecord_DeclaresColumnLengths_WithLongTextForMessagesAndJson()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        await using var scope = host.Services.CreateAsyncScope();
        var columns = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>().EntityMaintenance.GetEntityInfo<AuditRecord>().Columns;
        EntityColumnInfo Column(string name) => columns.Single(c => c.PropertyName == name);

        // 没有长度的字符串列在 PostgreSQL 会建成 varchar(255)，一条带字段级错误的审计就超长、整批丢弃：文案与 JSON 是长文本，其余列有明确长度
        foreach (var name in new[] { nameof(AuditRecord.ErrorMessage), nameof(AuditRecord.ErrorDetailsJson), nameof(AuditRecord.DetailsJson) })
        {
            Assert.Equal(StaticConfig.CodeFirst_BigString, Column(name).DataType);
        }

        Assert.Equal((256, 64, 256, 64, 1024, 256, 128), (
            Column(nameof(AuditRecord.Action)).Length,
            Column(nameof(AuditRecord.ErrorCode)).Length,
            Column(nameof(AuditRecord.ErrorMessageKey)).Length,
            Column(nameof(AuditRecord.TraceId)).Length,
            Column(nameof(AuditRecord.Resource)).Length,
            Column(nameof(AuditRecord.PrincipalId)).Length,
            Column(nameof(AuditRecord.TenantId)).Length));
        Assert.All(columns.Where(static c => c.PropertyInfo.PropertyType == typeof(string)), static c => Assert.True(c.Length > 0 || !string.IsNullOrEmpty(c.DataType), $"{c.PropertyName} has no length"));
    }

    [Fact]
    public async Task Query_FiltersAndPages_OnSqlite()
    {
        await using var database = new SqliteDatabase();
        var host = await database.StartHostAsync();
        var trail = host.Services.GetRequiredService<DatabaseAuditTrail>();
        var trace = Guid.NewGuid().ToString("N");
        var other = Guid.NewGuid().ToString("N");
        await trail.WriteAsync(Event(trace, 0, "orders.create", "alice", "acme", details: new Dictionary<string, object?> { ["amount"] = "12.50", ["rows"] = 3, ["when"] = _epoch }), CancellationToken.None);
        await trail.WriteAsync(Event(trace, 1, "orders.delete", "bob", "acme", failure: new Error("ORDERS_DENIED_001", "orders.denied", MessageArguments.Of(("id", "o-1")))), CancellationToken.None);
        await trail.WriteAsync(Event(trace, 2, "orders.create", "alice", "zeta"), CancellationToken.None);
        await trail.WriteAsync(Event(other, 3, "orders.export", "alice", "acme"), CancellationToken.None);
        await trail.WriteAsync(Event(trace, 4, "orders.create", "carol", "acme"), CancellationToken.None);
        await trail.FlushAsync(CancellationToken.None);
        Assert.Equal((5L, 0L), (trail.PersistedCount, trail.FailedCount));

        async Task<AuditPage> QueryAsync(AuditQuery query) => (await trail.QueryAsync(query, CancellationToken.None)).ValueOrThrow();

        // 时间范围含边界，时刻按 UTC 往返
        var byTime = await QueryAsync(new AuditQuery { From = _epoch.AddMinutes(1), To = _epoch.AddMinutes(2) });
        Assert.Equal(["orders.delete", "orders.create"], byTime.Events.Select(static e => e.Action));
        Assert.Equal(_epoch.AddMinutes(1), byTime.Events[0].Timestamp);
        // 动作、主体、租户、trace-id、成败各是一个 SQL 条件
        Assert.Equal(["alice", "alice", "carol"], (await QueryAsync(new AuditQuery { Action = "orders.create" })).Events.Select(static e => e.PrincipalId));
        Assert.Equal(["orders.create", "orders.create", "orders.export"], (await QueryAsync(new AuditQuery { PrincipalId = "alice" })).Events.Select(static e => e.Action));
        Assert.Equal(["alice", "bob", "alice", "carol"], (await QueryAsync(new AuditQuery { TenantId = "acme" })).Events.Select(static e => e.PrincipalId));
        Assert.Equal(4, (await QueryAsync(new AuditQuery { TraceId = trace })).Events.Count);
        Assert.Single((await QueryAsync(new AuditQuery { TraceId = other })).Events);
        Assert.Equal(["orders.delete"], (await QueryAsync(new AuditQuery { Succeeded = false })).Events.Select(static e => e.Action));
        Assert.Equal(["bob", "carol"], (await QueryAsync(new AuditQuery { TraceId = trace, TenantId = "acme", From = _epoch.AddMinutes(1) })).Events.Select(static e => e.PrincipalId));

        // 每页 2 条：续页令牌是序号；4 条正好两页，第二页已知没有下一页所以没有令牌
        var first = await QueryAsync(new AuditQuery { TraceId = trace, PageSize = 2 });
        Assert.Equal(["alice", "bob"], first.Events.Select(static e => e.PrincipalId));
        Assert.NotNull(first.ContinuationToken);
        var second = await QueryAsync(new AuditQuery { TraceId = trace, PageSize = 2, ContinuationToken = first.ContinuationToken });
        Assert.Equal(["alice", "carol"], second.Events.Select(static e => e.PrincipalId));
        Assert.Null(second.ContinuationToken);
        // 页大小 3 时第二页只有 1 条，同样没有令牌
        var tail = await QueryAsync(new AuditQuery { TraceId = trace, PageSize = 3, ContinuationToken = (await QueryAsync(new AuditQuery { TraceId = trace, PageSize = 3 })).ContinuationToken });
        Assert.Equal(["carol"], tail.Events.Select(static e => e.PrincipalId));
        Assert.Null(tail.ContinuationToken);

        // 细节与错误往返
        var created = first.Events[0];
        Assert.Equal("12.50", created.Details?["amount"]?.ToString());
        Assert.Equal(3, Assert.IsType<JsonElement>(created.Details?["rows"]).GetInt32());
        Assert.Equal(_epoch, Assert.IsType<JsonElement>(created.Details?["when"]).GetDateTimeOffset());
        var denied = first.Events[1].Outcome.Error!;
        Assert.Equal(("ORDERS_DENIED_001", "orders.denied", "o-1"), (denied.Code, denied.MessageKey, denied.Details?["id"]?.ToString()));
        Assert.Null(first.Events[1].Details);
    }

    [Fact]
    public async Task Stop_FlushesTheRemainingEvents_AndAnotherHostReadsThem()
    {
        await using var database = new SqliteDatabase();
        var writer = await database.StartHostAsync([new("XiHan:Auditing:Database:BatchSize", "100"), new("XiHan:Auditing:Database:FlushIntervalSeconds", "3600")]);
        var trail = writer.Services.GetRequiredService<DatabaseAuditTrail>();
        var trace = Guid.NewGuid().ToString("N");
        await trail.WriteAsync(Event(trace, 0, "a0", "alice", "acme"), CancellationToken.None);
        await trail.WriteAsync(Event(trace, 1, "a1", "alice", "acme"), CancellationToken.None);
        await trail.WriteAsync(Event(trace, 2, "a2", "alice", "acme"), CancellationToken.None);
        Assert.Equal((3, 0L), (trail.PendingCount, trail.PersistedCount));

        // 停止时把队列里的事件刷完；另一个宿主从同一个文件读到它们
        await writer.DisposeAsync();
        Assert.Equal((0, 3L), (trail.PendingCount, trail.PersistedCount));
        var reader = await database.StartHostAsync();
        var page = (await reader.Services.GetRequiredService<IAuditTrail>().QueryAsync(new AuditQuery { TraceId = trace }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["a0", "a1", "a2"], page.Events.Select(static e => e.Action));
    }

    /// <summary>
    /// 基准时刻起第 <paramref name="minute"/> 分钟的事件。
    /// </summary>
    private static AuditEvent Event(string trace, int minute, string action, string principal, string tenant, Error? failure = null, IReadOnlyDictionary<string, object?>? details = null)
        => new(_epoch.AddMinutes(minute), action, failure is null ? Result.Success() : Result.Failure(failure), trace)
        {
            PrincipalId = principal,
            TenantId = tenant,
            Details = details,
        };
}
