// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Diagnostics;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Kernel.Tests.Fixtures;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Tests.Diagnostics;

/// <summary>
/// Observe 阶段 <c>xihan.invoke</c> 的父级与采样（RFC-0003 规则 7）
/// </summary>
/// <remarks>
/// 监听器按 OpenTelemetry 的 ParentBased 规则采样：有父级随父级的采样位，链路的根按用例给定的根规则。同一类里的用例依次执行，监听器在用例结束时移除
/// </remarks>
public sealed class ObserveTracingTests
{
    /// <summary>
    /// 上游的 trace-id
    /// </summary>
    private const string UpstreamTrace = "4bf92f3577b34da6a3ce929d0e0e4736";

    /// <summary>
    /// 上游的 parent-id
    /// </summary>
    private const string UpstreamSpan = "00f067aa0ba902b7";

    /// <summary>
    /// 扮演宿主的源的名字
    /// </summary>
    /// <remarks>
    /// 监听器按名字比较：创建源时已有的监听器会立刻被询问，那时静态字段还没赋值
    /// </remarks>
    private const string HostSourceName = "XiHan.Framework.Kernel.Tests.Host";

    /// <summary>
    /// 扮演宿主（如 ASP.NET Core）开请求 Span 的源
    /// </summary>
    private static readonly ActivitySource _host = new(HostSourceName);

    /// <summary>
    /// 监听内核与测试宿主的源
    /// </summary>
    /// <remarks>
    /// 有父级时随父级的采样位，没有父级时按 <paramref name="keepRoots"/>
    /// </remarks>
    private static ActivityListener Listen(bool keepRoots)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name is FrameworkDiagnostics.SourceName or HostSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                var sampled = options.Parent.TraceId == default ? keepRoots : (options.Parent.TraceFlags & ActivityTraceFlags.Recorded) != 0;
                return sampled ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    /// <summary>
    /// 启动只有内核的应用
    /// </summary>
    private static async Task<(ServiceProvider Provider, App App)> StartAsync()
    {
        var (provider, app) = Hosts.BuildApp(static _ => { });
        await app.StartAsync(CancellationToken.None);
        return (provider, app);
    }

    /// <summary>
    /// 经完整管道调用，返回处理器里看到的 <c>xihan.invoke</c>
    /// </summary>
    /// <remarks>
    /// 没有开启（采样器不要）时返回 <see langword="null"/>
    /// </remarks>
    private static async Task<Activity?> InvokeAsync(App app, InvocationContext context)
    {
        Activity? seen = null;
        var result = await app.InvokeAsync(context, _ =>
        {
            seen = Activity.Current;
            return ValueTask.CompletedTask;
        });
        Assert.True(result.IsSuccess, result.ToString());
        return seen is { OperationName: "xihan.invoke" } ? seen : null;
    }

    [Fact]
    public async Task UpstreamFlags_AreKept_AndTheUpstreamIsTheRemoteParent()
    {
        var (provider, app) = await StartAsync();
        using var _ = provider;
        using var listener = Listen(keepRoots: true);
        using var scope = provider.CreateScope();

        // 上游采样：远端父级是上游的 parent-id，trace-id 不变
        var sampled = App.CreateContext(scope.ServiceProvider, CancellationToken.None, UpstreamTrace, UpstreamSpan, traceFlags: ActivityTraceFlags.Recorded);
        Assert.Equal(ActivityTraceFlags.Recorded, sampled.TraceFlags);
        var activity = await InvokeAsync(app, sampled);
        Assert.NotNull(activity);
        Assert.True(activity.Recorded);
        Assert.Equal(UpstreamTrace, activity.TraceId.ToHexString());
        Assert.Equal(UpstreamSpan, activity.ParentSpanId.ToHexString());
        Assert.True(activity.HasRemoteParent);

        // 上游不采样：标志原样保留，xihan.invoke 也不采样（根规则要采样也不影响）
        var unsampled = App.CreateContext(scope.ServiceProvider, CancellationToken.None, UpstreamTrace, UpstreamSpan, traceFlags: ActivityTraceFlags.None);
        Assert.Equal(ActivityTraceFlags.None, unsampled.TraceFlags);
        Assert.Null(await InvokeAsync(app, unsampled));

        // 派生的上下文带同一组标志
        Assert.Equal(ActivityTraceFlags.None, unsampled.WithBaggage(BaggageKeys.Correlation, "c").TraceFlags);

        // 只给 trace-id 不给标志的创建方按上游已采样处理
        Assert.Equal(ActivityTraceFlags.Recorded, App.CreateContext(scope.ServiceProvider, CancellationToken.None, UpstreamTrace).TraceFlags);
    }

