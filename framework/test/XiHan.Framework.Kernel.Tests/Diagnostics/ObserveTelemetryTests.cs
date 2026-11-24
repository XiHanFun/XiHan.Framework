// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Kernel.Tests.Diagnostics;

/// <summary>
/// Observe 阶段的 Span <c>xihan.invoke</c> 与两个指标上的操作维度（RFC-0023，ADR-0269）
/// </summary>
/// <remarks>
/// 操作调用带 <c>xihan.operation</c>（登记表里的操作 ID），不是操作调用时没有这个标签；请求里的文本不进标签。每个用例登记自己的操作 ID、用自己的 trace-id，指标只看本用例的异步流里记下的
/// </remarks>
[Collection(nameof(TelemetryGroup))]
public sealed class ObserveTelemetryTests
{
    /// <summary>
    /// 调用计数的仪表名
    /// </summary>
    private const string Invocations = "xihan.invocations";

    /// <summary>
    /// 调用耗时的仪表名
    /// </summary>
    private const string Duration = "xihan.invocation.duration";

    /// <summary>
    /// 上游的 parent-id
    /// </summary>
    private const string UpstreamSpan = "00f067aa0ba902b7";

    [Fact]
    public async Task OperationInvocation_CarriesTheOperationIdOnTheSpanAndBothInstruments()
    {
        var (provider, _) = await StartAsync("telemetry.created");
        using var disposing = provider;
        using var spans = new SpanCapture([FrameworkDiagnostics.SourceName]);
        using var metrics = new MetricCapture([FrameworkDiagnostics.SourceName]);
        metrics.RestrictToCurrentFlow();
        await using var scope = provider.CreateAsyncScope();
        var context = Sampled(scope.ServiceProvider);

        var response = await Invoker(provider).InvokeAsync(context, Request("telemetry.created", """{"text":"hi"}"""));

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        var span = Assert.Single(spans.Spans(context.TraceId));
        Assert.Equal(("xihan.invoke", ActivityKind.Internal, ActivityStatusCode.Unset), (span.OperationName, span.Kind, span.Status));
        Assert.Equal("telemetry.created", span.GetTagItem("xihan.operation"));
        Assert.Equal("success", span.GetTagItem("xihan.outcome"));
        // 操作 ID 在创建时就带上：采样器按它决定要不要这条 Span
        Assert.Contains(spans.SamplingRequests, request => request.TraceId == context.TraceId && request.Name == "xihan.invoke" && request.Tags.Contains(new("xihan.operation", "telemetry.created")));

        var expected = new Dictionary<string, object?> { ["xihan.operation"] = "telemetry.created", ["xihan.outcome"] = "success" };
        var counted = Assert.Single(metrics.Entries, static m => m.Instrument == Invocations);
        Assert.Equal(1, counted.Value);
        Assert.Equal(expected, counted.Tags);
        var timed = Assert.Single(metrics.Entries, static m => m.Instrument == Duration);
        Assert.True(timed.Value >= 0);
        Assert.Equal(expected, timed.Tags);
    }

    [Fact]
    public async Task FailedOperation_MarksTheSpanAsError_AndCountsAsFailureUnderTheSameOperation()
    {
        var (provider, _) = await StartAsync("telemetry.failed");
        using var disposing = provider;
        using var spans = new SpanCapture([FrameworkDiagnostics.SourceName]);
        using var metrics = new MetricCapture([FrameworkDiagnostics.SourceName]);
        metrics.RestrictToCurrentFlow();
        await using var scope = provider.CreateAsyncScope();

        // 操作返回失败：状态是 Error，说明是错误码
        var rejected = Sampled(scope.ServiceProvider);
        var failure = await Invoker(provider).InvokeAsync(rejected, Request("telemetry.failed", """{"text":""}"""));
        Assert.Equal("ECHOES_VALIDATION_001", failure.Outcome.Error?.Code);
        var span = Assert.Single(spans.Spans(rejected.TraceId));
        Assert.Equal((ActivityStatusCode.Error, "ECHOES_VALIDATION_001"), (span.Status, span.StatusDescription));
        Assert.Equal(("telemetry.failed", "failure"), (span.GetTagItem("xihan.operation"), span.GetTagItem("xihan.outcome")));

        // 处理器抛出：由 Observe 阶段翻译成未预期错误，同样记在这个操作名下
        var crashed = Sampled(scope.ServiceProvider);
        var unexpected = await Invoker(provider).InvokeAsync(crashed, Request("telemetry.failed", """{"text":"throw"}"""));
        Assert.Equal(KernelErrors.Unexpected, unexpected.Outcome.Error?.Code);
        var thrown = Assert.Single(spans.Spans(crashed.TraceId));
        Assert.Equal((ActivityStatusCode.Error, KernelErrors.Unexpected), (thrown.Status, thrown.StatusDescription));

        var expected = new Dictionary<string, object?> { ["xihan.operation"] = "telemetry.failed", ["xihan.outcome"] = "failure" };
        Assert.All(metrics.Entries, m => Assert.Equal(expected, m.Tags));
        Assert.Equal(2, metrics.Entries.Count(static m => m.Instrument == Invocations));
        Assert.Equal(2, metrics.Entries.Count(static m => m.Instrument == Duration));
    }

