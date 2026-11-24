// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Handlers;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Hosting;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.Proxies;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 宿主调用插件时的 Span <c>xihan.plugin.invoke</c>
/// </summary>
/// <remarks>
/// 规则出自 <c>spec/plugin/invocation.md</c> §6 与 ADR-0269。前半用假运行时核对两个宿主共用的那一处：名字、种类、标签、父级、状态，以及写给插件的 traceparent 取这条 Span。后半用真实的 EchoPlugin 子进程端到端核对：插件把收到的元数据原样带回宿主，回调的 Span 于是挂在 <c>xihan.plugin.invoke</c> 下面
/// </remarks>
[Collection(nameof(TelemetryGroup))]
public sealed class PluginTracingTests
{
    /// <summary>
    /// Span 名
    /// </summary>
    private const string PluginSpan = "xihan.plugin.invoke";

    /// <summary>
    /// 内核 Observe 阶段的 Span 名
    /// </summary>
    private const string KernelSpan = "xihan.invoke";

    /// <summary>
    /// 上游的 parent-id
    /// </summary>
    private const string UpstreamSpan = "00f067aa0ba902b7";

    /// <summary>
    /// 假运行时订阅的主题
    /// </summary>
    private const string Topic = "stub.events";

    [Fact]
    public async Task Invoke_OpensAClientSpanUnderTheInvocationSpan_AndThePluginIsParentedOnIt()
    {
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        var context = Sampled();
        Activity? during = null;
        TraceParent outgoing = default;
        var runtime = new StubPluginRuntime("xihan.test.alpha", "alphas.run")
        {
            OnInvoke = (operation, ctx) =>
            {
                // 载体在这里组装元数据：当前 Activity 是这条 Span，写给插件的 traceparent 取它的 span-id 与采样位
                during = Activity.Current;
                outgoing = ctx.OutgoingTraceParent();
                return ValueTask.FromResult(Result.Success<object>(operation));
            },
        };

        // 扮演内核 Observe 阶段的 xihan.invoke：同一 trace、以上游为远端父级
        Result<object> result;
        using (var invocation = FrameworkDiagnostics.ActivitySource.StartActivity(KernelSpan, ActivityKind.Internal, Upstream(context)))
        {
            result = await PluginTracing.InvokeAsync(runtime, "xihan.test.alpha", "alphas.run", default, context);
            // 调用返回后当前 Activity 回到外层
            Assert.Same(invocation, Activity.Current);
        }

        Assert.True(result.IsSuccess, result.ToString());
        var span = Assert.Single(recorder.Spans(context.TraceId, PluginSpan));
        Assert.Same(span, during);
        Assert.Equal((ActivityKind.Client, ActivityStatusCode.Unset), (span.Kind, span.Status));
        Assert.Equal(new KeyValuePair<string, object?>[] { new("xihan.plugin.id", "xihan.test.alpha"), new("xihan.operation", "alphas.run") }, span.TagObjects.ToArray());
        var kernel = Assert.Single(recorder.Spans(context.TraceId, KernelSpan));
        Assert.Equal(kernel.SpanId, span.ParentSpanId);
        Assert.Equal(new TraceParent(context.TraceId, span.SpanId.ToHexString(), true), outgoing);
    }

    [Fact]
    public async Task Invoke_WithoutAnInvocationSpan_IsStillInTheTraceOfTheCall()
    {
        // 应用关掉了内置中间件、管道里没有 xihan.invoke：Span 按 RFC-0003 规则 7 以上游为远端父级，不另起一条链路
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        var context = Sampled();
        var runtime = new StubPluginRuntime("xihan.test.alpha", "alphas.run");

        var result = await PluginTracing.InvokeAsync(runtime, "xihan.test.alpha", "alphas.run", default, context);

        Assert.True(result.IsSuccess, result.ToString());
        var span = Assert.Single(recorder.Spans(context.TraceId));
        Assert.Equal(PluginSpan, span.OperationName);
        Assert.Equal(UpstreamSpan, span.ParentSpanId.ToHexString());
        Assert.True(span.HasRemoteParent);

        // 上游不采样：不开 Span，调用照常
        var unsampled = App.CreateContext(Services(), new InvocationContextOptions { TraceId = ActivityTraceId.CreateRandom().ToHexString(), SpanId = UpstreamSpan, TraceFlags = ActivityTraceFlags.None }, CancellationToken.None);
        Assert.True((await PluginTracing.InvokeAsync(runtime, "xihan.test.alpha", "alphas.run", default, unsampled)).IsSuccess);
        Assert.Empty(recorder.Spans(unsampled.TraceId));
    }

