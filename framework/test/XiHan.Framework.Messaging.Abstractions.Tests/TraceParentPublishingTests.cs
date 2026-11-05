// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Messaging.Abstractions.Tests;

/// <summary>
/// 发布侧补的 <c>traceparent</c> 带调用的采样标志（RFC-0009「信封」，RFC-0003 规则 7）
/// </summary>
/// <remarks>
/// 上游「不采样」原样传下去，链路的根按已采样处理；当前 Activity 与调用同一 trace 且有采样决定时以它为父级、带它的标志
/// </remarks>
public sealed class TraceParentPublishingTests : IDisposable
{
    /// <summary>
    /// 调用的追踪 ID
    /// </summary>
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    /// <summary>
    /// 调用的父跨度 ID
    /// </summary>
    private const string SpanId = "00f067aa0ba902b7";

    /// <summary>
    /// 只给本测试用的 ActivitySource
    /// </summary>
    private readonly ActivitySource _source = new($"xihan.tests.traceparent.{Guid.NewGuid():N}");

    /// <summary>
    /// 只监听 <see cref="_source"/> 的监听器，采样结果由 <see cref="_sampling"/> 决定
    /// </summary>
    private readonly ActivityListener _listener;

    /// <summary>
    /// 监听器给出的采样结果
    /// </summary>
    private ActivitySamplingResult _sampling = ActivitySamplingResult.AllDataAndRecorded;

    /// <summary>
    /// 登记只监听本测试来源的监听器
    /// </summary>
    public TraceParentPublishingTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => ReferenceEquals(source, _source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => _sampling,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    [Theory]
    [InlineData(ActivityTraceFlags.None, "00")]
    [InlineData(ActivityTraceFlags.Recorded, "01")]
    public async Task Publisher_WritesTheUpstreamSamplingFlag(ActivityTraceFlags upstream, string flags)
    {
        var context = App.CreateContext(new ServiceCollection().BuildServiceProvider(), CancellationToken.None, TraceId, SpanId, traceFlags: upstream);

        var published = await PublishAsync(context);

        Assert.Equal($"00-{TraceId}-{SpanId}-{flags}", published);
    }

    [Fact]
    public async Task Publisher_TreatsTheRootOfATraceAsSampled()
    {
        // 没有上游：标志为空，按已采样处理
        var context = App.CreateContext(new ServiceCollection().BuildServiceProvider(), CancellationToken.None);
        Assert.Null(context.TraceFlags);

        var published = await PublishAsync(context);

        Assert.Equal($"00-{context.TraceId}-{context.SpanId}-01", published);
    }

    [Theory]
    [InlineData(ActivitySamplingResult.AllDataAndRecorded, "01")]
    [InlineData(ActivitySamplingResult.PropagationData, "00")]
    public async Task Publisher_PrefersTheCurrentActivityOfTheSameTrace(ActivitySamplingResult sampling, string flags)
    {
        var context = App.CreateContext(new ServiceCollection().BuildServiceProvider(), CancellationToken.None, TraceId, SpanId, traceFlags: ActivityTraceFlags.Recorded);
        _sampling = sampling;
        using var activity = _source.StartActivity("publish", ActivityKind.Internal, new ActivityContext(ActivityTraceId.CreateFromString(TraceId), ActivitySpanId.CreateFromString(SpanId), ActivityTraceFlags.Recorded, isRemote: true));
        Assert.NotNull(activity);

        var published = await PublishAsync(context);

        // 消费端是发布时这个 Span 的子 Span，采样随它的决定
        Assert.Equal($"00-{TraceId}-{activity.SpanId.ToHexString()}-{flags}", published);
    }

    [Fact]
    public async Task Publisher_IgnoresACurrentActivityOfAnotherTrace()
    {
        var context = App.CreateContext(new ServiceCollection().BuildServiceProvider(), CancellationToken.None, TraceId, SpanId, traceFlags: ActivityTraceFlags.None);
        using var other = _source.StartActivity("unrelated", ActivityKind.Internal, new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, isRemote: true));
        Assert.NotNull(other);

        Assert.Equal($"00-{TraceId}-{SpanId}-00", await PublishAsync(context));
    }

    /// <summary>
    /// 在给定调用里经直接发布者发布一条消息，返回代理收到的 <c>traceparent</c>
    /// </summary>
    private static async Task<string> PublishAsync(InvocationContext context)
    {
        var broker = new CapturingBroker();
        var publisher = new DirectPublisher(broker, new FixedInvocation(context));
        (await publisher.PublishAsync("orders", new MessageEnvelope { Id = "1", Source = "/o", Type = "t" }, CancellationToken.None)).ThrowIfFailure();
        return broker.Last!.Extension(EnvelopeExtensions.TraceParent).ValueOrThrow();
    }

    /// <summary>
    /// 固定返回给定上下文的调用访问器
    /// </summary>
    private sealed class FixedInvocation(InvocationContext context) : IInvocationAccessor
    {
        /// <inheritdoc />
        public Maybe<InvocationContext> Current => Maybe.Some(context);
    }

    /// <summary>
    /// 只记下最后一个信封的代理
    /// </summary>
    private sealed class CapturingBroker : IMessageBroker
    {
        /// <summary>
        /// 最后收到的信封
        /// </summary>
        public MessageEnvelope? Last { get; private set; }

        /// <inheritdoc />
        public ValueTask<Result> PublishAsync(string topic, MessageEnvelope envelope, CancellationToken cancellationToken)
        {
            Last = envelope;
            return ValueTask.FromResult(Result.Success());
        }

        /// <inheritdoc />
        public ValueTask<IReadOnlyList<Result>> PublishManyAsync(string topic, IReadOnlyList<MessageEnvelope> envelopes, CancellationToken cancellationToken)
        {
            Last = envelopes[^1];
            return ValueTask.FromResult<IReadOnlyList<Result>>([.. envelopes.Select(static _ => Result.Success())]);
        }

        /// <inheritdoc />
        public Task StartAsync(IReadOnlyCollection<MessageSubscription> subscriptions, MessageDispatch dispatch, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
