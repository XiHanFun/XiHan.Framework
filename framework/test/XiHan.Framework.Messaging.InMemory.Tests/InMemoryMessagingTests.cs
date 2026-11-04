// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
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
/// 内存代理的选项解析、容量与死信，以及发件箱与内存持久化配合的测试
/// </summary>
public sealed class InMemoryMessagingTests
{
    /// <summary>
    /// 测试订阅的主题
    /// </summary>
    private const string Topic = "orders";

    /// <summary>
    /// 来源 <c>/tests</c>、类型 <c>orders.placed</c> 的测试信封
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
    public async Task Instance_ComesFromTheConfiguredId_AndAnInvalidIdFailsAssembly()
    {
        await using var configured = await TestHost.StartAsync(static b => b.UseFeature<InMemoryMessagingFeature>(), [new("XiHan:Messaging:InstanceId", "orders-api-0")]);

        // 装配登记的单例取配置值，选项里也只有配置值
        Assert.Equal("orders-api-0", configured.Services.GetRequiredService<MessagingInstance>().Id);
        Assert.Equal("orders-api-0", configured.Services.GetRequiredService<MessagingOptions>().InstanceId);

        // 写了但不合文法：装配时失败，不退回随机标识
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => TestHost.StartAsync(static b => b.UseFeature<InMemoryMessagingFeature>(), [new("XiHan:Messaging:InstanceId", "orders@0")]));
        Assert.Contains("XiHan:Messaging:InstanceId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_ToATopicNobodySubscribes_IsDroppedAndLogged_WithoutCreatingATopic()
    {
        var log = new DeliveryLog();
        var logs = new LogCapture();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            services: services =>
            {
                services.AddSingleton<ILoggerFactory>(logs);
                services.AddSingleton(log);
                services.AddMessageHandler<RecordingHandler>(Topic);
            });
        var broker = host.Services.GetRequiredService<InMemoryMessageBroker>();
        Assert.Equal(1, broker.TopicCount);

        // 没人订阅：发布成功、消息丢弃，不建主题；主题数只随订阅表，不随发布增长
        Assert.True((await broker.PublishAsync("idle", Envelope("1"), CancellationToken.None)).IsSuccess);
        var batch = await broker.PublishManyAsync("idle", [Envelope("2"), Envelope("3")], CancellationToken.None);
        Assert.Equal(2, batch.Count);
        Assert.All(batch, static r => Assert.True(r.IsSuccess));
        Assert.Equal(1, broker.TopicCount);
        Assert.Equal(0, broker.QueuedCount("idle"));
        Assert.Equal(0, broker.DeliveredCount("idle"));
        Assert.Empty(broker.DeadLetters("idle"));

        // 每次丢弃记一条 Debug 日志，带条数与主题
        var dropped = logs.Entries.Where(static e => e.Category == typeof(InMemoryMessageBroker).FullName && e.Message.Contains("'idle'", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            [
                (LogLevel.Debug, "Dropped 1 message(s) published to topic 'idle' because nothing subscribes to it."),
                (LogLevel.Debug, "Dropped 2 message(s) published to topic 'idle' because nothing subscribes to it."),
            ],
            dropped.Select(static e => (e.Level, e.Message)));

        // 被订阅的主题照常排队与投递
        Assert.True((await broker.PublishAsync(Topic, Envelope("4"), CancellationToken.None)).IsSuccess);
        Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "4", 1, TimeSpan.FromSeconds(10)));
        Assert.DoesNotContain(log.All, static r => r.Delivery.Topic == "idle");
    }

    [Fact]
    public async Task Start_ConsumesEverySubscribedTopic_WithoutATopicLimit()
    {
        await using var host = await TestHost.StartAsync(static b => b.UseFeature<InMemoryMessagingFeature>());
        // 比原来的缺省上限（1000）多一个主题：主题数只由订阅表决定，不再有上限
        var subscriptions = new MessageSubscriptions();
        for (var i = 0; i <= 1_000; i++)
        {
            subscriptions.Add("topic-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), typeof(RecordingHandler), SubscriptionMode.Competing);
        }

        using var broker = new InMemoryMessageBroker(new InMemoryMessagingOptions(), host.Services.GetRequiredService<MessagingOptions>(), subscriptions, host.App, NullLoggerFactory.Instance);
        await broker.StartAsync([.. subscriptions.All], static (_, _) => ValueTask.FromResult(Result.Success()), CancellationToken.None);

        Assert.Equal(1_001, broker.TopicCount);
        await broker.StopAsync(CancellationToken.None);
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

        // 订阅了、还没开始消费的代理：消息留在通道里，用它验证容量
        var subscriptions = new MessageSubscriptions().Add("idle", typeof(RecordingHandler), SubscriptionMode.Competing);
        using var idle = new InMemoryMessageBroker(new InMemoryMessagingOptions { MaxQueuedMessages = 2 }, host.Services.GetRequiredService<MessagingOptions>(), subscriptions, host.App, NullLoggerFactory.Instance);
        Assert.True((await idle.PublishAsync("idle", Envelope("1"), CancellationToken.None)).IsSuccess);
        // 批量发布容量不足时整批 QueueFull，不写入半批
        var overflow = await idle.PublishManyAsync("idle", [Envelope("2"), Envelope("3")], CancellationToken.None);
        Assert.All(overflow, static r => Assert.Equal(MessagingErrors.QueueFull, r.Error?.Code));
        Assert.Equal(1, idle.QueuedCount("idle"));
        Assert.All(await idle.PublishManyAsync("idle", [Envelope("2")], CancellationToken.None), static r => Assert.True(r.IsSuccess));
        var full = await idle.PublishAsync("idle", Envelope("3"), CancellationToken.None);
        Assert.Equal(MessagingErrors.QueueFull, full.Error?.Code);
        Assert.Equal(2, idle.QueuedCount("idle"));

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
    public async Task BroadcastDelivery_IsDroppedInsteadOfDeadLettered_AndEachModeTakesASlot()
    {
        var log = new DeliveryLog();
        log.FailingIds["bad"] = 0;
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            [new("XiHan:Messaging:MaxAttempts", "2")],
            services =>
            {
                services.AddSingleton(log);
                services.AddMessageHandler<RecordingHandler>(Topic);
                services.AddMessageHandler<RecordingHandler>(Topic, SubscriptionMode.Broadcast);
            });
        var broker = host.Services.GetRequiredService<InMemoryMessageBroker>();

        Assert.True((await broker.PublishAsync(Topic, Envelope("good"), CancellationToken.None)).IsSuccess);
        Assert.True((await broker.PublishAsync(Topic, Envelope("bad"), CancellationToken.None)).IsSuccess);
        await log.WaitAsync(static r => r.Delivery.Envelope.Id == "good", 2, TimeSpan.FromSeconds(10));
        await log.WaitAsync(static r => r.Delivery.Envelope.Id == "bad", 4, TimeSpan.FromSeconds(10));
        await Task.Delay(100);

        // 两种方式各一次投递、各算一次成功；失败的广播投递到上限丢弃，只有竞争投递进死信
        Assert.Equal(2, broker.DeliveredCount(Topic));
        var dead = Assert.Single(broker.DeadLetters(Topic));
        Assert.Equal(("bad", SubscriptionMode.Competing, 2), (dead.Envelope.Id, dead.Mode, dead.Attempt));

        // 一条消息给每种方式各占一个位置：容量 2 的队列放下一条就满
        var subscriptions = new MessageSubscriptions().Add("idle", typeof(RecordingHandler), SubscriptionMode.Competing).Add("idle", typeof(RecordingHandler), SubscriptionMode.Broadcast);
        using var idle = new InMemoryMessageBroker(new InMemoryMessagingOptions { MaxQueuedMessages = 2 }, host.Services.GetRequiredService<MessagingOptions>(), subscriptions, host.App, NullLoggerFactory.Instance);
        Assert.True((await idle.PublishAsync("idle", Envelope("1"), CancellationToken.None)).IsSuccess);
        Assert.Equal(2, idle.QueuedCount("idle"));
        Assert.Equal(MessagingErrors.QueueFull, (await idle.PublishAsync("idle", Envelope("2"), CancellationToken.None)).Error?.Code);
        Assert.All(await idle.PublishManyAsync("idle", [Envelope("3")], CancellationToken.None), static r => Assert.Equal(MessagingErrors.QueueFull, r.Error?.Code));
        Assert.Equal(2, idle.QueuedCount("idle"));
    }

    [Fact]
    public async Task UnsampledInvocation_PublishesAnUnsampledTraceparent_AndTheHandlerKeepsIt()
    {
        var log = new DeliveryLog();
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryMessagingFeature>(),
            services: services =>
            {
                services.AddSingleton(log);
                services.AddMessageHandler<RecordingHandler>(Topic);
            });
        await using var scope = host.Services.CreateAsyncScope();
        // 上游不采样（HTTP 的 -00 等）的调用里发布：处理器的调用同一 trace、仍不采样（RFC-0003 规则 7）
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None, "4bf92f3577b34da6a3ce929d0e0e4736", "00f067aa0ba902b7", traceFlags: ActivityTraceFlags.None);

        (await host.App.InvokeAsync(context, async ctx => (await ctx.Services.GetRequiredService<IMessagePublisher>().PublishAsync(Topic, Envelope("unsampled"), ctx.CancellationToken)).ThrowIfFailure())).ThrowIfFailure();

        var received = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "unsampled", 1, TimeSpan.FromSeconds(10)));
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", received.TraceId);
        Assert.Equal(ActivityTraceFlags.None, received.TraceFlags);
        Assert.EndsWith("-00", received.Delivery.Envelope.Extension(EnvelopeExtensions.TraceParent).ValueOrThrow(), StringComparison.Ordinal);
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
