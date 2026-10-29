// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Auditing.Abstractions;
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Auditing.Database.Tests.Fixtures;
using XiHan.Framework.Auditing.Database.Trail;
using XiHan.Framework.Auditing.InMemory;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Localization.Abstractions;
using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Database.Tests;

/// <summary>
/// 数据库 Provider 特有的行为。
/// </summary>
/// <remarks>
/// 顶替内存实现、经中间件进队列与刷写、错误往返与翻译、队列满丢弃、批满与定时刷写、停止时刷完、落库失败只记日志、选项解析。
/// </remarks>
public sealed class DatabaseAuditingTests
{
    /// <summary>
    /// 固定的起始时刻。
    /// </summary>
    private static readonly DateTimeOffset _start = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 主体 alice 在租户 acme 的 baggage。
    /// </summary>
    private static readonly Dictionary<string, string> _alice = new(StringComparer.Ordinal) { [BaggageKeys.Principal] = "alice", [BaggageKeys.Tenant] = "acme" };

    /// <summary>
    /// 业务目录：失败结果的消息键有中文译文。
    /// </summary>
    private static readonly MessageCatalog _orders = new("orders", new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string> { ["orders.amount.negative"] = "The amount '{amount}' must not be negative." },
        ["zh-CN"] = new Dictionary<string, string> { ["orders.amount.negative"] = "金额 '{amount}' 不能为负。" },
    });

    [Fact]
    public async Task Feature_ReplacesInMemory_AndRecordsProtectedOperationThroughTheQueue()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock);
        var trail = host.Services.GetRequiredService<DatabaseAuditTrail>();
        // 显式 Provider 顶替默认的内存审计
        Assert.Same(trail, host.Services.GetRequiredService<IAuditSink>());
        Assert.Same(trail, host.Services.GetRequiredService<IAuditTrail>());
        Assert.Equal(["auditing.trail"], host.App.Graph["xihan.auditing.database"].SelectedCapabilities);
        Assert.Empty(host.App.Graph["xihan.auditing.inmemory"].SelectedCapabilities);
        Assert.Contains(nameof(AuditMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);

        Assert.True((await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(12.5m), _alice)).IsSuccess);
        // 调用结束时事件只进了队列；假时钟不走、不足一批，落库由用例驱动
        Assert.Equal((1, 0L), (trail.PendingCount, trail.PersistedCount));
        await trail.FlushAsync(CancellationToken.None);
        Assert.Equal((0, 1L, 0L, 0L), (trail.PendingCount, trail.PersistedCount, trail.FailedCount, trail.DroppedCount));

        var page = (await trail.QueryAsync(new AuditQuery { Action = CreateOrder.Id, PrincipalId = "alice" }, CancellationToken.None)).ValueOrThrow();
        var recorded = Assert.Single(page.Events);
        Assert.Null(page.ContinuationToken);
        Assert.Equal(("alice", "acme", "xrn:global:orders:acme:order/o-1", _start), (recorded.PrincipalId, recorded.TenantId, recorded.Resource, recorded.Timestamp));
        Assert.True(recorded.Outcome.IsSuccess);
        Assert.Equal(32, recorded.TraceId.Length);
        // 细节读回是 JsonElement
        Assert.Equal(12.5m, Assert.IsType<JsonElement>(recorded.Details?["amount"]).GetDecimal());
    }

    [Fact]
    public async Task Errors_RoundTrip_KeyedOnesTranslate_LiteralOnesKeepTheirText()
    {
        await using var host = await StartAsync(new FakeTimeProvider(_start));
        var trail = host.Services.GetRequiredService<DatabaseAuditTrail>();
        var keyed = Error.Validation("ORDERS_VALIDATION_001", "orders.amount.negative", MessageArguments.Of(("amount", -1)));
        var literal = Error.Literal("ORDERS_UNEXPECTED_001", "The printer caught fire.", MessageArguments.Of(("printer", "p-7")));
        await host.InvokeAsync(ctx =>
        {
            ctx.SetAuditAction("orders.create");
            ctx.SetOutcome(Result.Failure(keyed));
            return ValueTask.CompletedTask;
        }, baggage: _alice);
        await host.InvokeAsync(ctx =>
        {
            ctx.SetAuditAction("orders.print");
            ctx.SetOutcome(Result.Failure(literal));
            return ValueTask.CompletedTask;
        }, baggage: _alice);
        await trail.FlushAsync(CancellationToken.None);

        var events = (await trail.QueryAsync(new AuditQuery { Succeeded = false, PrincipalId = "alice" }, CancellationToken.None)).ValueOrThrow().Events;
        Assert.Equal(["orders.create", "orders.print"], events.Select(static e => e.Action));
        var localizer = host.Services.GetRequiredService<IMessageLocalizer>();
        // 带键的错误按键与细节还原，读取方按区域翻译
        var restored = events[0].Outcome.Error!;
        Assert.Equal(("ORDERS_VALIDATION_001", "orders.amount.negative"), (restored.Code, restored.MessageKey));
        Assert.Equal("-1", restored.Details?["amount"]?.ToString());
        Assert.Equal("金额 '-1' 不能为负。", localizer.Localize(restored, "zh-CN"));
        Assert.Equal("The amount '-1' must not be negative.", localizer.Localize(restored, "en-US"));
        // 字面错误原样保留文本与细节，不翻译
        var text = events[1].Outcome.Error!;
        Assert.Null(text.MessageKey);
        Assert.Equal(("ORDERS_UNEXPECTED_001", "The printer caught fire."), (text.Code, text.Message));
        Assert.Equal("p-7", text.Details?["printer"]?.ToString());
        Assert.Equal("The printer caught fire.", localizer.Localize(text, "zh-CN"));
    }

    [Fact]
    public async Task QueueFull_DropsNewEvents_AndCounts()
    {
        await using var host = await StartAsync(new FakeTimeProvider(_start), configuration: [new("XiHan:Auditing:Database:QueueCapacity", "2"), new("XiHan:Auditing:Database:BatchSize", "100")]);
        var trail = host.Services.GetRequiredService<DatabaseAuditTrail>();
        await trail.WriteAsync(Event("a1"), CancellationToken.None);
        await trail.WriteAsync(Event("a2"), CancellationToken.None);
        await trail.WriteAsync(Event("a3"), CancellationToken.None);
        // 第三条进不了队列：丢弃并计数，写入本身不抛出
        Assert.Equal((2, 1L), (trail.PendingCount, trail.DroppedCount));

        await trail.FlushAsync(CancellationToken.None);
        Assert.Equal((0, 2L, 1L), (trail.PendingCount, trail.PersistedCount, trail.DroppedCount));
        var page = (await trail.QueryAsync(new AuditQuery { TraceId = "t" }, CancellationToken.None)).ValueOrThrow();
        Assert.Equal(["a1", "a2"], page.Events.Select(static e => e.Action));
    }

    [Fact]
    public async Task BatchSize_TriggersFlush_WithoutWaitingForTheTimer()
    {
        await using var host = await StartAsync(new FakeTimeProvider(_start), configuration: [new("XiHan:Auditing:Database:BatchSize", "2")]);
        var trail = host.Services.GetRequiredService<DatabaseAuditTrail>();
        await trail.WriteAsync(Event("a1"), CancellationToken.None);
        Assert.Equal((1, 0L), (trail.PendingCount, trail.PersistedCount));

        // 攒够一批就唤醒后台循环，假时钟不需要走
        await trail.WriteAsync(Event("a2"), CancellationToken.None);
        await WaitUntilAsync(() => trail.PersistedCount == 2);
        Assert.Equal(0, trail.PendingCount);
    }

    [Fact]
    public async Task FlushInterval_FlushesOnTheClock()
    {
        var clock = new FakeTimeProvider(_start);
        await using var host = await StartAsync(clock, configuration: [new("XiHan:Auditing:Database:BatchSize", "100"), new("XiHan:Auditing:Database:FlushIntervalSeconds", "2")]);
        var trail = host.Services.GetRequiredService<DatabaseAuditTrail>();
        await trail.WriteAsync(Event("a1"), CancellationToken.None);
        // 时钟不走就不刷写
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal((1, 0L), (trail.PendingCount, trail.PersistedCount));

        clock.Advance(TimeSpan.FromSeconds(1));
        await WaitUntilAsync(() => trail.PersistedCount == 1);
        Assert.Equal(0, trail.PendingCount);
        // 下一个周期照常
        await trail.WriteAsync(Event("a2"), CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(2));
        await WaitUntilAsync(() => trail.PersistedCount == 2);
    }

    [Fact]
    public async Task Stop_FlushesTheRemainingEvents_AndDropsLaterOnes()
    {
        var host = await StartAsync(new FakeTimeProvider(_start), configuration: [new("XiHan:Auditing:Database:BatchSize", "100")]);
        var trail = host.Services.GetRequiredService<DatabaseAuditTrail>();
        await trail.WriteAsync(Event("a1"), CancellationToken.None);
        await trail.WriteAsync(Event("a2"), CancellationToken.None);
        await trail.WriteAsync(Event("a3"), CancellationToken.None);
        Assert.Equal((3, 0L), (trail.PendingCount, trail.PersistedCount));

        await host.DisposeAsync();
        Assert.Equal((0, 3L, 0L), (trail.PendingCount, trail.PersistedCount, trail.FailedCount));
        // 停止后到达的事件不再接收，按丢弃计数
        await trail.WriteAsync(Event("late"), CancellationToken.None);
        Assert.Equal((0, 1L), (trail.PendingCount, trail.DroppedCount));
    }

    [Fact]
    public async Task PersistenceFailure_OnlyLogs_AndDoesNotAffectTheCall()
    {
        await using var host = await StartAsync(new FakeTimeProvider(_start), static services => services.AddScoped<IRepository<AuditRecord, Guid>, ThrowingAuditRepository>());
        var trail = host.Services.GetRequiredService<DatabaseAuditTrail>();
        Assert.True((await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(1m), _alice)).IsSuccess);
        Assert.Equal(1, trail.PendingCount);

        // 刷写不抛出：整批丢弃并计数
        await trail.FlushAsync(CancellationToken.None);
        Assert.Equal((0, 0L, 1L), (trail.PendingCount, trail.PersistedCount, trail.FailedCount));
        // 查询同样不抛出，返回存储不可达
        var failed = await trail.QueryAsync(new AuditQuery(), CancellationToken.None);
        Assert.Equal(AuditingErrors.TrailUnavailable, failed.Error?.Code);
        Assert.Equal($"审计存储不可用：{ThrowingAuditRepository.Message}", host.Services.GetRequiredService<IMessageLocalizer>().Localize(failed.Error!, "zh-CN"));
    }

    [Fact]
    public async Task Query_RejectsUnrecognizedTokens_AndInvalidPages()
    {
        await using var host = await StartAsync(new FakeTimeProvider(_start));
        var trail = host.Services.GetRequiredService<DatabaseAuditTrail>();
        foreach (var query in new[] { new AuditQuery { ContinuationToken = "abc" }, new AuditQuery { ContinuationToken = "-1" }, new AuditQuery { PageSize = 0 } })
        {
            Assert.Equal(AuditingErrors.QueryInvalid, (await trail.QueryAsync(query, CancellationToken.None)).Error?.Code);
        }
    }

    [Fact]
    public void Options_ValidateAndParse()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseAuditingOptions { QueueCapacity = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseAuditingOptions { BatchSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(static () => new DatabaseAuditingOptions { FlushInterval = TimeSpan.Zero });
        var defaults = new DatabaseAuditingOptions();
        Assert.Equal((10_000, 100, TimeSpan.FromSeconds(1), true), (defaults.QueueCapacity, defaults.BatchSize, defaults.FlushInterval, defaults.LogEvents));

        var options = DatabaseAuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Auditing:Database:QueueCapacity"] = "7",
            ["XiHan:Auditing:Database:BatchSize"] = "3",
            ["XiHan:Auditing:Database:FlushIntervalSeconds"] = "0.5",
            ["XiHan:Auditing:Database:LogEvents"] = "false",
        }).Build());
        Assert.Equal((7, 3, TimeSpan.FromMilliseconds(500), false), (options.QueueCapacity, options.BatchSize, options.FlushInterval, options.LogEvents));

        foreach (var (key, value) in new[] { ("QueueCapacity", "-1"), ("BatchSize", "x"), ("FlushIntervalSeconds", "0"), ("LogEvents", "yes") })
        {
            Assert.Throws<ArgumentException>(() => DatabaseAuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [$"XiHan:Auditing:Database:{key}"] = value }).Build()));
        }
    }

    /// <summary>
    /// 启动装配了内存持久化、内存本地化、内存审计、数据库审计与订单操作的宿主。
    /// </summary>
    /// <remarks>
    /// 被顶替。时钟换成假时钟让刷写只由用例驱动，事件日志关闭。
    /// </remarks>
    private static Task<TestHost> StartAsync(TimeProvider clock, Action<IServiceCollection>? services = null, params KeyValuePair<string, string?>[] configuration)
        => TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<InMemoryAuditingFeature>().UseFeature<DatabaseAuditingFeature>().UseFeature<OrdersFeature>(),
            [new("XiHan:Auditing:Database:LogEvents", "false"), .. configuration],
            collection =>
            {
                collection.AddMessageCatalog(_orders);
                collection.AddSingleton(clock);
                services?.Invoke(collection);
            });

    /// <summary>
    /// trace-id 为 <c>t</c> 的成功事件。
    /// </summary>
    private static AuditEvent Event(string action) => new(_start, action, Result.Success(), "t");

    /// <summary>
    /// 轮询直到条件成立，最多 10 秒。
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not become true in time.");
            }

            await Task.Delay(20);
        }
    }
}