    [Fact]
    public async Task InvocationWithoutAnOperation_HasNoOperationTag()
    {
        // 消息处理器、作业与直接交给 App.InvokeAsync 的处理器都没有操作描述：不带标签，也不拿占位值顶替
        var (provider, app) = await StartAsync("telemetry.unused");
        using var disposing = provider;
        using var spans = new SpanCapture([FrameworkDiagnostics.SourceName]);
        using var metrics = new MetricCapture([FrameworkDiagnostics.SourceName]);
        metrics.RestrictToCurrentFlow();
        await using var scope = provider.CreateAsyncScope();
        var context = Sampled(scope.ServiceProvider);

        var result = await app.InvokeAsync(context, static _ => ValueTask.CompletedTask);

        Assert.True(result.IsSuccess, result.ToString());
        var span = Assert.Single(spans.Spans(context.TraceId));
        Assert.Equal(["xihan.outcome"], span.TagObjects.Select(static tag => tag.Key));
        Assert.DoesNotContain(spans.SamplingRequests, static request => request.Name == "xihan.invoke" && request.Tags.Contains(new("xihan.operation", "telemetry.unused")));
        Assert.Equal(2, metrics.Entries.Count);
        Assert.All(metrics.Entries, static m => Assert.Equal(["xihan.outcome"], m.Tags.Keys));
    }

    [Fact]
    public async Task UnknownOperation_NeverReachesThePipeline_SoItsIdIsNeverATagValue()
    {
        // 请求点名的操作不在登记表里：调用器直接返回未知操作，不进管道。请求里的文本因此到不了 Span 与指标
        var (provider, _) = await StartAsync("telemetry.known");
        using var disposing = provider;
        using var spans = new SpanCapture([FrameworkDiagnostics.SourceName]);
        using var metrics = new MetricCapture([FrameworkDiagnostics.SourceName]);
        metrics.RestrictToCurrentFlow();
        await using var scope = provider.CreateAsyncScope();
        var context = Sampled(scope.ServiceProvider);

        var response = await Invoker(provider).InvokeAsync(context, Request("telemetry.made-up-by-the-caller", "{}"));

        Assert.Equal(KernelErrors.UnknownOperation, response.Outcome.Error?.Code);
        Assert.Empty(spans.Spans(context.TraceId));
        Assert.Empty(metrics.Entries);
    }

    [Fact]
    public async Task NestedInvocationOnTheSameContext_DoesNotRenameTheOuterInvocation()
    {
        // 处理器在同一个上下文上再调一个操作：调用器把里层的描述放进同一个上下文。外层的操作维度在进入时取，不随之改变
        var (provider, app) = await StartAsync("telemetry.inner");
        using var disposing = provider;
        using var spans = new SpanCapture([FrameworkDiagnostics.SourceName]);
        using var metrics = new MetricCapture([FrameworkDiagnostics.SourceName]);
        metrics.RestrictToCurrentFlow();
        await using var scope = provider.CreateAsyncScope();
        var context = Sampled(scope.ServiceProvider);
        var invoker = Invoker(provider);

        var result = await app.InvokeAsync(context, async inner =>
        {
            var response = await invoker.InvokeAsync(inner, Request("telemetry.inner", """{"text":"hi"}"""));
            Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        });

        Assert.True(result.IsSuccess, result.ToString());
        // 里层先结束：它带操作 ID，是外层的子 Span；外层没有操作 ID
        var stopped = spans.Spans(context.TraceId);
        Assert.Equal(2, stopped.Count);
        Assert.Equal("telemetry.inner", stopped[0].GetTagItem("xihan.operation"));
        Assert.Equal(stopped[1].SpanId, stopped[0].ParentSpanId);
        Assert.Null(stopped[1].GetTagItem("xihan.operation"));
        var counts = metrics.Entries.Where(static m => m.Instrument == Invocations).ToList();
        Assert.Equal(2, counts.Count);
        Assert.Equal("telemetry.inner", counts[0].Tags["xihan.operation"]);
        Assert.False(counts[1].Tags.ContainsKey("xihan.operation"));
    }

