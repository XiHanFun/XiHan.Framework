// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Messaging.Conformance;
using XiHan.Framework.Persistence.Abstractions.Queries;
using XiHan.Framework.Persistence.Abstractions.Repositories;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.InMemory.Tests;

/// <summary>
/// 内存代理的选项解析、容量与死信，以及发件箱与内存持久化配合的测试。
/// </summary>
public sealed class InMemoryMessagingTests
{
    /// <summary>
    /// 测试订阅的主题。
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 来源 <c>/tests</c>、类型 <c>orders.placed</c> 的测试信封。
    /// </summary>
    private static MessageEnvelope Envelope(string id) => new() { Id = id, Source = "/tests", Type = "orders.placed" };

    [Fact]
    public void Options_FromConfiguration_ParsesAndRejectsInvalid()
    {
        // 用内存键值对构造配置
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Equal(10_000, InMemoryMessagingOptions.FromConfiguration(Build()).MaxQueuedMessages);
        Assert.Equal(3, InMemoryMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:InMemory:MaxQueuedMessages", "3"))).MaxQueuedMessages);
        Assert.Throws<ArgumentException>(() => InMemoryMessagingOptions.FromConfiguration(Build(("XiHan:Messaging:InMemory:MaxQueuedMessages", "0"))));
    }

    [Fact]
    public async Task Queue_IsBounded_AndDeadLettersAfterMaxAttempts()
    {
        var log = new DeliveryLog();
        log.FailingIds["bad"] = 0;
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            [new("XiHan:Messaging:MaxAttempts", "2"), new("XiHan:Messaging:InMemory:MaxQueuedMessages", "2")],
            services =>
            {
                services.AddSingleton(log);
                services.AddMessageHandler<RecordingHandler>(Topic);
            });
        var broker = host.Services.GetRequiredService<InMemoryMessageBroker>();

        // 未订阅的主题没有消费者，用它验证容量
        Assert.True((await broker.PublishAsync("idle", Envelope("1"), CancellationToken.None)).IsSuccess);
        // 批量发布容量不足时整批 QueueFull，不写入半批
        var overflow = await broker.PublishManyAsync("idle", [Envelope("2"), Envelope("3")], CancellationToken.None);
        Assert.All(overflow, static r => Assert.Equal(MessagingErrors.QueueFull, r.Error?.Code));
        Assert.Equal(1, broker.QueuedCount("idle"));
        Assert.All(await broker.PublishManyAsync("idle", [Envelope("2")], CancellationToken.None), static r => Assert.True(r.IsSuccess));
        var full = await broker.PublishAsync("idle", Envelope("3"), CancellationToken.None);
        Assert.Equal(MessagingErrors.QueueFull, full.Error?.Code);
        Assert.Equal(2, broker.QueuedCount("idle"));

        Assert.True((await broker.PublishAsync(Topic, Envelope("bad"), CancellationToken.None)).IsSuccess);
        Assert.True((await broker.PublishAsync(Topic, Envelope("good"), CancellationToken.None)).IsSuccess);
        await log.WaitAsync(static r => r.Delivery.Envelope.Id == "good", 1, TimeSpan.FromSeconds(10));
        await log.WaitAsync(static r => r.Delivery.Envelope.Id == "bad", 2, TimeSpan.FromSeconds(10));
        await Task.Delay(100);

        var dead = Assert.Single(broker.DeadLetters(Topic));
        Assert.Equal("bad", dead.Envelope.Id);
        Assert.Equal(2, dead.Attempt);
        Assert.Equal(1, broker.DeliveredCount(Topic));
        Assert.Equal(0, broker.QueuedCount(Topic));
    }

    [Fact]
    public async Task Outbox_PublishesOnlyAfterCommit_AndMarksDispatched()
    {
        var log = new DeliveryLog();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryMessagingFeature>().UseFeature<OutboxFeature>(),
            [new("XiHan:Messaging:Outbox:PollIntervalSeconds", "0.05")],
            services =>
            {
                services.AddSingleton(log);
                services.AddMessageHandler<RecordingHandler>(Topic);
            });
        Assert.Contains("xihan.messaging.outbox", host.App.DescribeGraph(), StringComparison.Ordinal);

        var rolledBack = await host.InvokeAsync(async ctx =>
        {
            var published = await ctx.Services.GetRequiredService<IMessagePublisher>().PublishAsync(Topic, Envelope("rolled-back"), ctx.CancellationToken);
            published.ThrowIfFailure();
            ctx.SetOutcome(Error.Literal("ORDERS_VALIDATION_001", "Rejected by a business rule."));
        });
        var committed = await host.InvokeAsync(async ctx =>
        {
            Assert.IsType<OutboxPublisher>(ctx.Services.GetRequiredService<IMessagePublisher>());
            (await ctx.Services.GetRequiredService<IMessagePublisher>().PublishAsync(Topic, Envelope("committed"), ctx.CancellationToken)).ThrowIfFailure();
        });

        Assert.True(rolledBack.IsFailure);
        Assert.True(committed.IsSuccess, committed.ToString());
        var received = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "committed", 1, TimeSpan.FromSeconds(10)));
        Assert.Equal(Topic, received.Delivery.Topic);
        await Task.Delay(200);
        Assert.DoesNotContain(log.All, static r => r.Delivery.Envelope.Id == "rolled-back");

        OutboxMessage? row = null;
        (await host.InvokeAsync(async ctx =>
        {
            var rows = await ctx.Services.GetRequiredService<IRepository<OutboxMessage, Guid>>().ListAsync(EntityQuery.All<OutboxMessage>(), ctx.CancellationToken);
            row = Assert.Single(rows);
        })).ThrowIfFailure();
        Assert.NotNull(row);
        Assert.Equal(Topic, row.Topic);
        Assert.NotNull(row.DispatchedAt);
        Assert.Equal(1, row.Attempts);
        Assert.Null(row.LastError);
    }

    [Fact]
    public async Task OutboxPublisher_OutsideUnitOfWork_PublishesDirectly()
    {
        var log = new DeliveryLog();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryMessagingFeature>().UseFeature<OutboxFeature>(),
            services: services =>
            {
                services.AddSingleton(log);
                services.AddMessageHandler<RecordingHandler>(Topic);
            });

        await using var scope = host.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishAsync(Topic, Envelope("direct"), CancellationToken.None)).ThrowIfFailure();

        Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "direct", 1, TimeSpan.FromSeconds(10)));
    }
}
