// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.Conformance.Fixtures;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 聚合的领域事件经工作单元 → 发件箱 → 代理 → 处理器的全链路。
/// </summary>
/// <remarks>
/// 提交的调用其事件随订单写入落进发件箱并投递到处理器，回滚的调用什么都不留下；没有发件箱时直接发布到代理。
/// </remarks>
public sealed class DomainEventOutboxTests
{
    /// <summary>
    /// 主题。
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 启动装配了内存持久化、内存消息、可选发件箱、订单聚合与事件登记的宿主。
    /// </summary>
    /// <remarks>
    /// 租户由套件的 Tenant 阶段中间件从 Baggage 解析。
    /// </remarks>
    private static Task<TestHost> StartAsync(DeliveryLog log, bool outbox) => TestHost.StartAsync(
        b =>
        {
            b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryMessagingFeature>().UseFeature<ConformanceFeature>();
            if (outbox)
            {
                b.UseFeature<OutboxFeature>();
            }
        },
        [new("XiHan:Messaging:Outbox:PollIntervalSeconds", "0.05")],
        services =>
        {
            services.AddSingleton(log);
            services.AddRepository<ConformanceOrder, Guid>();
            services.AddDomainEvent<ConformanceEvent>(Topic, "orders.created", ConformanceEventJsonContext.Default.ConformanceEvent, static e => "event/" + e.Name);
            services.AddMessageHandler<RecordingHandler>(Topic);
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AggregateEvents_ReachTheHandler_OnlyWhenTheCallCommits(bool outbox)
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log, outbox);
        var when = new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

        var rolledBack = await host.InvokeAsync(async ctx =>
        {
            var order = new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "rolled-back" };
            order.Raise(new ConformanceEvent("rolled-back", when));
            (await ctx.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>().AddAsync(order, ctx.CancellationToken)).ThrowIfFailure();
            ctx.SetOutcome(Error.Literal("ORDERS_VALIDATION_001", "Rejected by a business rule."));
        });
        var committed = await host.InvokeAsync(async ctx =>
        {
            var order = new ConformanceOrder { Id = Guid.CreateVersion7(), Name = "committed" };
            order.Raise(new ConformanceEvent("committed", when));
            (await ctx.Services.GetRequiredService<IRepository<ConformanceOrder, Guid>>().AddAsync(order, ctx.CancellationToken)).ThrowIfFailure();
        }, baggage: new Dictionary<string, string> { [BaggageKeys.Tenant] = "acme" });

        Assert.True(rolledBack.IsFailure);
        Assert.True(committed.IsSuccess, committed.ToString());
        var received = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Type == "orders.created", 1, TimeSpan.FromSeconds(10)));
        Assert.Equal(Topic, received.Delivery.Topic);
        Assert.Equal("/orders", received.Delivery.Envelope.Source);
        Assert.Equal("event/committed", received.Delivery.Envelope.Subject);
        Assert.Equal(when, received.Delivery.Envelope.Time);
        Assert.Equal("acme", received.Delivery.Envelope.Extension(EnvelopeExtensions.Tenant).ValueOrThrow());
        using var data = JsonDocument.Parse(received.Delivery.Envelope.Data);
        Assert.Equal("committed", data.RootElement.GetProperty("name").GetString());
        await Task.Delay(200);
        Assert.DoesNotContain(log.All, static r => r.Delivery.Envelope.Subject == "event/rolled-back");

        if (outbox)
        {
            // 事件与订单同一事务：回滚的调用在发件箱表里也没有留下行
            OutboxMessage? row = null;
            (await host.InvokeAsync(async ctx =>
            {
                row = Assert.Single(await ctx.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().ListAsync(EntityQuery.All<OutboxMessage>(), ctx.CancellationToken));
            })).ThrowIfFailure();
            Assert.Equal(Topic, row!.Topic);
            Assert.NotNull(row.DispatchedAt);
        }
    }
}