    [Fact]
    public async Task UnsampledInvocation_StillCountsUnderItsOperation()
    {
        // 上游不采样：没有 Span，指标照记，操作维度照带（指标不随采样丢）
        var (provider, _) = await StartAsync("telemetry.unsampled");
        using var disposing = provider;
        using var spans = new SpanCapture([FrameworkDiagnostics.SourceName]);
        using var metrics = new MetricCapture([FrameworkDiagnostics.SourceName]);
        metrics.RestrictToCurrentFlow();
        await using var scope = provider.CreateAsyncScope();
        var context = App.CreateContext(scope.ServiceProvider, new InvocationContextOptions { TraceId = ActivityTraceId.CreateRandom().ToHexString(), SpanId = UpstreamSpan, TraceFlags = ActivityTraceFlags.None }, CancellationToken.None);

        var response = await Invoker(provider).InvokeAsync(context, Request("telemetry.unsampled", """{"text":"hi"}"""));

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        Assert.Empty(spans.Spans(context.TraceId));
        var counted = Assert.Single(metrics.Entries, static m => m.Instrument == Invocations);
        Assert.Equal("telemetry.unsampled", counted.Tags["xihan.operation"]);
    }

    [Fact]
    public async Task WithoutListeners_NoSpanIsStarted_AndTheInstrumentsAreOff()
    {
        // 这个集合不与别的集合并行，此刻没有任何监听器：不开 Span、仪表关着，调用照常
        var (provider, app) = await StartAsync("telemetry.silent");
        using var disposing = provider;
        await using var scope = provider.CreateAsyncScope();
        Assert.False(FrameworkDiagnostics.ActivitySource.HasListeners());
        Assert.False(FrameworkDiagnostics.Invocations.Enabled);
        Assert.False(FrameworkDiagnostics.InvocationDuration.Enabled);

        var response = await Invoker(provider).InvokeAsync(Sampled(scope.ServiceProvider), Request("telemetry.silent", """{"text":"hi"}"""));
        var current = new Activity("placeholder");
        var result = await app.InvokeAsync(Sampled(scope.ServiceProvider), _ =>
        {
            current = Activity.Current;
            return ValueTask.CompletedTask;
        });

        Assert.True(response.Outcome.IsSuccess, response.Outcome.ToString());
        Assert.True(result.IsSuccess, result.ToString());
        Assert.Null(current);
    }

    /// <summary>
    /// 启动登记了一个回声操作的应用
    /// </summary>
    /// <param name="operationId">操作 ID；每个用例用自己的，登记表里才有它</param>
    private static async Task<(ServiceProvider Provider, App App)> StartAsync(string operationId)
    {
        var (provider, app) = Hosts.BuildApp(b => b.UseFeature(new EchoFeature(operationId: operationId)));
        await app.StartAsync(CancellationToken.None);
        return (provider, app);
    }

    /// <summary>
    /// 带上游采样决定的调用上下文，trace-id 每次新建
    /// </summary>
    private static InvocationContext Sampled(IServiceProvider services)
        => App.CreateContext(services, new InvocationContextOptions { TraceId = ActivityTraceId.CreateRandom().ToHexString(), SpanId = UpstreamSpan, TraceFlags = ActivityTraceFlags.Recorded }, CancellationToken.None);

    /// <summary>
    /// 操作调用器
    /// </summary>
    private static IOperationInvoker Invoker(IServiceProvider provider) => provider.GetRequiredService<IOperationInvoker>();

    /// <summary>
    /// 以 JSON 文本为载荷的请求
    /// </summary>
    private static OperationRequest Request(string operationId, string json) => new(operationId, Encoding.UTF8.GetBytes(json));
}