    [Fact]
    public async Task CurrentActivity_IsJoined_AndBecomesTheParent_ForNestedInvocationsToo()
    {
        var (provider, app) = await StartAsync();
        using var _ = provider;
        using var listener = Listen(keepRoots: true);
        using var scope = provider.CreateScope();

        using var request = _host.StartActivity("request", ActivityKind.Server);
        Assert.NotNull(request);

        // 没有载体的 trace 时并入当前 Activity：trace-id、parent-id 与采样标志都取它的
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        Assert.Equal(request.TraceId.ToHexString(), context.TraceId);
        Assert.Equal(request.SpanId.ToHexString(), context.SpanId);
        Assert.Equal(ActivityTraceFlags.Recorded, context.TraceFlags);

        Activity? outer = null;
        Activity? inner = null;
        var result = await app.InvokeAsync(context, async ctx =>
        {
            outer = Activity.Current;
            // 同一作用域里嵌套的调用：内层 xihan.invoke 是外层的子 Span
            await app.InvokeAsync(ctx.WithBaggage(BaggageKeys.Correlation, "inner"), _ =>
            {
                inner = Activity.Current;
                return ValueTask.CompletedTask;
            });
        });

        Assert.True(result.IsSuccess, result.ToString());
        Assert.NotNull(outer);
        Assert.Equal("xihan.invoke", outer.OperationName);
        Assert.Same(request, outer.Parent);
        Assert.Equal(request.SpanId, outer.ParentSpanId);
        Assert.Equal(request.TraceId, outer.TraceId);
        Assert.NotNull(inner);
        Assert.Same(outer, inner.Parent);
        Assert.Same(request, Activity.Current);
    }

    [Fact]
    public async Task KernelRoot_IsARootSpan_WithTheContextTraceId_AndFollowsTheRootRule()
    {
        var (provider, app) = await StartAsync();
        using var _ = provider;
        using var scope = provider.CreateScope();
        Assert.Null(Activity.Current);

        using (Listen(keepRoots: true))
        {
            // 没有上游：标志为空，xihan.invoke 是根 Span，trace-id 与上下文一致、没有父级
            var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
            Assert.Null(context.TraceFlags);
            var activity = await InvokeAsync(app, context);
            Assert.NotNull(activity);
            Assert.True(activity.Recorded);
            Assert.Equal(context.TraceId, activity.TraceId.ToHexString());
            Assert.Equal(default, activity.ParentSpanId);
            Assert.Null(activity.ParentId);
            Assert.False(activity.HasRemoteParent);
        }

        // 根规则不要：不再因为固定的采样位被采样
        using (Listen(keepRoots: false))
        {
            Assert.Null(await InvokeAsync(app, App.CreateContext(scope.ServiceProvider, CancellationToken.None)));
        }
    }

    [Fact]
    public async Task LegacyHostActivity_WithoutSamplingDecision_IsJoined_ButSampledAsARoot()
    {
        var (provider, app) = await StartAsync();
        using var _ = provider;
        using var scope = provider.CreateScope();

        // 不经 ActivitySource 直接构造的根 Activity（如没有监听器时 ASP.NET Core 为日志开的请求 Activity）：标志只是缺省值，不代表不采样
        using var legacy = new Activity("legacy").Start();
        Assert.Equal(ActivityTraceFlags.None, legacy.ActivityTraceFlags);
        var context = App.CreateContext(scope.ServiceProvider, CancellationToken.None);
        Assert.Equal(legacy.TraceId.ToHexString(), context.TraceId);
        Assert.Null(context.TraceFlags);

        using (Listen(keepRoots: true))
        {
            var activity = await InvokeAsync(app, context);
            Assert.NotNull(activity);
            Assert.True(activity.Recorded);
            Assert.Equal(legacy.TraceId, activity.TraceId);
            Assert.Equal(default, activity.ParentSpanId);
        }

        using (Listen(keepRoots: false))
        {
            Assert.Null(await InvokeAsync(app, App.CreateContext(scope.ServiceProvider, CancellationToken.None)));
        }

        Assert.Same(legacy, Activity.Current);
    }
}
