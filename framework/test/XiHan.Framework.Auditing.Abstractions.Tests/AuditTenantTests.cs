// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Auditing.Abstractions.Tests.Fixtures;
using XiHan.Framework.MultiTenancy.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Auditing.Abstractions.Tests;

/// <summary>
/// 装配了多租户时审计记下的租户（ADR-0191）
/// </summary>
/// <remarks>
/// Tenant 阶段裁定过这次调用的租户，审计只信安全上下文：被拒绝的调用不回退到调用方自报的 Baggage，事件不落进它自报的租户；没有 Tenant 阶段的宿主照旧回退
/// </remarks>
public sealed class AuditTenantTests
{
    [Theory]
    [InlineData("ghost", "MULTITENANCY_NOTFOUND_001")]
    [InlineData("frozen", "MULTITENANCY_DENIED_001")]
    public async Task RejectedTenant_IsNotRecordedAsTheEventTenant(string claimed, string code)
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<RecordingAuditingFeature>().UseFeature<OrdersFeature>().UseFeature<InMemoryMultiTenancyFeature>(),
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["XiHan:MultiTenancy:Tenants:acme:DisplayName"] = "Acme",
                ["XiHan:MultiTenancy:Tenants:frozen:DisplayName"] = "Frozen",
                ["XiHan:MultiTenancy:Tenants:frozen:IsEnabled"] = "false",
            });
        var sink = host.Services.GetRequiredService<RecordingAuditSink>();
        var baggage = new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Principal] = "mallory", [BaggageKeys.Tenant] = claimed };

        var created = await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(1m), baggage);

        Assert.Equal(code, created.Error?.Code);
        var recorded = Assert.Single(sink.Events);
        // 修之前回退到 Baggage，被拒绝的调用记在调用方自报的租户名下；被拒绝的租户仍在结果的错误参数里
        Assert.Null(recorded.TenantId);
        Assert.Equal(claimed, recorded.Outcome.Error?.Details?["tenantId"]);
    }

    [Fact]
    public async Task AcceptedTenant_IsRecorded()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<RecordingAuditingFeature>().UseFeature<OrdersFeature>().UseFeature<InMemoryMultiTenancyFeature>(),
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["XiHan:MultiTenancy:Tenants:acme:DisplayName"] = "Acme" });
        var sink = host.Services.GetRequiredService<RecordingAuditSink>();

        var created = await host.InvokeOperationAsync<CreateOrderRequest, OrderResponse>(CreateOrder.Id, new(1m), new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Principal] = "alice", [BaggageKeys.Tenant] = "acme" });

        Assert.True(created.IsSuccess, created.ToString());
        Assert.Equal("acme", Assert.Single(sink.Events).TenantId);
    }
}
