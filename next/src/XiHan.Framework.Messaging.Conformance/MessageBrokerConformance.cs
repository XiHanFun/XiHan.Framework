// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Messaging.Abstractions;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;
using Xunit;

namespace XiHan.Framework.Messaging.Conformance;

/// <summary>
/// 套件记录的一次到达：投递、Baggage 快照、追踪 ID。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record ReceivedMessage(MessageDelivery Delivery, IReadOnlyDictionary<string, string> Baggage, string TraceId);

/// <summary>
/// 套件的到达日志：处理器把每次投递写进来，用例按条件等待。可指定某些事件 ID 失败或抛异常。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class DeliveryLog
{
    private readonly Channel<ReceivedMessage> _received = Channel.CreateUnbounded<ReceivedMessage>();
    private readonly List<ReceivedMessage> _all = [];
    private readonly Lock _gate = new();

    /// <summary>
    /// 处理时返回失败的事件 ID。
    /// </summary>
    public ConcurrentDictionary<string, byte> FailingIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 处理时抛异常的事件 ID。
    /// </summary>
    public ConcurrentDictionary<string, byte> ThrowingIds { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 全部到达，按顺序。
    /// </summary>
    public IReadOnlyList<ReceivedMessage> All
    {
        get
        {
            lock (_gate)
            {
                return [.. _all];
            }
        }
    }

    /// <summary>
    /// 记录。
    /// </summary>
    public void Record(ReceivedMessage received)
    {
        ArgumentNullException.ThrowIfNull(received);
        lock (_gate)
        {
            _all.Add(received);
        }

        _received.Writer.TryWrite(received);
    }

    /// <summary>
    /// 等到满足条件的到达累计 <paramref name="count"/> 条；超时抛出。
    /// </summary>
    public async Task<IReadOnlyList<ReceivedMessage>> WaitAsync(Func<ReceivedMessage, bool> predicate, int count, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        using var cts = new CancellationTokenSource(timeout);
        var matched = All.Where(predicate).ToList();
        if (matched.Count >= count)
        {
            return matched;
        }

        try
        {
            await foreach (var received in _received.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
            {
                if (All.Count(predicate) >= count)
                {
                    return All.Where(predicate).ToList();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"{timeout} 内没有等到 {count} 条匹配的投递；已到达 {All.Count} 条。");
        }

        return All.Where(predicate).ToList();
    }
}

/// <summary>
/// 套件处理器：记录到达并按日志指示失败或抛异常。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class RecordingHandler(DeliveryLog log) : IMessageHandler
{
    /// <inheritdoc />
    public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(context);
        log.Record(new ReceivedMessage(delivery, new Dictionary<string, string>(context.Baggage, StringComparer.Ordinal), context.TraceId));
        if (log.ThrowingIds.ContainsKey(delivery.Envelope.Id))
        {
            throw new InvalidOperationException($"处理器按指示对 {delivery.Envelope.Id} 抛异常。");
        }

        return ValueTask.FromResult(log.FailingIds.ContainsKey(delivery.Envelope.Id)
            ? Result.Failure(Error.Validation("CONFORMANCE_VALIDATION_001", "处理器按指示失败。"))
            : Result.Success());
    }
}

/// <summary>
/// <c>messaging.queue@1</c> Provider 的一致性套件。继承并实现 <see cref="CreateFeature"/>，全部用例随之执行。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public abstract class MessageBrokerConformance
{
    /// <summary>
    /// 套件使用的主题。
    /// </summary>
    public const string OrdersTopic = "conformance.orders";

    /// <summary>
    /// 套件使用的第二个主题。
    /// </summary>
    public const string ShipmentsTopic = "conformance.shipments";

    /// <summary>
    /// 套件配置的最多投递次数。
    /// </summary>
    public const int MaxAttempts = 2;

    /// <summary>
    /// 等待投递的超时。
    /// </summary>
    protected virtual TimeSpan DeliveryTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// 宿主配置；Provider 需要的连接等在此提供。
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> Configuration => [];

    /// <summary>
    /// 创建被测 Provider 的特性。每个用例调用一次。
    /// </summary>
    protected abstract IFeature CreateFeature();

    /// <summary>
    /// 用例 1：信封的属性、扩展与数据完整到达处理器。
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
    /// 用例 2：非 JSON 数据按字节完整往返。
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
    /// 用例 3：同一主题按发布顺序投递。
    /// </summary>
    [Fact]
    public async Task Publish_PreservesOrder_WithinTopic()
    {
        var log = new DeliveryLog();
        await using var host = await StartAsync(log).ConfigureAwait(false);
        var ids = Enumerable.Range(0, 20).Select(static i => $"seq-{i:D2}").ToList();
        foreach (var id in ids)
        {
            (await PublishAsync(host, OrdersTopic, Envelope(id, "orders.seq")).ConfigureAwait(false)).ThrowIfFailure();
        }

        var received = await log.WaitAsync(static r => r.Delivery.Envelope.Id.StartsWith("seq-", StringComparison.Ordinal), ids.Count, DeliveryTimeout).ConfigureAwait(false);

        Assert.Equal(ids, received.Select(static r => r.Delivery.Envelope.Id));
    }

    /// <summary>
    /// 用例 4：主题互不串扰。
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
    /// 用例 5：处理器失败时重试到上限后停止，后续消息照常投递。
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
    /// 用例 6：处理器抛异常同样算失败并重试。
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
    /// 用例 7：租户、关联与追踪扩展进入调用上下文。
    /// </summary>
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
    }

    /// <summary>
    /// 用例 8：非法主题被发布者拒绝，不到达代理。
    /// </summary>
    [Fact]
    public async Task Publisher_RejectsInvalidTopic()
    {
        await using var host = await StartAsync(new DeliveryLog()).ConfigureAwait(false);

        var result = await PublishAsync(host, "Not A Topic", Envelope("x", "orders.x")).ConfigureAwait(false);

        Assert.Equal(MessagingErrors.InvalidTopic, result.Error?.Code);
    }

    /// <summary>
    /// 装配并启动宿主：被测特性、两个主题的记录处理器、套件配置（最多投递 2 次）。
    /// </summary>
    protected Task<TestHost> StartAsync(DeliveryLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        var configuration = new List<KeyValuePair<string, string?>> { new($"{MessagingOptions.ConfigurationSection}:MaxAttempts", MaxAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        configuration.AddRange(Configuration);
        return TestHost.StartAsync(
            builder => builder.UseFeature(CreateFeature()),
            configuration,
            services =>
            {
                services.AddSingleton(log);
                services.AddMessageHandler<RecordingHandler>(OrdersTopic);
                services.AddMessageHandler<RecordingHandler>(ShipmentsTopic);
            });
    }

    /// <summary>
    /// 在新作用域内发布。
    /// </summary>
    protected static async Task<Result> PublishAsync(TestHost host, string topic, MessageEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(host);
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMessagePublisher>().PublishAsync(topic, envelope, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 套件信封。
    /// </summary>
    protected static MessageEnvelope Envelope(string id, string type) => new() { Id = id, Source = "/conformance", Type = type };
}
