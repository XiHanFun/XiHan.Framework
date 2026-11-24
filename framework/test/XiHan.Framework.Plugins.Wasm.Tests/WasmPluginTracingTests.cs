// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Messaging.Abstractions.Envelopes;
using XiHan.Framework.Messaging.Abstractions.Publishing;
using XiHan.Framework.Messaging.InMemory;
using XiHan.Framework.Plugins.Wasm.Tests.Fixtures;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Plugins.Wasm.Tests;

/// <summary>
/// Wasm 宿主调用插件时的 Span <c>xihan.plugin.invoke</c>
/// </summary>
/// <remarks>
/// 规则出自 <c>spec/plugin/invocation.md</c> §6 与 ADR-0269。以真实的 EchoWasm 模块端到端核对：Span 是内核 <c>xihan.invoke</c> 的子 Span，带插件 ID 与操作 ID；插件收到的 <c>trace_parent</c> 以它为父级，原样带回宿主时成为回调上下文的上游。名字、种类与状态的细则由进程外测试项目里的 <c>PluginTracingTests</c> 核对，两个宿主共用那一处
/// </remarks>
[Collection(nameof(TelemetryGroup))]
public sealed class WasmPluginTracingTests
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

    [Fact]
    public async Task Invoke_IsRecordedUnderTheInvocationSpan_AndThePluginReceivesItAsItsParent()
    {
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature(new StubFeature(TestManifests.Create("xihan.app.traces"))
        {
            OnConfigure = static ctx => ctx.AddOperation<TraceOperation, PingRequest, PingResponse>(new OperationContract("traces.read"), PingJsonContext.Default.PingRequest, PingJsonContext.Default.PingResponse),
        }));
        await using var scope = host.Services.CreateAsyncScope();
        var invoker = scope.ServiceProvider.GetRequiredService<IOperationInvoker>();

        // echoes.relay 把收到的元数据（含 traceParent）原样交给宿主导入 xihan.invoke、回调 traces.read，后者回显回调上下文的上游
        var context = Sampled(scope.ServiceProvider);
        var relayed = await invoker.InvokeAsync(context, Request("echoes.relay", """{"operation":"traces.read","payload":{"name":"traced"}}"""));

        Assert.True(relayed.Outcome.IsSuccess, relayed.Outcome.ToString());
        var plugin = Assert.Single(recorder.Spans(context.TraceId, PluginSpan));
        Assert.Equal((ActivityKind.Client, ActivityStatusCode.Unset), (plugin.Kind, plugin.Status));
        Assert.Equal((EchoWasmPluginHost.PluginId, "echoes.relay"), (plugin.GetTagItem("xihan.plugin.id"), plugin.GetTagItem("xihan.operation")));
        var kernel = recorder.Spans(context.TraceId, KernelSpan);
        var outer = Assert.Single(kernel, static s => (string?)s.GetTagItem("xihan.operation") == "echoes.relay");
        Assert.Equal(outer.SpanId, plugin.ParentSpanId);
        var upstream = JsonDocument.Parse(relayed.Payload).RootElement;
        Assert.Equal(plugin.SpanId.ToHexString(), upstream.GetProperty("name").GetString());
        Assert.Equal(context.TraceId, upstream.GetProperty("principal").GetString());
        // 回调在宿主一侧是又一次调用：它的 xihan.invoke 在同一条链路里，挂在 xihan.plugin.invoke 下面
        var callback = Assert.Single(kernel, static s => (string?)s.GetTagItem("xihan.operation") == "traces.read");
        Assert.Equal(plugin.SpanId, callback.ParentSpanId);

        // 插件报的业务失败：Span 的状态是 Error，说明是插件的错误码
        var failing = Sampled(scope.ServiceProvider);
        var failed = await invoker.InvokeAsync(failing, Request("echoes.create", """{"text":""}"""));
        Assert.True(failed.Outcome.IsFailure);
        var rejected = Assert.Single(recorder.Spans(failing.TraceId, PluginSpan));
        Assert.Equal((ActivityStatusCode.Error, failed.Outcome.Error!.Code), (rejected.Status, rejected.StatusDescription));
    }

    [Fact]
    public async Task EventDelivery_IsRecordedUnderTheDeliveryInvocation_WithoutAnOperationTag()
    {
        using var recorder = new SpanCapture([FrameworkDiagnostics.SourceName]);
        await using var host = await EchoWasmPluginHost.StartAsync(static b => b.UseFeature<InMemoryMessagingFeature>());
        await using var scope = host.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        // 信封自带 traceparent：派发时成为投递那次调用的链路
        var traceId = ActivityTraceId.CreateRandom().ToHexString();
        var envelope = new MessageEnvelope
        {
            Id = "evt-wasm-traced",
            Source = "tests",
            Type = "echo.created",
            Data = "{}"u8.ToArray(),
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal) { [EnvelopeExtensions.TraceParent] = new TraceParent(traceId, UpstreamSpan, true).ToString() },
        };
        var published = await publisher.PublishAsync("echo.events", envelope, CancellationToken.None);
        Assert.True(published.IsSuccess, published.ToString());

        Assert.True(
            await EchoWasmPluginHost.WaitUntilAsync(() => recorder.Spans(traceId, PluginSpan) is [var sent] && recorder.Spans(traceId, KernelSpan).Any(s => s.SpanId == sent.ParentSpanId), TimeSpan.FromSeconds(10)),
            "the delivery invocation that handed the event to the plugin must end");
        var plugin = Assert.Single(recorder.Spans(traceId, PluginSpan));
        Assert.Equal(["xihan.plugin.id"], plugin.TagObjects.Select(static tag => tag.Key));
        Assert.Equal(EchoWasmPluginHost.PluginId, plugin.GetTagItem("xihan.plugin.id"));
        Assert.Equal((ActivityKind.Client, ActivityStatusCode.Unset), (plugin.Kind, plugin.Status));
        // 投递那次调用不是操作调用：它的 xihan.invoke 也没有操作标签
        var delivery = Assert.Single(recorder.Spans(traceId, KernelSpan), s => s.SpanId == plugin.ParentSpanId);
        Assert.Null(delivery.GetTagItem("xihan.operation"));
    }

    /// <summary>
    /// 带上游采样决定的调用上下文，trace-id 每次新建
    /// </summary>
    private static InvocationContext Sampled(IServiceProvider services)
        => App.CreateContext(services, new InvocationContextOptions { TraceId = ActivityTraceId.CreateRandom().ToHexString(), SpanId = UpstreamSpan, TraceFlags = ActivityTraceFlags.Recorded }, CancellationToken.None);

    /// <summary>
    /// 以 JSON 文本为载荷的请求
    /// </summary>
    private static OperationRequest Request(string operationId, string json) => new(operationId, Encoding.UTF8.GetBytes(json));
}
