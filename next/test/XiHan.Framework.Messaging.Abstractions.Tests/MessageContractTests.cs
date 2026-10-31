// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.CloudEvents;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Outbox;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 主题文法、信封、CloudEvents 编码、订阅表、直接发布者与选项解析的契约测试。
/// </summary>
public sealed class MessageContractTests
{
    /// <summary>
    /// 什么都不做、总是成功的处理器。
    /// </summary>
    private sealed class NoopHandler : IMessageHandler
    {
        /// <inheritdoc />
        public ValueTask<Result> HandleAsync(MessageDelivery delivery, InvocationContext context, CancellationToken cancellationToken) => ValueTask.FromResult(Result.Success());
    }

    /// <summary>
    /// 固定返回给定上下文的调用访问器。
    /// </summary>
    private sealed class FixedInvocation(InvocationContext? context) : IInvocationAccessor
    {
        /// <inheritdoc />
        public Maybe<InvocationContext> Current => context is null ? Maybe.None<InvocationContext>() : Maybe.Some(context);
    }

    /// <summary>
    /// 只记录发布、不消费的代理。
    /// </summary>
    private sealed class RecordingBroker : IMessageBroker
    {
        /// <summary>
        /// 收到的发布，按顺序。
        /// </summary>
        public List<(string Topic, MessageEnvelope Envelope)> Published { get; } = [];

