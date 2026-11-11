// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Auditing.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Primitives;
using Xunit;

namespace XiHan.Framework.Auditing.Conformance;

/// <summary>
/// <c>IAuditSink</c> + <see cref="IAuditTrail"/> 的一致性套件
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateFixtureAsync"/>，全部用例随之执行。每个夹具应是空的（或用 trace-id 前缀隔离的）存储
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class AuditTrailConformance
{
    /// <summary>
    /// 基准时刻
    /// </summary>
    private static readonly DateTimeOffset _epoch = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 创建夹具
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次
    /// </remarks>
    protected abstract Task<AuditTrailFixture> CreateFixtureAsync();

    /// <summary>
    /// 用例 1：写入后按写入顺序读回
    /// </summary>
    /// <remarks>
    /// 字段完整往返（失败结果保留错误码，细节按文本比较）
    /// </remarks>
    [Fact]
    public async Task Query_ReturnsEventsInWriteOrder_WithAllFields()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        var trace = Trace();
        await fixture.Sink.WriteAsync(Event(trace, 0, "orders.create", principal: "alice", tenant: "acme", resource: "xrn:global:orders:acme:order/1", details: new Dictionary<string, object?> { ["amount"] = "12.50" }), CancellationToken.None).ConfigureAwait(false);
        await fixture.Sink.WriteAsync(Event(trace, 1, "orders.delete", principal: "bob", tenant: "acme", failure: "ORDERS_DENIED_001"), CancellationToken.None).ConfigureAwait(false);
        await fixture.Sink.WriteAsync(Event(trace, 2, "orders.create", principal: "alice", tenant: "zeta"), CancellationToken.None).ConfigureAwait(false);

        var page = (await fixture.Trail.QueryAsync(new AuditQuery { TraceId = trace }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Null(page.ContinuationToken);
        Assert.Equal(["orders.create", "orders.delete", "orders.create"], page.Events.Select(static e => e.Action));
        var first = page.Events[0];
        Assert.Equal((_epoch, "alice", "acme", "xrn:global:orders:acme:order/1", trace), (first.Timestamp, first.PrincipalId, first.TenantId, first.Resource, first.TraceId));
        Assert.True(first.Outcome.IsSuccess);
        Assert.Equal("12.50", first.Details?["amount"]?.ToString());
        Assert.Equal("ORDERS_DENIED_001", page.Events[1].Outcome.Error?.Code);
        Assert.Null(page.Events[1].Resource);
        Assert.Null(page.Events[1].Details);
    }

    /// <summary>
    /// 用例 2：各过滤项取交集
    /// </summary>
    /// <remarks>
    /// 没有匹配时返回空页
    /// </remarks>
    [Fact]
    public async Task Query_FiltersIntersect()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        var trace = Trace();
        await fixture.Sink.WriteAsync(Event(trace, 0, "orders.create", principal: "alice", tenant: "acme"), CancellationToken.None).ConfigureAwait(false);
        await fixture.Sink.WriteAsync(Event(trace, 1, "orders.create", principal: "bob", tenant: "acme", failure: "ORDERS_CONFLICT_001"), CancellationToken.None).ConfigureAwait(false);
        await fixture.Sink.WriteAsync(Event(trace, 2, "orders.delete", principal: "alice", tenant: "zeta"), CancellationToken.None).ConfigureAwait(false);

        async Task<IReadOnlyList<string?>> PrincipalsAsync(AuditQuery query)
            => (await fixture.Trail.QueryAsync(query with { TraceId = trace }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow().Events.Select(static e => e.PrincipalId).ToList();

        Assert.Equal(["alice", "bob"], await PrincipalsAsync(new AuditQuery { Action = "orders.create" }).ConfigureAwait(false));
        Assert.Equal(["alice", "alice"], await PrincipalsAsync(new AuditQuery { PrincipalId = "alice" }).ConfigureAwait(false));
        Assert.Equal(["alice", "bob"], await PrincipalsAsync(new AuditQuery { TenantId = "acme" }).ConfigureAwait(false));
        Assert.Equal(["bob"], await PrincipalsAsync(new AuditQuery { Succeeded = false }).ConfigureAwait(false));
        Assert.Equal(["alice", "alice"], await PrincipalsAsync(new AuditQuery { Succeeded = true }).ConfigureAwait(false));
        Assert.Equal(["bob", "alice"], await PrincipalsAsync(new AuditQuery { From = _epoch.AddMinutes(1) }).ConfigureAwait(false));
        Assert.Equal(["alice", "bob"], await PrincipalsAsync(new AuditQuery { To = _epoch.AddMinutes(1) }).ConfigureAwait(false));
        Assert.Equal(["bob"], await PrincipalsAsync(new AuditQuery { From = _epoch.AddMinutes(1), To = _epoch.AddMinutes(1), TenantId = "acme" }).ConfigureAwait(false));
        Assert.Empty(await PrincipalsAsync(new AuditQuery { Action = "orders.delete", TenantId = "acme" }).ConfigureAwait(false));
    }

    /// <summary>
    /// 用例 3：分页与续页令牌
    /// </summary>
    /// <remarks>
    /// 最后一页没有令牌
    /// </remarks>
    [Fact]
    public async Task Query_PagesWithContinuationToken()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        var trace = Trace();
        for (var i = 0; i < 5; i++)
        {
            await fixture.Sink.WriteAsync(Event(trace, i, "orders.create", principal: "p" + i), CancellationToken.None).ConfigureAwait(false);
        }

        var seen = new List<string?>();
        string? token = null;
        var pages = 0;
        do
        {
            var page = (await fixture.Trail.QueryAsync(new AuditQuery { TraceId = trace, PageSize = 2, ContinuationToken = token }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
            Assert.True(page.Events.Count <= 2);
            seen.AddRange(page.Events.Select(static e => e.PrincipalId));
            token = page.ContinuationToken;
            pages++;
        }
        while (token is not null && pages < 10);

        Assert.Equal(["p0", "p1", "p2", "p3", "p4"], seen);
        Assert.Equal(3, pages);
    }

    /// <summary>
    /// 用例 4：页大小越界、起止颠倒与不可识别的续页令牌都是 VALIDATION
    /// </summary>
    [Fact]
    public async Task Query_InvalidQueries_AreValidationFailures()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        foreach (var query in new[]
        {
            new AuditQuery { PageSize = 0 },
            new AuditQuery { PageSize = AuditQuery.MaxPageSize + 1 },
            new AuditQuery { From = _epoch.AddMinutes(1), To = _epoch },
            new AuditQuery { ContinuationToken = "not-a-token-" + Guid.NewGuid().ToString("N") },
        })
        {
            Assert.Equal(AuditingErrors.QueryInvalid, (await fixture.Trail.QueryAsync(query, CancellationToken.None).ConfigureAwait(false)).Error?.Code);
        }
    }

    /// <summary>
    /// 用例 5：接收器不抛出，取消的令牌也不例外
    /// </summary>
    /// <remarks>
    /// 契约（ADR-0147）：取消时实现可以放弃这条事件，但不把故障交给调用方；令牌没取消的写入照常留下
    /// </remarks>
    [Fact]
    public async Task Write_NeverThrows_EvenWithACancelledToken()
    {
        var fixture = await CreateFixtureAsync().ConfigureAwait(false);
        var trace = Trace();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(false);

        var exception = await Record.ExceptionAsync(async () => await fixture.Sink.WriteAsync(Event(trace, 0, "orders.cancelled"), cancelled.Token).ConfigureAwait(false)).ConfigureAwait(false);
        await fixture.Sink.WriteAsync(Event(trace, 1, "orders.kept"), CancellationToken.None).ConfigureAwait(false);

        Assert.Null(exception);
        var page = (await fixture.Trail.QueryAsync(new AuditQuery { TraceId = trace }, CancellationToken.None).ConfigureAwait(false)).ValueOrThrow();
        Assert.Contains(page.Events, static e => e.Action == "orders.kept");
    }

    /// <summary>
    /// 套件内唯一的 trace-id（32 位十六进制）
    /// </summary>
    private static string Trace() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// 基准时刻起第 <paramref name="minute"/> 分钟的事件
    /// </summary>
    private static AuditEvent Event(string trace, int minute, string action, string? principal = null, string? tenant = null, string? resource = null, string? failure = null, IReadOnlyDictionary<string, object?>? details = null)
        => new()
        {
            Timestamp = _epoch.AddMinutes(minute),
            Action = action,
            Outcome = failure is null ? Result.Success() : new Error(failure, "conformance.failed"),
            TraceId = trace,
            PrincipalId = principal,
            TenantId = tenant,
            Resource = resource,
            Details = details,
        };
}
