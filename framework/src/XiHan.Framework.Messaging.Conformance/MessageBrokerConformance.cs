// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// <c>messaging.queue@1</c> Provider 的一致性套件（含批量发布用例）
/// </summary>
/// <remarks>
/// 继承并实现 <see cref="CreateFeature"/>，全部用例随之执行
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public abstract class MessageBrokerConformance
{
    /// <summary>
    /// 套件使用的主题
    /// </summary>
    public const string OrdersTopic = "conformance.orders";

    /// <summary>
    /// 套件使用的第二个主题
    /// </summary>
    public const string ShipmentsTopic = "conformance.shipments";

    /// <summary>
    /// 套件里没人订阅的主题
    /// </summary>
    public const string UnsubscribedTopic = "conformance.unsubscribed";

    /// <summary>
    /// 订阅方式用例的主题
    /// </summary>
    public const string NoticesTopic = "conformance.notices";

    /// <summary>
    /// 套件配置的最多投递次数
    /// </summary>
    public const int MaxAttempts = 2;

    /// <summary>
    /// 等待投递的超时
    /// </summary>
    protected virtual TimeSpan DeliveryTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// 被测 Provider 能否让两个宿主连到同一个代理
    /// </summary>
    /// <remarks>
    /// 默认能。多实例用例（广播每个实例都收到、竞争只有一个实例收到）要两个宿主共用一个代理；内存代理每个宿主各有一个进程内代理，覆写为 <see langword="false"/>，这些用例跳过
    /// </remarks>
    protected virtual bool SupportsMultipleInstances => true;

    /// <summary>
    /// 宿主配置
    /// </summary>
    /// <remarks>
    /// Provider 需要的连接等在此提供
    /// </remarks>
    protected virtual IEnumerable<KeyValuePair<string, string?>> Configuration => [];

    /// <summary>
    /// 创建被测 Provider 的特性
    /// </summary>
    /// <remarks>
    /// 每个用例调用一次
    /// </remarks>
    protected abstract IFeature CreateFeature();

    /// <summary>
    /// 用例 1：信封的属性、扩展与数据完整到达处理器
    /// </summary>
    [Fact]
    public async Task Publish_DeliversEnvelope_WithAttributesExtensionsAndData()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);
        var envelope = Envelope("evt-1", "orders.placed") with
        {
            Time = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero),
            Subject = "order/42",
            Data = Encoding.UTF8.GetBytes("{\"orderId\":42}"),
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal) { ["region"] = "cn" },
        };

        (await PublishAsync(host, OrdersTopic, envelope).ConfigureAwait(false)).ThrowIfFailure();

        var received = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "evt-1", 1, DeliveryTimeout).ConfigureAwait(false));
        Assert.Equal(OrdersTopic, received.Delivery.Topic);
        Assert.Equal(1, received.Delivery.Attempt);
        Assert.Equal("evt-1", received.Delivery.Envelope.Id);
        Assert.Equal("/conformance", received.Delivery.Envelope.Source);
        Assert.Equal("orders.placed", received.Delivery.Envelope.Type);
        Assert.Equal(envelope.Time, received.Delivery.Envelope.Time);
        Assert.Equal("order/42", received.Delivery.Envelope.Subject);
        Assert.Equal("application/json", received.Delivery.Envelope.DataContentType);
        Assert.Equal("{\"orderId\":42}", Encoding.UTF8.GetString(received.Delivery.Envelope.Data.Span));
        Assert.Equal("cn", received.Delivery.Envelope.Extensions["region"]);
    }

    /// <summary>
    /// 用例 2：非 JSON 数据按字节完整往返
    /// </summary>
    [Fact]
    public async Task Publish_PreservesBinaryData()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);
        var payload = new byte[] { 0, 1, 2, 255, 254, 253 };

        (await PublishAsync(host, OrdersTopic, Envelope("bin-1", "orders.blob") with { DataContentType = "application/octet-stream", Data = payload }).ConfigureAwait(false)).ThrowIfFailure();

        var received = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "bin-1", 1, DeliveryTimeout).ConfigureAwait(false));
        Assert.Equal("application/octet-stream", received.Delivery.Envelope.DataContentType);
        Assert.True(payload.AsSpan().SequenceEqual(received.Delivery.Envelope.Data.Span));
    }

    /// <summary>
    /// 用例 3：同一主题按发布顺序投递
    /// </summary>
    /// <remarks>
    /// 一次批量发布多条同样保持入参顺序
    /// </remarks>
    [Fact]
    public async Task Publish_PreservesOrder_WithinTopic()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);
        var ids = Enumerable.Range(0, 20).Select(static i => $"seq-{i:D2}").ToList();
        // 前十条逐条发布，后十条一次批量发布：两种方式在同一主题内都保持顺序
        foreach (var id in ids.Take(10))
        {
            (await PublishAsync(host, OrdersTopic, Envelope(id, "orders.seq")).ConfigureAwait(false)).ThrowIfFailure();
        }

        var batched = await PublishManyAsync(host, OrdersTopic, [.. ids.Skip(10).Select(static id => Envelope(id, "orders.seq"))]).ConfigureAwait(false);
        Assert.Equal(10, batched.Count);
        Assert.All(batched, static r => Assert.True(r.IsSuccess, r.ToString()));

        var received = await log.WaitAsync(static r => r.Delivery.Envelope.Id.StartsWith("seq-", StringComparison.Ordinal), ids.Count, DeliveryTimeout).ConfigureAwait(false);

        Assert.Equal(ids, received.Select(static r => r.Delivery.Envelope.Id));
    }

    /// <summary>
    /// 用例 4：主题互不串扰
    /// </summary>
    [Fact]
    public async Task Topics_AreIsolated()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);

        (await PublishAsync(host, ShipmentsTopic, Envelope("ship-1", "shipments.sent")).ConfigureAwait(false)).ThrowIfFailure();
        (await PublishAsync(host, OrdersTopic, Envelope("ord-1", "orders.placed")).ConfigureAwait(false)).ThrowIfFailure();

        var shipment = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "ship-1", 1, DeliveryTimeout).ConfigureAwait(false));
        var order = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "ord-1", 1, DeliveryTimeout).ConfigureAwait(false));
        Assert.Equal(ShipmentsTopic, shipment.Delivery.Topic);
        Assert.Equal(OrdersTopic, order.Delivery.Topic);
    }

    /// <summary>
    /// 用例 5：处理器失败时重试到上限后停止，后续消息照常投递
    /// </summary>
    [Fact]
    public async Task FailingHandler_IsRetriedUpToMaxAttempts_ThenStops()
    {
        var log = new DeliveryLog();
        log.FailingIds["bad-1"] = 0;
        await using var host = await StartAsync(log).ConfigureAwait(false);

        (await PublishAsync(host, OrdersTopic, Envelope("bad-1", "orders.bad")).ConfigureAwait(false)).ThrowIfFailure();
        (await PublishAsync(host, OrdersTopic, Envelope("good-1", "orders.good")).ConfigureAwait(false)).ThrowIfFailure();

        var attempts = await log.WaitAsync(static r => r.Delivery.Envelope.Id == "bad-1", MaxAttempts, DeliveryTimeout).ConfigureAwait(false);
        Assert.Equal(Enumerable.Range(1, MaxAttempts), attempts.Select(static r => r.Delivery.Attempt));
        Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "good-1", 1, DeliveryTimeout).ConfigureAwait(false));

        await Task.Delay(200).ConfigureAwait(false);
        Assert.Equal(MaxAttempts, log.All.Count(static r => r.Delivery.Envelope.Id == "bad-1"));
    }

    /// <summary>
    /// 用例 6：处理器抛异常同样算失败并重试
    /// </summary>
    [Fact]
    public async Task ThrowingHandler_IsTreatedAsFailure_AndRetried()
    {
        var log = new DeliveryLog();
        log.ThrowingIds["boom-1"] = 0;
        await using var host = await StartAsync(log).ConfigureAwait(false);

        (await PublishAsync(host, OrdersTopic, Envelope("boom-1", "orders.boom")).ConfigureAwait(false)).ThrowIfFailure();

        var attempts = await log.WaitAsync(static r => r.Delivery.Envelope.Id == "boom-1", MaxAttempts, DeliveryTimeout).ConfigureAwait(false);
        Assert.Equal(MaxAttempts, attempts.Count);
    }

    /// <summary>
    /// 用例 7：租户、关联与追踪扩展进入调用上下文
    /// </summary>
    /// <remarks>
    /// 主体与区域同样按对应表进入 Baggage，见用例 10。<c>traceparent</c> 的采样标志原样还原：<c>-01</c> 为已采样，<c>-00</c>（上游不采样）仍是不采样
    /// </remarks>
    [Fact]
    public async Task TenantCorrelationAndTrace_FlowIntoInvocationContext()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);
        var envelope = Envelope("ctx-1", "orders.ctx")
            .WithExtension(EnvelopeExtensions.Tenant, "acme")
            .WithExtension(EnvelopeExtensions.Correlation, "corr-9")
            .WithExtension(EnvelopeExtensions.TraceParent, "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");

        (await PublishAsync(host, OrdersTopic, envelope).ConfigureAwait(false)).ThrowIfFailure();

        var received = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "ctx-1", 1, DeliveryTimeout).ConfigureAwait(false));
        Assert.Equal("acme", received.Baggage[BaggageKeys.Tenant]);
        Assert.Equal("corr-9", received.Baggage[BaggageKeys.Correlation]);
        Assert.Equal("0af7651916cd43dd8448eb211c80319c", received.TraceId);
        Assert.Equal(ActivityTraceFlags.Recorded, received.TraceFlags);
        Assert.Equal(new InvocationOrigin { Protocol = InvocationProtocols.Messaging, Target = OrdersTopic }, received.Origin);

        // 上游不采样（-00）：标志原样还原，不被改成采样
        (await PublishAsync(host, OrdersTopic, Envelope("ctx-2", "orders.ctx").WithExtension(EnvelopeExtensions.TraceParent, "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00")).ConfigureAwait(false)).ThrowIfFailure();
        var unsampled = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "ctx-2", 1, DeliveryTimeout).ConfigureAwait(false));
        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", unsampled.TraceId);
        Assert.Equal(ActivityTraceFlags.None, unsampled.TraceFlags);
    }

    /// <summary>
    /// 用例 8：非法主题被发布者拒绝，不到达代理
    /// </summary>
    [Fact]
    public async Task Publisher_RejectsInvalidTopic()
    {
        await using var host = await StartAsync(new DeliveryLog()).ConfigureAwait(false);

        var result = await PublishAsync(host, "Not A Topic", Envelope("x", "orders.x")).ConfigureAwait(false);

        Assert.Equal(MessagingErrors.InvalidTopic, result.Error?.Code);
    }

    /// <summary>
    /// 用例 9：批量发布逐项结果与入参一一对应并全部投递
    /// </summary>
    /// <remarks>
    /// 空集合返回空列表；主题不合法时每一项都被发布者拒绝，不到达代理
    /// </remarks>
    [Fact]
    public async Task PublishMany_ReportsPerItem_EmptyIsEmpty_AndInvalidTopicRejectsAll()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);

        var results = await PublishManyAsync(host, OrdersTopic, [Envelope("many-1", "orders.many"), Envelope("many-2", "orders.many")]).ConfigureAwait(false);
        Assert.Equal(2, results.Count);
        Assert.All(results, static r => Assert.True(r.IsSuccess, r.ToString()));
        var received = await log.WaitAsync(static r => r.Delivery.Envelope.Id.StartsWith("many-", StringComparison.Ordinal), 2, DeliveryTimeout).ConfigureAwait(false);
        Assert.Equal(["many-1", "many-2"], received.Select(static r => r.Delivery.Envelope.Id));

        Assert.Empty(await PublishManyAsync(host, OrdersTopic, []).ConfigureAwait(false));

        var rejected = await PublishManyAsync(host, "Not A Topic", [Envelope("x-1", "orders.x"), Envelope("x-2", "orders.x")]).ConfigureAwait(false);
        Assert.Equal(2, rejected.Count);
        Assert.All(rejected, static r => Assert.Equal(MessagingErrors.InvalidTopic, r.Error?.Code));
    }

    /// <summary>
    /// 用例 10：调用内发布的追踪与 Baggage 随消息到达处理器
    /// </summary>
    /// <remarks>
    /// 发布者从当前调用补齐扩展，处理器在同一条追踪链上，带同样的租户、主体、关联与区域；幂等键不随消息传下去；信封上显式设置的扩展优先（RFC-0009「信封」）
    /// </remarks>
    [Fact]
    public async Task PublishWithinInvocation_CarriesTraceAndBaggage_ToTheHandler()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);
        var baggage = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [BaggageKeys.Tenant] = "acme",
            [BaggageKeys.Principal] = "user-7",
            [BaggageKeys.Correlation] = "corr-10",
            [BaggageKeys.Locale] = "zh-CN",
            [BaggageKeys.IdempotencyKey] = "idem-10",
        };
        string? traceId = null;

        var published = await host.InvokeAsync(
            async context =>
            {
                traceId = context.TraceId;
                var publisher = context.Services.GetRequiredService<IMessagePublisher>();
                (await publisher.PublishAsync(OrdersTopic, Envelope("trace-1", "orders.traced"), context.CancellationToken).ConfigureAwait(false)).ThrowIfFailure();
                var explicitCorrelation = Envelope("trace-2", "orders.traced").WithExtension(EnvelopeExtensions.Correlation, "explicit");
                foreach (var result in await publisher.PublishManyAsync(OrdersTopic, [explicitCorrelation], context.CancellationToken).ConfigureAwait(false))
                {
                    result.ThrowIfFailure();
                }
            },
            baggage: baggage).ConfigureAwait(false);
        published.ThrowIfFailure();

        var received = await log.WaitAsync(static r => r.Delivery.Envelope.Id.StartsWith("trace-", StringComparison.Ordinal), 2, DeliveryTimeout).ConfigureAwait(false);
        Assert.Equal(2, received.Count);
        Assert.All(received, message =>
        {
            Assert.Equal(traceId, message.TraceId);
            Assert.Equal("acme", message.Baggage[BaggageKeys.Tenant]);
            Assert.Equal("user-7", message.Baggage[BaggageKeys.Principal]);
            Assert.Equal("zh-CN", message.Baggage[BaggageKeys.Locale]);
            Assert.False(message.Baggage.ContainsKey(BaggageKeys.IdempotencyKey));
        });
        Assert.Equal("corr-10", Assert.Single(received, static r => r.Delivery.Envelope.Id == "trace-1").Baggage[BaggageKeys.Correlation]);
        Assert.Equal("explicit", Assert.Single(received, static r => r.Delivery.Envelope.Id == "trace-2").Baggage[BaggageKeys.Correlation]);
    }

    /// <summary>
    /// 用例 11：发布到没人订阅的主题成功
    /// </summary>
    /// <remarks>
    /// 单条与批量都成功、逐项成功，不当作发布失败（RFC-0009，ADR-0074、ADR-0079）：调度、工作流等框架事件照常发到应用不一定订阅的主题。消息是否留存由 Provider 决定（内存代理与 RabbitMQ 丢弃，Kafka 按保留期留在主题里），这里不断言；订阅的主题照常投递
    /// </remarks>
    [Fact]
    public async Task Publish_ToATopicNobodySubscribes_Succeeds()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);

        var single = await PublishAsync(host, UnsubscribedTopic, Envelope("nobody-1", "conformance.nobody")).ConfigureAwait(false);
        var batch = await PublishManyAsync(host, UnsubscribedTopic, [Envelope("nobody-2", "conformance.nobody"), Envelope("nobody-3", "conformance.nobody")]).ConfigureAwait(false);

        Assert.True(single.IsSuccess, single.ToString());
        Assert.Equal(2, batch.Count);
        Assert.All(batch, static r => Assert.True(r.IsSuccess, r.ToString()));
        (await PublishAsync(host, OrdersTopic, Envelope("after-nobody", "orders.placed")).ConfigureAwait(false)).ThrowIfFailure();
        Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "after-nobody", 1, DeliveryTimeout).ConfigureAwait(false));
        Assert.DoesNotContain(log.All, static r => r.Delivery.Envelope.Id.StartsWith("nobody-", StringComparison.Ordinal));
    }

    /// <summary>
    /// 用例 12：同一主题的竞争订阅与广播订阅各收到一次，各自重试
    /// </summary>
    /// <remarks>
    /// 一个实例里两种方式是两次独立的投递（RFC-0009「订阅方式」）：成功的消息每种方式各一次；失败的消息每种方式各自重试到上限（竞争进死信、广播丢弃），互不影响
    /// </remarks>
    [Fact]
    public async Task CompetingAndBroadcast_OnTheSameTopic_EachReceiveTheMessageOnce_AndRetrySeparately()
    {
        var log = new DeliveryLog();
        log.FailingIds["both-bad"] = 0;
        await using var host = await StartWithSubscriptionsAsync(log, [new(NoticesTopic, SubscriptionMode.Competing), new(NoticesTopic, SubscriptionMode.Broadcast)]).ConfigureAwait(false);
        await WarmUpBroadcastAsync(host, [log]).ConfigureAwait(false);

        (await PublishAsync(host, NoticesTopic, Envelope("both-1", "notices.both")).ConfigureAwait(false)).ThrowIfFailure();
        (await PublishAsync(host, NoticesTopic, Envelope("both-bad", "notices.both")).ConfigureAwait(false)).ThrowIfFailure();

        var received = await log.WaitAsync(static r => r.Delivery.Envelope.Id == "both-1", 2, DeliveryTimeout).ConfigureAwait(false);
        Assert.Equal([SubscriptionMode.Competing, SubscriptionMode.Broadcast], received.Select(static r => r.Delivery.Mode).Order());
        var failed = await log.WaitAsync(static r => r.Delivery.Envelope.Id == "both-bad", 2 * MaxAttempts, DeliveryTimeout).ConfigureAwait(false);
        Assert.Equal(Enumerable.Range(1, MaxAttempts), failed.Where(static r => r.Delivery.Mode == SubscriptionMode.Competing).Select(static r => r.Delivery.Attempt));
        Assert.Equal(Enumerable.Range(1, MaxAttempts), failed.Where(static r => r.Delivery.Mode == SubscriptionMode.Broadcast).Select(static r => r.Delivery.Attempt));

        await Task.Delay(300).ConfigureAwait(false);
        Assert.Equal(2, log.All.Count(static r => r.Delivery.Envelope.Id == "both-1"));
        Assert.Equal(2 * MaxAttempts, log.All.Count(static r => r.Delivery.Envelope.Id == "both-bad"));
    }

    /// <summary>
    /// 用例 13：两个实例的广播订阅都收到同一条消息
    /// </summary>
    /// <remarks>
    /// 两个宿主连同一个代理，各自以广播方式订阅同一主题，只发布一次；每个实例各收到一份。需要 <see cref="SupportsMultipleInstances"/>
    /// </remarks>
    [Fact]
    public async Task Broadcast_ReachesEveryInstance()
    {
        Assert.SkipUnless(SupportsMultipleInstances, "This provider cannot share a broker between two hosts.");
        var first = new DeliveryLog();
        var second = new DeliveryLog();
        await using var one = await StartWithSubscriptionsAsync(first, [new(NoticesTopic, SubscriptionMode.Broadcast)]).ConfigureAwait(false);
        await using var two = await StartWithSubscriptionsAsync(second, [new(NoticesTopic, SubscriptionMode.Broadcast)]).ConfigureAwait(false);
        await WarmUpBroadcastAsync(one, [first, second]).ConfigureAwait(false);

        (await PublishAsync(one, NoticesTopic, Envelope("everyone-1", "notices.broadcast")).ConfigureAwait(false)).ThrowIfFailure();

        foreach (var log in new[] { first, second })
        {
            var received = Assert.Single(await log.WaitAsync(static r => r.Delivery.Envelope.Id == "everyone-1", 1, DeliveryTimeout).ConfigureAwait(false));
            Assert.Equal(SubscriptionMode.Broadcast, received.Delivery.Mode);
            Assert.Equal(1, received.Delivery.Attempt);
        }
    }

    /// <summary>
    /// 用例 14：两个实例的竞争订阅每条消息只有一个收到
    /// </summary>
    /// <remarks>
    /// 两个宿主连同一个代理，各自以竞争方式订阅同一主题；一批消息每条恰好被其中一个实例收到一次。需要 <see cref="SupportsMultipleInstances"/>
    /// </remarks>
    [Fact]
    public async Task Competing_ReachesOnlyOneInstance()
    {
        Assert.SkipUnless(SupportsMultipleInstances, "This provider cannot share a broker between two hosts.");
        var first = new DeliveryLog();
        var second = new DeliveryLog();
        await using var one = await StartWithSubscriptionsAsync(first, [new(NoticesTopic, SubscriptionMode.Competing)]).ConfigureAwait(false);
        await using var two = await StartWithSubscriptionsAsync(second, [new(NoticesTopic, SubscriptionMode.Competing)]).ConfigureAwait(false);
        var ids = Enumerable.Range(0, 10).Select(static i => $"one-of-{i:D2}").ToList();

        var results = await PublishManyAsync(one, NoticesTopic, [.. ids.Select(static id => Envelope(id, "notices.competing"))]).ConfigureAwait(false);
        Assert.All(results, static r => Assert.True(r.IsSuccess, r.ToString()));

        // 两个实例合起来收齐，再多等一会儿，确认没有哪条被两个实例都收到
        var stopwatch = Stopwatch.StartNew();
        while (Received(first, second).Count < ids.Count && stopwatch.Elapsed < DeliveryTimeout)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        await Task.Delay(300).ConfigureAwait(false);
        Assert.Equal(ids, Received(first, second).Order(StringComparer.Ordinal));

        // 两个实例收到的本套件消息
        static List<string> Received(DeliveryLog first, DeliveryLog second)
            => [.. first.All.Concat(second.All).Where(static r => r.Delivery.Envelope.Id.StartsWith("one-of-", StringComparison.Ordinal)).Select(static r => r.Delivery.Envelope.Id)];
    }

    /// <summary>
    /// 装配并启动宿主：被测特性、两个主题的记录处理器、套件配置（最多投递 2 次）
    /// </summary>
    protected Task<TestHost> StartAsync(DeliveryLog log)
        => StartWithSubscriptionsAsync(log, [new(OrdersTopic, SubscriptionMode.Competing), new(ShipmentsTopic, SubscriptionMode.Competing)]);

    /// <summary>
    /// 装配并启动宿主：被测特性、给定订阅的记录处理器、套件配置（最多投递 2 次）
    /// </summary>
    protected Task<TestHost> StartWithSubscriptionsAsync(DeliveryLog log, IReadOnlyList<MessageSubscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(subscriptions);
        var configuration = new List<KeyValuePair<string, string?>> { new($"{MessagingOptions.ConfigurationSection}:MaxAttempts", MaxAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        configuration.AddRange(Configuration);
        return TestHost.StartAsync(
            builder => builder.UseFeature(CreateFeature()),
            configuration,
            services =>
            {
                services.AddSingleton(log);
                foreach (var subscription in subscriptions)
                {
                    services.AddMessageHandler<RecordingHandler>(subscription.Topic, subscription.Mode);
                }
            });
    }

    /// <summary>
    /// 等到每个宿主的广播订阅都开始收消息
    /// </summary>
    /// <remarks>
    /// 广播只投给在线、已经接上代理的实例；有的代理在启动之后才异步接上（Kafka 的消费组要等分到分区）。反复向 <see cref="NoticesTopic"/> 发预热消息（ID 以 <c>warmup-</c> 开头），直到每个记录器都以广播方式收到一条，超过 <see cref="DeliveryTimeout"/> 抛出 <see cref="TimeoutException"/>。用例断言时按 ID 排除预热消息
    /// </remarks>
    protected async Task WarmUpBroadcastAsync(TestHost publisher, IReadOnlyList<DeliveryLog> logs)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(logs);
        var stopwatch = Stopwatch.StartNew();
        for (var round = 0; ; round++)
        {
            (await PublishAsync(publisher, NoticesTopic, Envelope($"warmup-{round}", "notices.warmup")).ConfigureAwait(false)).ThrowIfFailure();
            try
            {
                foreach (var log in logs)
                {
                    await log.WaitAsync(static r => r.Delivery.Mode == SubscriptionMode.Broadcast && r.Delivery.Envelope.Id.StartsWith("warmup-", StringComparison.Ordinal), 1, TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
                }

                return;
            }
            catch (TimeoutException) when (stopwatch.Elapsed < DeliveryTimeout)
            {
                // 还有实例没接上：再发一条
            }
        }
    }

    /// <summary>
    /// 在新作用域内发布
    /// </summary>
    protected static async Task<Result> PublishAsync(TestHost host, string topic, MessageEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(host);
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishAsync(topic, envelope, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 在新作用域内批量发布
    /// </summary>
    protected static async Task<IReadOnlyList<Result>> PublishManyAsync(TestHost host, string topic, IReadOnlyList<MessageEnvelope> envelopes)
    {
        ArgumentNullException.ThrowIfNull(host);
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishManyAsync(topic, envelopes, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 套件信封
    /// </summary>
    protected static MessageEnvelope Envelope(string id, string type) => new() { Id = id, Source = "/conformance", Type = type };
}