        /// <inheritdoc />
        public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
        {
            Published.Add((topic, envelope));
            return ValueTask.FromResult(Result.Success());
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
        {
            Published.AddRange(envelopes.Select(envelope => (topic, envelope)));
            return ValueTask.FromResult<IReadOnlyList<Result>>([.. envelopes.Select(static _ => Result.Success())]);
        }

        /// <inheritdoc />
        public Task StartAsync(IReadOnlyCollection<string> topics, MessageDispatch dispatch, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("orders", true)]
    [InlineData("orders.placed_v2-eu", true)]
    [InlineData("Orders", false)]
    [InlineData("", false)]
    [InlineData(".orders", false)]
    [InlineData("has space", false)]
    public void TopicName_ValidatesShape(string topic, bool valid)
    {
        Assert.Equal(valid, TopicName.IsValid(topic));
        if (!valid)
        {
            Assert.Throws<ArgumentException>(() => TopicName.Validate(topic));
        }
    }

    [Fact]
    public void Envelope_ValidatesRequiredFields_AndExtensionNames()
    {
        var envelope = new MessageEnvelope { Id = "1", Source = "/orders", Type = "orders.placed" };

        Assert.Equal("application/json", envelope.DataContentType);
        Assert.True(envelope.IsJsonData);
        Assert.False((envelope with { DataContentType = "application/octet-stream" }).IsJsonData);
        Assert.True((envelope with { DataContentType = "application/vnd.xihan.order+json; charset=utf-8" }).IsJsonData);
        Assert.Equal("cn", envelope.WithExtension("region", "cn").Extension("region").ValueOrThrow());
        Assert.False(envelope.Extension("region").HasValue);

        Assert.Throws<ArgumentException>(static () => new MessageEnvelope { Id = " ", Source = "/o", Type = "t" });
        Assert.Throws<ArgumentException>(static () => new MessageEnvelope { Id = "1", Source = "", Type = "t" });
        Assert.Throws<ArgumentException>(() => envelope.WithExtension("Region", "cn"));
        Assert.Throws<ArgumentException>(() => envelope.WithExtension("waytoolongextensionname", "cn"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MessageDelivery("orders", envelope, 0));
        Assert.Throws<ArgumentException>(() => new MessageDelivery("Bad Topic", envelope, 1));
    }

    [Fact]
    public void CloudEventsFormat_RoundTripsJsonAndBinaryData()
    {
        var json = new MessageEnvelope
        {
            Id = "evt-1",
            Source = "/orders",
            Type = "orders.placed",
            Time = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.FromHours(8)),
            Subject = "order/42",
            Data = Encoding.UTF8.GetBytes("{\"orderId\":42,\"items\":[1,2]}"),
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal) { [EnvelopeExtensions.Tenant] = "acme", ["traceparent"] = "00-aa-bb-01" },
        };

        var encoded = CloudEventsFormat.Encode(json);
        using var document = JsonDocument.Parse(encoded);
        Assert.Equal("1.0", document.RootElement.GetProperty("specversion").GetString());
        Assert.Equal(42, document.RootElement.GetProperty("data").GetProperty("orderId").GetInt32());
        Assert.Equal("acme", document.RootElement.GetProperty("xihantenant").GetString());
        Assert.False(document.RootElement.TryGetProperty("data_base64", out _));

        var decoded = CloudEventsFormat.Decode(encoded).ValueOrThrow();
        Assert.Equal(json.Id, decoded.Id);
        Assert.Equal(json.Time, decoded.Time);
        Assert.Equal(json.Subject, decoded.Subject);
        Assert.Equal(42, JsonDocument.Parse(decoded.Data).RootElement.GetProperty("orderId").GetInt32());
        Assert.Equal("acme", decoded.Extensions[EnvelopeExtensions.Tenant]);
        Assert.Equal("00-aa-bb-01", decoded.Extensions["traceparent"]);

        var binary = json with { DataContentType = "application/octet-stream", Data = new byte[] { 1, 2, 3 } };
        var binaryEncoded = CloudEventsFormat.Encode(binary);
        using var binaryDocument = JsonDocument.Parse(binaryEncoded);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), binaryDocument.RootElement.GetProperty("data_base64").GetString());
        Assert.True(new byte[] { 1, 2, 3 }.AsSpan().SequenceEqual(CloudEventsFormat.Decode(binaryEncoded).ValueOrThrow().Data.Span));
    }

    [Fact]
    public void CloudEventsFormat_RejectsInvalidDocuments()
    {
        Assert.Throws<FrameworkException>(static () => CloudEventsFormat.Encode(new MessageEnvelope { Id = "1", Source = "/o", Type = "t", Data = Encoding.UTF8.GetBytes("{not json") }));

        Assert.Equal(MessagingErrors.InvalidEnvelope, CloudEventsFormat.Decode(Encoding.UTF8.GetBytes("not json")).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, CloudEventsFormat.Decode(Encoding.UTF8.GetBytes("{\"specversion\":\"0.3\",\"id\":\"1\",\"source\":\"/o\",\"type\":\"t\"}")).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, CloudEventsFormat.Decode(Encoding.UTF8.GetBytes("{\"specversion\":\"1.0\",\"id\":\"1\",\"source\":\"/o\"}")).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, CloudEventsFormat.Decode(Encoding.UTF8.GetBytes("{\"specversion\":\"1.0\",\"id\":\"1\",\"source\":\"/o\",\"type\":\"t\",\"time\":\"yesterday\"}")).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, CloudEventsFormat.Decode(Encoding.UTF8.GetBytes("{\"specversion\":\"1.0\",\"id\":\"1\",\"source\":\"/o\",\"type\":\"t\",\"data_base64\":\"***\"}")).Error?.Code);
        Assert.Equal(MessagingErrors.InvalidEnvelope, CloudEventsFormat.Decode(Encoding.UTF8.GetBytes("{\"specversion\":\"1.0\",\"id\":\"1\",\"source\":\"/o\",\"type\":\"t\",\"Bad-Name\":\"x\"}")).Error?.Code);
    }

    [Fact]
    public void Subscriptions_RecordHandlersPerTopic_AndRegisterScopedServices()
    {
        var services = new ServiceCollection();

        services.AddMessageHandler<NoopHandler>("orders").AddMessageHandler<NoopHandler>("orders").AddMessageHandler<NoopHandler>("shipments");

        var subscriptions = services.GetMessageSubscriptions();
        Assert.Equal(["orders", "shipments"], subscriptions.Topics.OrderBy(static t => t, StringComparer.Ordinal));
        Assert.Equal([typeof(NoopHandler)], subscriptions.HandlersFor("orders"));
        Assert.Empty(subscriptions.HandlersFor("nothing"));
        Assert.Contains(services, static d => d.ServiceType == typeof(NoopHandler) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Same(subscriptions, services.GetMessageSubscriptions());
        Assert.Throws<ArgumentException>(() => subscriptions.Add("Bad Topic", typeof(NoopHandler)));
    }

    [Fact]
    public async Task DirectPublisher_ValidatesTopic_ThenForwards()
    {
        var broker = new RecordingBroker();
        var publisher = new DirectPublisher(broker);
        var envelope = new MessageEnvelope { Id = "1", Source = "/o", Type = "t" };

        Assert.True((await publisher.PublishAsync("orders", envelope, CancellationToken.None)).IsSuccess);
        Assert.Equal(MessagingErrors.InvalidTopic, (await publisher.PublishAsync("Bad", envelope, CancellationToken.None)).Error?.Code);
        Assert.Single(broker.Published);
    }

    [Fact]
    public async Task DirectPublisher_FillsMissingExtensions_FromTheCurrentInvocation()
    {
        var context = TestInvocationContext.Create(baggage: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [BaggageKeys.Tenant] = "from-baggage",
            [BaggageKeys.Principal] = "baggage-user",
            [BaggageKeys.Correlation] = "corr-1",
            [BaggageKeys.Locale] = "zh-CN",
            [BaggageKeys.IdempotencyKey] = "idem-1",
        });
        // 安全上下文优先于 Baggage：认证后的主体与解析出的租户
        context.SetSecurity(new SecurityContext(new Principal("user-7", "Bearer"), new TenantContext("acme")));
        var broker = new RecordingBroker();
        var publisher = new DirectPublisher(broker, new FixedInvocation(context));

        (await publisher.PublishAsync("orders", new MessageEnvelope { Id = "1", Source = "/o", Type = "t" }, CancellationToken.None)).ThrowIfFailure();
        var explicitCorrelation = new MessageEnvelope { Id = "2", Source = "/o", Type = "t" }.WithExtension(EnvelopeExtensions.Correlation, "explicit").WithExtension("region", "cn");
        Assert.All(await publisher.PublishManyAsync("orders", [explicitCorrelation], CancellationToken.None), static r => Assert.True(r.IsSuccess));

        var first = broker.Published[0].Envelope;
        Assert.Equal($"00-{context.TraceId}-{context.SpanId}-01", first.Extensions[EnvelopeExtensions.TraceParent]);
        Assert.Equal("acme", first.Extensions[EnvelopeExtensions.Tenant]);
        Assert.Equal("user-7", first.Extensions[EnvelopeExtensions.Principal]);
        Assert.Equal("corr-1", first.Extensions[EnvelopeExtensions.Correlation]);
        Assert.Equal("zh-CN", first.Extensions[EnvelopeExtensions.Locale]);
        // 幂等键不进入消息：派发不写幂等键
        Assert.Equal(5, first.Extensions.Count);

        // 信封上已有的扩展保持不变，其余照补
        var second = broker.Published[1].Envelope;
        Assert.Equal("explicit", second.Extensions[EnvelopeExtensions.Correlation]);
        Assert.Equal("cn", second.Extensions["region"]);
        Assert.Equal("acme", second.Extensions[EnvelopeExtensions.Tenant]);
        Assert.Equal($"00-{context.TraceId}-{context.SpanId}-01", second.Extensions[EnvelopeExtensions.TraceParent]);
    }

    [Fact]
    public async Task DirectPublisher_FallsBackToBaggage_ForAnonymousCalls_AndLeavesEnvelopesAloneOutsideAnInvocation()
    {
        var context = TestInvocationContext.Create(baggage: new Dictionary<string, string>(StringComparer.Ordinal) { [BaggageKeys.Tenant] = "t-1", [BaggageKeys.Principal] = "svc" });
        var broker = new RecordingBroker();
        var envelope = new MessageEnvelope { Id = "1", Source = "/o", Type = "t" };

        (await new DirectPublisher(broker, new FixedInvocation(context)).PublishAsync("orders", envelope, CancellationToken.None)).ThrowIfFailure();
        (await new DirectPublisher(broker, new FixedInvocation(null)).PublishAsync("orders", envelope, CancellationToken.None)).ThrowIfFailure();
        (await new DirectPublisher(broker).PublishAsync("orders", envelope, CancellationToken.None)).ThrowIfFailure();

        var anonymous = broker.Published[0].Envelope;
        Assert.Equal("t-1", anonymous.Extensions[EnvelopeExtensions.Tenant]);
        Assert.Equal("svc", anonymous.Extensions[EnvelopeExtensions.Principal]);
        Assert.False(anonymous.Extensions.ContainsKey(EnvelopeExtensions.Correlation));
        Assert.False(anonymous.Extensions.ContainsKey(EnvelopeExtensions.Locale));
        Assert.True(anonymous.Extensions.ContainsKey(EnvelopeExtensions.TraceParent));
        // 不在调用内或没有访问器：原样发布
        Assert.Same(envelope, broker.Published[1].Envelope);
        Assert.Same(envelope, broker.Published[2].Envelope);
    }

    [Fact]
    public void BaggageMappings_CoverTheRegisteredKeys_ExceptTheIdempotencyKey()
    {
        Assert.Equal(
            [("xihantenant", BaggageKeys.Tenant), ("xihanprincipal", BaggageKeys.Principal), ("xihancorrelation", BaggageKeys.Correlation), ("xihanlocale", BaggageKeys.Locale)],
            EnvelopeExtensions.BaggageMappings);
        Assert.All(EnvelopeExtensions.BaggageMappings, static m => Assert.True(EnvelopeExtensions.IsValidName(m.Extension)));
        Assert.Equal(BaggageKeys.Registered.Where(static k => k != BaggageKeys.IdempotencyKey).Order(StringComparer.Ordinal), EnvelopeExtensions.BaggageMappings.Select(static m => m.BaggageKey).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Options_FromConfiguration_ParseAndRejectInvalid()
    {
        // 用内存键值对构造配置
        static IConfiguration Build(params (string Key, string Value)[] values)
            => new ConfigurationBuilder().AddInMemoryCollection(values.Select(static v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        var messaging = MessagingOptions.FromConfiguration(Build(("XiHan:Messaging:MaxAttempts", "5"), ("XiHan:Messaging:DeadLetterSuffix", ".dead")));
        Assert.Equal(5, messaging.MaxAttempts);
        Assert.Equal(".dead", messaging.DeadLetterSuffix);
        Assert.Equal(3, MessagingOptions.FromConfiguration(Build()).MaxAttempts);
        Assert.Throws<ArgumentException>(() => MessagingOptions.FromConfiguration(Build(("XiHan:Messaging:MaxAttempts", "0"))));

        var outbox = OutboxOptions.FromConfiguration(Build(("XiHan:Messaging:Outbox:PollIntervalSeconds", "0.25"), ("XiHan:Messaging:Outbox:BatchSize", "7")));
        Assert.Equal(TimeSpan.FromMilliseconds(250), outbox.PollInterval);
        Assert.Equal(7, outbox.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(1), OutboxOptions.FromConfiguration(Build()).PollInterval);
        Assert.Throws<ArgumentException>(() => OutboxOptions.FromConfiguration(Build(("XiHan:Messaging:Outbox:BatchSize", "lots"))));
        // 每轮按批大小分页查发件箱表，超过查询的取出上限在解析配置时就拒绝
        Assert.Equal(1000, OutboxOptions.FromConfiguration(Build(("XiHan:Messaging:Outbox:BatchSize", "1000"))).BatchSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => OutboxOptions.FromConfiguration(Build(("XiHan:Messaging:Outbox:BatchSize", "1001"))));

        var retry = OutboxOptions.FromConfiguration(Build(("XiHan:Messaging:Outbox:MaxAttempts", "4"), ("XiHan:Messaging:Outbox:RetryDelaySeconds", "2.5"), ("XiHan:Messaging:Outbox:MaxRetryDelaySeconds", "60"), ("XiHan:Messaging:Outbox:DispatchedRetentionSeconds", "0")));
        Assert.Equal(4, retry.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(2.5), retry.RetryDelay);
        Assert.Equal(TimeSpan.FromMinutes(1), retry.MaxRetryDelay);
        Assert.Equal(TimeSpan.Zero, retry.DispatchedRetention);
        var defaults = OutboxOptions.FromConfiguration(Build());
        Assert.Equal(10, defaults.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(5), defaults.RetryDelay);
        Assert.Equal(TimeSpan.FromMinutes(5), defaults.MaxRetryDelay);
        Assert.Equal(TimeSpan.FromDays(1), defaults.DispatchedRetention);
        Assert.Throws<ArgumentException>(() => OutboxOptions.FromConfiguration(Build(("XiHan:Messaging:Outbox:MaxAttempts", "0"))));
        Assert.Throws<ArgumentException>(() => OutboxOptions.FromConfiguration(Build(("XiHan:Messaging:Outbox:RetryDelaySeconds", "0"))));
        Assert.Throws<ArgumentException>(() => OutboxOptions.FromConfiguration(Build(("XiHan:Messaging:Outbox:DispatchedRetentionSeconds", "-1"))));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new OutboxOptions { DispatchedRetention = TimeSpan.FromSeconds(-1) });
    }
}
