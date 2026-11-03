// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// HTTP 载体的追踪标志与请求 Activity
/// </summary>
/// <remarks>
/// <c>traceparent</c> 的采样标志与 ASP.NET Core 请求 Activity（RFC-0003 规则 7）。监听内核与 ASP.NET Core 的源，按 OpenTelemetry 的 ParentBased 规则采样（有父级随父级，根一律采样），在 Activity 开始时记下，按 trace-id 找本用例的 Activity
/// </remarks>
public sealed class HttpTracingTests
{
    /// <summary>
    /// ASP.NET Core 请求 Activity 所在的源
    /// </summary>
    private const string AspNetCoreSource = "Microsoft.AspNetCore";

    /// <summary>
    /// 捕获最近一次请求的调用上下文
    /// </summary>
    private sealed class Captured
    {
        /// <summary>
        /// 最近一次经过管道的调用上下文
        /// </summary>
        public InvocationContext? Context { get; set; }
    }

    /// <summary>
    /// 把调用上下文写入 <see cref="Captured"/> 后放行的中间件
    /// </summary>
    private sealed class CaptureMiddleware(Captured captured) : IPipelineMiddleware
    {
        /// <inheritdoc />
        public ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
        {
            captured.Context = context;
            return nextStep(context);
        }
    }

    /// <summary>
    /// 开始监听，开始的 Activity 记进 <paramref name="started"/>
    /// </summary>
    private static ActivityListener Listen(ConcurrentQueue<Activity> started)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name is FrameworkDiagnostics.SourceName or AspNetCoreSource,
            Sample = static (ref ActivityCreationOptions<ActivityContext> options) =>
                options.Parent.TraceId == default || (options.Parent.TraceFlags & ActivityTraceFlags.Recorded) != 0
                    ? ActivitySamplingResult.AllDataAndRecorded
                    : ActivitySamplingResult.None,
            ActivityStarted = started.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    /// <summary>
    /// 用 TestServer 启动只装配捕获中间件的应用
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client, Captured Captured)> StartAsync()
    {
        var captured = new Captured();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(captured);
        builder.AddXiHan(static xihan => xihan.UseFeature(new StubFeature(TestManifests.Create("xihan.test.tracing"))
        {
            OnConfigure = static context => context.AddMiddleware<CaptureMiddleware>(PipelineStage.Tenant),
        }));
        var app = builder.Build();
        app.UseXiHan();
        app.MapGet("/ok", () => "ok");
        await app.StartAsync();
        return (app, app.GetTestClient(), captured);
    }

    [Fact]
    public async Task Traceparent_KeepsItsFlags_AndAnUnsampledUpstreamStaysUnsampled()
    {
        var started = new ConcurrentQueue<Activity>();
        using var listener = Listen(started);
        var (app, client, captured) = await StartAsync();
        await using var _ = app;

        // 每次用新的 trace-id：同一程序集里的其他用例并行发请求，固定值会被它们的 Activity 撞上
        var unsampledTrace = ActivityTraceId.CreateRandom().ToHexString();
        var sampledTrace = ActivityTraceId.CreateRandom().ToHexString();

        // 上游不采样（-00）：标志原样进上下文，xihan.invoke 不采样
        using (var unsampled = new HttpRequestMessage(HttpMethod.Get, "/ok"))
        {
            unsampled.Headers.Add("traceparent", $"00-{unsampledTrace}-00f067aa0ba902b7-00");
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(unsampled)).StatusCode);
        }

        Assert.Equal(unsampledTrace, captured.Context!.TraceId);
        Assert.Equal("00f067aa0ba902b7", captured.Context.SpanId);
        Assert.Equal(ActivityTraceFlags.None, captured.Context.TraceFlags);
        Assert.DoesNotContain(started, a => a.OperationName == "xihan.invoke" && a.TraceId.ToHexString() == unsampledTrace && a.Recorded);

        // 上游采样（-01）：xihan.invoke 在同一条链路上采样
        using (var sampled = new HttpRequestMessage(HttpMethod.Get, "/ok"))
        {
            sampled.Headers.Add("traceparent", $"00-{sampledTrace}-b7ad6b7169203331-01");
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(sampled)).StatusCode);
        }

        Assert.Equal(ActivityTraceFlags.Recorded, captured.Context.TraceFlags);
        Assert.Contains(started, a => a.OperationName == "xihan.invoke" && a.TraceId.ToHexString() == sampledTrace && a.Recorded);
    }

    [Fact]
    public async Task WithoutTraceparent_TheInvocationJoinsTheAspNetCoreRequestActivity()
    {
        var started = new ConcurrentQueue<Activity>();
        using var listener = Listen(started);
        var (app, client, captured) = await StartAsync();
        await using var _ = app;

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ok")).StatusCode);

        // 调用上下文的 trace-id 就是 ASP.NET Core 请求 Activity 的，xihan.invoke 是它的子 Span，不再另起一条链路
        var traceId = captured.Context!.TraceId;
        var request = Assert.Single(started, a => a.Source.Name == AspNetCoreSource && a.TraceId.ToHexString() == traceId);
        Assert.Equal(request.SpanId.ToHexString(), captured.Context.SpanId);
        Assert.Equal(ActivityTraceFlags.Recorded, captured.Context.TraceFlags);
        var invoke = Assert.Single(started, a => a.OperationName == "xihan.invoke" && a.TraceId.ToHexString() == traceId);
        Assert.Equal(request.SpanId, invoke.ParentSpanId);
        Assert.True(invoke.Recorded);
    }
}
