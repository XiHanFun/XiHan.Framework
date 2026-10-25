// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Auditing.Abstractions.Pipeline;
using XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;
using XiHan.Framework.Auditing.Abstractions.Trail;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Abstractions.Tests;

/// <summary>
/// 审计中间件经完整管道的行为：选择规则、显式动作、主体与租户、失败与异常、接收器故障、选项解析。
/// </summary>
public sealed class AuditMiddlewareTests
{
    /// <summary>
    /// 主体 alice 在租户 acme 的 baggage。
    /// </summary>
    private static readonly Dictionary<string, string> _alice = new(StringComparer.Ordinal) { [BaggageKeys.Principal] = "alice", [BaggageKeys.Tenant] = "acme" };

    /// <summary>
    /// 启动装配了记录接收器与订单操作的宿主，可指定 <c>XiHan:Auditing</c> 的两个选项。
    /// </summary>
    private static Task<TestHost> StartAsync(string? selection = null, string? includeSuccesses = null)
    {
        var configuration = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (selection is not null)
        {
            configuration["XiHan:Auditing:Selection"] = selection;
        }

        if (includeSuccesses is not null)
        {
            configuration["XiHan:Auditing:IncludeSuccesses"] = includeSuccesses;
        }

        return TestHost.StartAsync(static b => b.UseFeature<RecordingAuditingFeature>().UseFeature<OrdersFeature>(), configuration);
    }

    [Fact]
    public async Task Protected_RecordsOnlyOperationsWithPermission_AndExplicitActions()
    {
        await using var host = await StartAsync();
        var sink = host.Services.GetRequiredService<RecordingAuditSink>();
        Assert.Contains(nameof(AuditMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);

        Assert.True((await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(12.5m), _alice)).IsSuccess);
        Assert.True((await host.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ListOrders.Id, new(), _alice)).IsSuccess);
        Assert.True((await host.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ExportOrders.Id, new(), _alice)).IsSuccess);
        // 非操作调用没有动作就不记录
        Assert.True((await host.InvokeAsync(static _ => ValueTask.CompletedTask)).IsSuccess);

        Assert.Equal([CreateOrder.Id, ExportOrders.AuditAction], sink.Events.Select(static e => e.Action));
        var created = sink.Events[0];
        Assert.True(created.Outcome.IsSuccess);
        Assert.Equal(("alice", "acme", "xrn:global:orders:acme:order/o-1"), (created.PrincipalId, created.TenantId, created.Resource));
        Assert.Equal(12.5m, created.Details?["amount"]);
        Assert.Equal(32, created.TraceId.Length);
        Assert.True(created.Timestamp <= DateTimeOffset.UtcNow);
        Assert.Null(sink.Events[1].PrincipalId == null ? null : sink.Events[1].Resource);
    }

    [Fact]
    public async Task All_RecordsEveryOperation_AndNoneOnlyExplicit()
    {
        await using var all = await StartAsync(selection: "all");
        await all.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ListOrders.Id, new());
        await all.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(1));
        Assert.Equal([ListOrders.Id, CreateOrder.Id], all.Services.GetRequiredService<RecordingAuditSink>().Events.Select(static e => e.Action));

        await using var none = await StartAsync(selection: "None");
        await none.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(1));
        await none.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ExportOrders.Id, new());
        Assert.Equal([ExportOrders.AuditAction], none.Services.GetRequiredService<RecordingAuditSink>().Events.Select(static e => e.Action));
    }

    [Fact]
    public async Task Failures_AndExceptions_AreRecordedAsFailures_AndSuccessesCanBeSkipped()
    {
        await using var host = await StartAsync(includeSuccesses: "false");
        var sink = host.Services.GetRequiredService<RecordingAuditSink>();

        Assert.True((await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(1))).IsSuccess);
        Assert.Empty(sink.Events);

        var rejected = await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(-1));
        Assert.Equal("ORDERS_VALIDATION_001", rejected.Error?.Code);
        var crashed = await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(999));
        Assert.Equal(KernelErrors.Unexpected, crashed.Error?.Code);

        Assert.Equal(["ORDERS_VALIDATION_001", KernelErrors.Unexpected], sink.Events.Select(static e => e.Outcome.Error?.Code));
        Assert.All(sink.Events, static e => Assert.Equal(CreateOrder.Id, e.Action));
        Assert.Equal(-1m, sink.Events[0].Details?["amount"]);
    }

    [Fact]
    public async Task BrokenSink_DoesNotFailTheInvocation()
    {
        await using var host = await StartAsync();
        var sink = host.Services.GetRequiredService<RecordingAuditSink>();
        sink.Throw = true;
        Assert.True((await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(1))).IsSuccess);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void Options_AndQuery_Validate()
    {
        var options = AuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Auditing:Selection"] = "none",
            ["XiHan:Auditing:IncludeSuccesses"] = "false",
        }).Build());
        Assert.Equal((AuditSelection.None, false), (options.Selection, options.IncludeSuccesses));
        Assert.Throws<ArgumentException>(static () => AuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Auditing:Selection"] = "some" }).Build()));
        Assert.Throws<ArgumentException>(static () => AuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Auditing:IncludeSuccesses"] = "maybe" }).Build()));

        Assert.True(new AuditQuery().Validate().IsSuccess);
        Assert.Equal(AuditingErrors.QueryInvalid, new AuditQuery { PageSize = 0 }.Validate().Error?.Code);
        Assert.Equal(AuditingErrors.QueryInvalid, new AuditQuery { PageSize = AuditQuery.MaxPageSize + 1 }.Validate().Error?.Code);
        Assert.Equal(AuditingErrors.QueryInvalid, new AuditQuery { From = DateTimeOffset.UnixEpoch.AddDays(1), To = DateTimeOffset.UnixEpoch }.Validate().Error?.Code);

        var auditEvent = new AuditEvent(DateTimeOffset.UnixEpoch, "orders.create", Result.Success(), "trace") { PrincipalId = "alice" };
        Assert.True(new AuditQuery { Action = "orders.create", PrincipalId = "alice", Succeeded = true, From = DateTimeOffset.UnixEpoch, To = DateTimeOffset.UnixEpoch }.Matches(auditEvent));
        Assert.False(new AuditQuery { TenantId = "acme" }.Matches(auditEvent));
        Assert.False(new AuditQuery { Succeeded = false }.Matches(auditEvent));

        var context = App.CreateContext(new ServiceCollection().BuildServiceProvider(), CancellationToken.None);
        Assert.Throws<ArgumentException>(() => context.SetAuditAction(" "));
        Assert.Null(context.GetAuditDetails());
        context.AddAuditDetail("a", 1);
        context.AddAuditDetail("a", 2);
        Assert.Equal(2, context.GetAuditDetails()?["a"]);
    }
}
