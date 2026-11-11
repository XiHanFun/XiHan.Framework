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
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Abstractions.Tests;

/// <summary>
/// 审计中间件经完整管道的行为
/// </summary>
/// <remarks>
/// 选择规则、显式动作、主体与租户、失败与异常、接收器故障、选项解析
/// </remarks>
public sealed class AuditMiddlewareTests
{
    /// <summary>
    /// 主体 alice 在租户 acme 的 baggage
    /// </summary>
    private static readonly Dictionary<string, string> _alice = new(StringComparer.Ordinal) { [BaggageKeys.Principal] = "alice", [BaggageKeys.Tenant] = "acme" };

    /// <summary>
    /// 启动装配了记录接收器与订单操作的宿主，可指定 <c>XiHan:Auditing</c> 的选项与排除项
    /// </summary>
    private static Task<TestHost> StartAsync(string? selection = null, string? includeSuccesses = null, IReadOnlyList<string>? exclude = null)
    {
        var configuration = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < (exclude?.Count ?? 0); i++)
        {
            configuration[$"XiHan:Auditing:Exclude:{i}"] = exclude![i];
        }

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
    public async Task Protected_RecordsOperationsRequiringAuthorization_AndExplicitActions()
    {
        await using var host = await StartAsync();
        var sink = host.Services.GetRequiredService<RecordingAuditSink>();
        Assert.Contains(nameof(AuditMiddleware), host.App.DescribePipeline(), StringComparison.Ordinal);

        Assert.True((await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(12.5m), _alice)).IsSuccess);
        Assert.True((await host.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ListOrders.Id, new(), _alice)).IsSuccess);
        // 只用策略保护（没有权限码）的操作同样需要授权，同样记录
        Assert.True((await host.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ApproveOrders.Id, new(), _alice)).IsSuccess);
        Assert.True((await host.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ExportOrders.Id, new(), _alice)).IsSuccess);
        // 非操作调用没有动作就不记录
        Assert.True((await host.InvokeAsync(static _ => ValueTask.CompletedTask)).IsSuccess);

        Assert.Equal([CreateOrder.Id, ApproveOrders.Id, ExportOrders.AuditAction], sink.Events.Select(static e => e.Action));
        var created = sink.Events[0];
        Assert.True(created.Outcome.IsSuccess);
        Assert.Equal(("alice", "acme", "xrn:global:orders:acme:order/o-1"), (created.PrincipalId, created.TenantId, created.Resource));
        Assert.Equal(12.5m, created.Details?["amount"]);
        Assert.Equal(32, created.TraceId.Length);
        Assert.True(created.Timestamp <= DateTimeOffset.UtcNow);
        Assert.Null(sink.Events[2].PrincipalId == null ? null : sink.Events[2].Resource);
    }

    [Fact]
    public async Task PrincipalAndTenant_ComeFromSecurityContext_AndFallBackToBaggage()
    {
        await using var host = await StartAsync();
        var sink = host.Services.GetRequiredService<RecordingAuditSink>();

        // 认证后的主体与解析后的租户在安全上下文里：Baggage 没有也记得到（HTTP + JWT 的路径没有中间件写这两个 Baggage）
        await host.InvokeAsync(static context =>
        {
            context.SetSecurity(new SecurityContext(new Principal("alice", "jwt"), new TenantContext("acme")));
            context.SetAuditAction("manual.touch");
            return ValueTask.CompletedTask;
        });
        // 安全上下文优先于 Baggage
        await host.InvokeAsync(static context =>
        {
            context.SetSecurity(new SecurityContext(new Principal("bob", "jwt"), new TenantContext("globex")));
            context.SetAuditAction("manual.touch");
            return ValueTask.CompletedTask;
        }, baggage: _alice);
        // 匿名主体、没有租户：回退到 Baggage（插件协议、消息派发、命令行写入的）
        await host.InvokeAsync(static context =>
        {
            context.SetAuditAction("manual.touch");
            return ValueTask.CompletedTask;
        }, baggage: _alice);
        // 两边都没有就是空
        await host.InvokeAsync(static context =>
        {
            context.SetAuditAction("manual.touch");
            return ValueTask.CompletedTask;
        });

        Assert.Equal([("alice", "acme"), ("bob", "globex"), ("alice", "acme"), (null, null)], sink.Events.Select(static e => (e.PrincipalId, e.TenantId)));
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
    public async Task Exclude_SkipsSelectedOperationsByIdOrPrefix_ButKeepsExplicitActions()
    {
        // 精确 ID：只去掉 orders.create，同样受保护的 orders.approve 照记
        await using var exact = await StartAsync(exclude: ["orders.create"]);
        await InvokeAllAsync(exact);
        Assert.Equal([ApproveOrders.Id, ExportOrders.AuditAction], exact.Services.GetRequiredService<RecordingAuditSink>().Events.Select(static e => e.Action));

        // 前缀：orders.* 去掉订单资源的全部操作；ExportOrders 显式声明了动作，不受排除影响
        await using var prefix = await StartAsync(selection: "All", exclude: ["orders.*"]);
        await InvokeAllAsync(prefix);
        Assert.Equal([ExportOrders.AuditAction], prefix.Services.GetRequiredService<RecordingAuditSink>().Events.Select(static e => e.Action));

        // 资源名前缀：ord* 同样命中 orders.*；不命中的前缀与不配置时一样
        await using var partial = await StartAsync(selection: "All", exclude: ["ord*"]);
        await InvokeAllAsync(partial);
        Assert.Equal([ExportOrders.AuditAction], partial.Services.GetRequiredService<RecordingAuditSink>().Events.Select(static e => e.Action));

        await using var miss = await StartAsync(selection: "All", exclude: ["orders.creat", "invoices.*", "orders.lists*"]);
        await InvokeAllAsync(miss);
        Assert.Equal([CreateOrder.Id, ListOrders.Id, ApproveOrders.Id, ExportOrders.AuditAction], miss.Services.GetRequiredService<RecordingAuditSink>().Events.Select(static e => e.Action));

        // 依次调用四个订单操作：创建、列表、审批（只用策略保护）、导出（显式动作）
        static async Task InvokeAllAsync(TestHost host)
        {
            Assert.True((await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(1), _alice)).IsSuccess);
            Assert.True((await host.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ListOrders.Id, new(), _alice)).IsSuccess);
            Assert.True((await host.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ApproveOrders.Id, new(), _alice)).IsSuccess);
            Assert.True((await host.InvokeOperationAsync<ListOrdersRequest, OrderResponse>(ExportOrders.Id, new(), _alice)).IsSuccess);
        }
    }

    [Fact]
    public async Task Exclude_NotConfigured_BehavesAsBefore()
    {
        await using var host = await StartAsync();

        Assert.Empty(host.Services.GetRequiredService<AuditingOptions>().Exclude);
        Assert.True((await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(1), _alice)).IsSuccess);
        Assert.Equal([CreateOrder.Id], host.Services.GetRequiredService<RecordingAuditSink>().Events.Select(static e => e.Action));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("*")]
    [InlineData("orders")]
    [InlineData("orders.")]
    [InlineData("Orders.list")]
    [InlineData("orders.*.x")]
    [InlineData("orders*list")]
    [InlineData("orders.list.*")]
    [InlineData("*.list")]
    public async Task Exclude_InvalidEntry_FailsTheAssembly(string entry)
    {
        var configuration = new Dictionary<string, string?>(StringComparer.Ordinal) { ["XiHan:Auditing:Exclude:0"] = "orders.list", ["XiHan:Auditing:Exclude:1"] = entry };

        // 读选项在特性配置时：不合法的一项让装配失败，消息指出配置键与那一项
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => TestHost.StartAsync(static b => b.UseFeature<RecordingAuditingFeature>().UseFeature<OrdersFeature>(), configuration));

        Assert.Contains("'XiHan:Auditing:Exclude:1'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{entry}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exclude_Options_ParseListsAndRejectSingleValuesAndInvalidCode()
    {
        var options = AuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["XiHan:Auditing:Exclude:0"] = "permissions.current",
            ["XiHan:Auditing:Exclude:1"] = "profile.*",
            ["XiHan:Auditing:Exclude:2"] = "my-*",
        }).Build());
        Assert.Equal(["permissions.current", "profile.*", "my-*"], options.Exclude);

        // 写成单个值（环境变量常见）不当作没配
        var single = Assert.Throws<ArgumentException>(static () => AuditingOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["XiHan:Auditing:Exclude"] = "orders.list" }).Build()));
        Assert.Contains("must be a list", single.Message, StringComparison.Ordinal);

        // 代码里构造同样校验
        Assert.Equal(["orders.*"], new AuditingOptions { Exclude = ["orders.*"] }.Exclude);
        Assert.Throws<ArgumentException>(static () => new AuditingOptions { Exclude = ["orders"] });
        Assert.Throws<ArgumentNullException>(static () => new AuditingOptions { Exclude = null! });
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

        var auditEvent = new AuditEvent
        {
            Timestamp = DateTimeOffset.UnixEpoch,
            Action = "orders.create",
            Outcome = Result.Success(),
            TraceId = "trace",
            PrincipalId = "alice",
        };
        Assert.True(new AuditQuery { Action = "orders.create", PrincipalId = "alice", Succeeded = true, From = DateTimeOffset.UnixEpoch, To = DateTimeOffset.UnixEpoch }.Matches(auditEvent));
        Assert.False(new AuditQuery { TenantId = "acme" }.Matches(auditEvent));
        Assert.False(new AuditQuery { Succeeded = false }.Matches(auditEvent));

        var context = App.CreateContext(new ServiceCollection().BuildServiceProvider(), CancellationToken.None);
        Assert.Throws<ArgumentException>(() => context.SetAuditAction(" "));
        Assert.False(context.FindAuditDetails().HasValue);
        context.AddAuditDetail("a", 1);
        context.AddAuditDetail("a", 2);
        var snapshot = context.FindAuditDetails().ValueOrThrow();
        Assert.Equal(2, snapshot["a"]);
        context.AddAuditDetail("b", 3);
        Assert.False(snapshot.ContainsKey("b"));
        Assert.IsNotType<Dictionary<string, object?>>(snapshot);
    }
}