    [Fact]
    public async Task Invoke_ThatFails_MarksTheSpanAsError()
    {
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        var failing = new StubPluginRuntime("xihan.test.alpha", "alphas.run")
        {
            OnInvoke = static (_, _) => ValueTask.FromResult(Result.Failure<object>(Error.Unavailable(PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, MessageArguments.Of(("id", "xihan.test.alpha"), ("state", "Failed"))))),
        };
        var throwing = new StubPluginRuntime("xihan.test.alpha", "alphas.run")
        {
            OnInvoke = static (_, _) => throw new OperationCanceledException("The call was cancelled at /secret/path."),
        };

        // 失败结果：状态是 Error，说明是错误码（与 xihan.invoke 一致）
        var failed = Sampled();
        var result = await PluginTracing.InvokeAsync(failing, "xihan.test.alpha", "alphas.run", default, failed);
        Assert.Equal(PluginErrors.Unavailable, result.Error?.Code);
        var span = Assert.Single(recorder.Spans(failed.TraceId));
        Assert.Equal((ActivityStatusCode.Error, PluginErrors.Unavailable), (span.Status, span.StatusDescription));

        // 抛出（取消）：原样抛给调用方，状态是 Error，说明只有异常类型、没有异常消息
        var cancelled = Sampled();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await PluginTracing.InvokeAsync(throwing, "xihan.test.alpha", "alphas.run", default, cancelled));
        var thrown = Assert.Single(recorder.Spans(cancelled.TraceId));
        Assert.Equal((ActivityStatusCode.Error, typeof(OperationCanceledException).FullName), (thrown.Status, thrown.StatusDescription));
    }

    [Fact]
    public async Task ProxyOperation_IsInvokedInsideTheSpan_TaggedWithItsOwnerAndId()
    {
        // 代理操作是宿主调用插件操作的唯一入口：归属（插件的清单 ID）与操作 ID 作标签
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        var context = Sampled();
        var runtime = new StubPluginRuntime("xihan.test.alpha", "alphas.run")
        {
            OnInvoke = static (_, _) => ValueTask.FromResult(Result.Success<object>(new PluginPayload("{}"u8.ToArray()))),
        };
        var proxy = new ProxyOperationDescriptor(new OperationContract("alphas.run"), "xihan.test.alpha", runtime, null);

        var result = await proxy.ExecuteAsync(context, new PluginPayload("{}"u8.ToArray()));

        Assert.True(result.IsSuccess, result.ToString());
        var span = Assert.Single(recorder.Spans(context.TraceId, PluginSpan));
        Assert.Equal(("xihan.test.alpha", "alphas.run"), (span.GetTagItem("xihan.plugin.id"), span.GetTagItem("xihan.operation")));
    }

    [Fact]
    public async Task Delivery_OpensOneSpanPerSubscribedPlugin_WithoutAnOperationTag()
    {
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        using var roster = new PluginRoster<StubPluginRuntime>();
        roster.UseRegistry(new OperationRegistry(new PermissionRegistry()));
        var alpha = new StubPluginRuntime("xihan.test.alpha", "alphas.run", Topic);
        var beta = new StubPluginRuntime("xihan.test.beta", "betas.run", Topic)
        {
            OnDeliver = static () => Task.FromResult(Result.Failure(Error.Unavailable(PluginErrors.Unavailable, PluginMessages.ProcessUnavailable, MessageArguments.Of(("id", "xihan.test.beta"), ("state", "Failed"))))),
        };
        var gamma = new StubPluginRuntime("xihan.test.gamma", "gammas.run");
        foreach (var runtime in new[] { alpha, beta, gamma })
        {
            (await roster.AddAsync(runtime.Manifest.Id, () => runtime, CancellationToken.None)).ValueOrThrow();
        }

        var context = Sampled();
        var delivered = await roster.DeliverAsync(
            new MessageDelivery
            {
                Topic = Topic,
                Envelope = new MessageEnvelope { Id = "evt-1", Source = "tests", Type = "stub.created", Data = "{}"u8.ToArray() },
                Attempt = 1,
                Mode = SubscriptionMode.Competing,
            },
            context);

        // 投给订阅了的两个插件、各一条 Span；没订阅的不投、没有 Span。投递不是操作调用：只有插件 ID，没有操作标签
        Assert.Equal(PluginErrors.Unavailable, delivered.Error?.Code);
        var spans = recorder.Spans(context.TraceId, PluginSpan);
        Assert.Equal(["xihan.test.alpha", "xihan.test.beta"], spans.Select(static s => (string?)s.GetTagItem("xihan.plugin.id")));
        Assert.All(spans, static s => Assert.Equal(["xihan.plugin.id"], s.TagObjects.Select(static tag => tag.Key)));
        Assert.All(spans, static s => Assert.Equal(ActivityKind.Client, s.Kind));
        Assert.Equal((ActivityStatusCode.Unset, ActivityStatusCode.Error), (spans[0].Status, spans[1].Status));
        Assert.Equal(PluginErrors.Unavailable, spans[1].StatusDescription);
        // 两条是兄弟：都以上游为父级，后一条不挂在前一条下面
        Assert.All(spans, static s => Assert.Equal(UpstreamSpan, s.ParentSpanId.ToHexString()));
    }

    [Fact]
    public async Task WithoutListeners_TheCallGoesStraightToTheCarrier()
    {
        // 这个集合不与别的集合并行，此刻没有监听器：不开 Span，交回的就是载体自己的那个任务
        Assert.False(FrameworkDiagnostics.ActivitySource.HasListeners());
        var pending = new TaskCompletionSource<Result<object>>();
        Activity? during = new("placeholder");
        var runtime = new StubPluginRuntime("xihan.test.alpha", "alphas.run")
        {
            OnInvoke = (_, _) =>
            {
                during = Activity.Current;
                return new ValueTask<Result<object>>(pending.Task);
            },
        };

        var call = PluginTracing.InvokeAsync(runtime, "xihan.test.alpha", "alphas.run", default, Sampled()).AsTask();

        Assert.Null(during);
        Assert.Same(pending.Task, call);
        pending.SetResult(Result.Success<object>("done"));
        Assert.True((await call).IsSuccess);
    }

    [Fact]
    public async Task RealPlugin_ReceivesTheSpanAsItsParent_SoItsCallbackHangsUnderIt()
    {
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        await using var host = await EchoPluginHost.StartAsync(static b => b.UseFeature(new StubFeature(TestManifests.Create("xihan.app.traces"))
        {
            OnConfigure = static ctx => ctx.AddOperation<TraceOperation, PingRequest, PingResponse>(new OperationContract("traces.read"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
        }));
        await using var scope = host.Services.CreateAsyncScope();
        var invoker = scope.ServiceProvider.GetRequiredService<IOperationInvoker>();

        // echoes.relay 把收到的元数据（含 trace_parent）原样带回宿主、回调 traces.read，后者回显回调上下文的上游
        var context = Sampled(scope.ServiceProvider);
        var relayed = await invoker.InvokeAsync(context, Request("echoes.relay", """{"operation":"traces.read","payload":{"name":"traced"}}"""));

        Assert.True(relayed.Outcome.IsSuccess, relayed.Outcome.ToString());
        var plugin = Assert.Single(recorder.Spans(context.TraceId, PluginSpan));
        Assert.Equal(ActivityKind.Client, plugin.Kind);
        Assert.Equal((EchoPluginHost.PluginId, "echoes.relay"), (plugin.GetTagItem("xihan.plugin.id"), plugin.GetTagItem("xihan.operation")));
        Assert.Equal(ActivityStatusCode.Unset, plugin.Status);
        var kernel = recorder.Spans(context.TraceId, KernelSpan);
        var outer = Assert.Single(kernel, static s => (string?)s.GetTagItem("xihan.operation") == "echoes.relay");
        Assert.Single(kernel, static s => (string?)s.GetTagItem("xihan.operation") == "traces.read");
        // 链路：xihan.invoke（echoes.relay）→ xihan.plugin.invoke → 插件 → 回调。插件收到的 trace_parent 以这条 Span 为父级、带采样位，
        // 它原样带回来，成了回调上下文的上游
        Assert.Equal(outer.SpanId, plugin.ParentSpanId);
        var upstream = System.Text.Json.JsonDocument.Parse(relayed.Payload).RootElement;
        Assert.Equal(plugin.SpanId.ToHexString(), upstream.GetProperty("name").GetString());
        Assert.Equal(context.TraceId, upstream.GetProperty("principal").GetString());

        // 插件报的业务失败：Span 的状态是 Error，说明是插件的错误码
        var failing = Sampled(scope.ServiceProvider);
        var failed = await invoker.InvokeAsync(failing, Request("echoes.create", """{"text":""}"""));
        Assert.Equal("ECHO_VALIDATION_001", failed.Outcome.Error?.Code);
        var rejected = Assert.Single(recorder.Spans(failing.TraceId, PluginSpan));
        Assert.Equal((ActivityStatusCode.Error, "ECHO_VALIDATION_001"), (rejected.Status, rejected.StatusDescription));
    }

    [Fact]
    public async Task RealPlugin_EventDelivery_IsRecordedUnderTheDeliveryInvocation()
    {
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        await using var host = await EchoPluginHost.StartAsync(static b => b.UseFeature<InMemoryMessagingFeature>());
        await using var scope = host.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        // 信封自带 traceparent：派发时成为投递那次调用的链路
        var traceId = ActivityTraceId.CreateRandom().ToHexString();
        var envelope = new MessageEnvelope
        {
            Id = "evt-traced",
            Source = "tests",
            Type = "echo.created",
            Data = "{}"u8.ToArray(),
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal) { [EnvelopeExtensions.TraceParent] = new TraceParent(traceId, UpstreamSpan, true).ToString() },
        };
        var published = await publisher.PublishAsync("echo.events", envelope, CancellationToken.None);
        Assert.True(published.IsSuccess, published.ToString());

        // 插件对这个主题有两种方式的订阅（不限类型的竞争订阅、只收 echo.cleared 的广播订阅），宿主各有一次派发；只有竞争的那次投给了插件
        Assert.True(
            await EchoPluginHost.WaitUntilAsync(() => recorder.Spans(traceId, PluginSpan) is [var sent] && recorder.Spans(traceId, KernelSpan).Any(s => s.SpanId == sent.ParentSpanId), TimeSpan.FromSeconds(10)),
            "the delivery invocation that handed the event to the plugin must end");
        var plugin = Assert.Single(recorder.Spans(traceId, PluginSpan));
        Assert.Equal(["xihan.plugin.id"], plugin.TagObjects.Select(static tag => tag.Key));
        Assert.Equal(EchoPluginHost.PluginId, plugin.GetTagItem("xihan.plugin.id"));
        Assert.Equal((ActivityKind.Client, ActivityStatusCode.Unset), (plugin.Kind, plugin.Status));
        // 投递那次调用不是操作调用：它的 xihan.invoke 也没有操作标签
        var delivery = Assert.Single(recorder.Spans(traceId, KernelSpan), s => s.SpanId == plugin.ParentSpanId);
        Assert.Null(delivery.GetTagItem("xihan.operation"));
    }

    /// <summary>
    /// 带上游采样决定的调用上下文，trace-id 每次新建
    /// </summary>
    private static InvocationContext Sampled(IServiceProvider? services = null)
        => App.CreateContext(services ?? Services(), new InvocationContextOptions { TraceId = ActivityTraceId.CreateRandom().ToHexString(), SpanId = UpstreamSpan, TraceFlags = ActivityTraceFlags.Recorded }, CancellationToken.None);

    /// <summary>
    /// 上下文的上游作远端父级
    /// </summary>
    private static ActivityContext Upstream(InvocationContext context)
        => new(ActivityTraceId.CreateFromString(context.TraceId), ActivitySpanId.CreateFromString(context.SpanId), ActivityTraceFlags.Recorded, isRemote: true);

    /// <summary>
    /// 空的服务提供器
    /// </summary>
    private static ServiceProvider Services() => new ServiceCollection().BuildServiceProvider();

    /// <summary>
    /// 以 JSON 文本为载荷的请求
    /// </summary>
    private static OperationRequest Request(string operationId, string json) => new(operationId, Encoding.UTF8.GetBytes(json));
}
