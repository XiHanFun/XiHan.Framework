// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// 内核的诊断源
/// </summary>
/// <remarks>
/// 直接基于 <see cref="System.Diagnostics"/>，OpenTelemetry 桥接包只需订阅同名的 ActivitySource 与 Meter
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class FrameworkDiagnostics
{
    /// <summary>
    /// ActivitySource 与 Meter 的名称
    /// </summary>
    public const string SourceName = "XiHan.Framework";

    /// <summary>
    /// 内核的 ActivitySource
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(SourceName, FrameworkInfo.Version);

    /// <summary>
    /// 内核的 Meter
    /// </summary>
    public static Meter Meter { get; } = new(SourceName, FrameworkInfo.Version);

    /// <summary>
    /// 调用计数，按操作与结果标记
    /// </summary>
    /// <remarks>
    /// 标签 <c>xihan.outcome</c>（<c>success</c> / <c>failure</c> / <c>unset</c>）与 <c>xihan.operation</c>（操作 ID；只有操作调用带，不是操作调用时没有这个标签，ADR-0269）
    /// </remarks>
    public static Counter<long> Invocations { get; } = Meter.CreateCounter<long>("xihan.invocations", "{invocation}", "Number of pipeline invocations.");

    /// <summary>
    /// 调用耗时直方图
    /// </summary>
    /// <remarks>
    /// 标签同 <see cref="Invocations"/>
    /// </remarks>
    public static Histogram<double> InvocationDuration { get; } = Meter.CreateHistogram<double>("xihan.invocation.duration", "ms", "Duration of pipeline invocations.");

    /// <summary>
    /// Activity 的采样标志是否是一次采样决定
    /// </summary>
    /// <remarks>
    /// 经具名 ActivitySource 创建（监听器做过采样）或有父级（标志随父级）时是；不经 ActivitySource 直接构造的根 Activity（如没有监听器时 ASP.NET Core 为日志创建的请求 Activity）的标志只是缺省值，不代表「不采样」
    /// </remarks>
    internal static bool HasSamplingDecision(Activity activity)
        => activity.Source.Name.Length > 0 || activity.ParentSpanId != default;

    /// <summary>
    /// 在一次调用的链路里开启一个 Span
    /// </summary>
    /// <remarks>
    /// <para>父级与采样按 RFC-0003 规则 7：当前 Activity 与上下文同一 trace 且有采样决定时作为它的子 Span；否则上下文有标志时以上游为远端父级，采样随上游；标志为空时是链路的根 Span，trace-id 用上下文的，采样交给采样器的根规则。</para>
    /// <para>Observe 阶段的 <c>xihan.invoke</c> 与插件宿主的 <c>xihan.plugin.invoke</c> 共用这一处，后者因此总在调用的链路里：有 <c>xihan.invoke</c> 时是它的子 Span，没有（应用关掉了内置中间件）时按同一规则挂到上游（ADR-0269）。</para>
    /// <para>没有监听器或采样器不要时返回 <see langword="null"/>。只能在异步方法里同步调用：链路的根要先摘掉没有采样决定的当前 Activity，对 <see cref="Activity.Current"/> 的改动在那个异步方法返回时由异步状态机还原，不影响它的调用方。</para>
    /// </remarks>
    /// <param name="context">调用上下文</param>
    /// <param name="name">Span 名</param>
    /// <param name="kind">Span 的种类</param>
    /// <param name="tags">创建时就带上的标签，采样器看得到；调用方先查 <see cref="ActivitySource.HasListeners"/>，有监听器才建这个数组</param>
    internal static Activity? StartActivity(InvocationContext context, string name, ActivityKind kind, KeyValuePair<string, object?>[]? tags)
    {
        var source = ActivitySource;
        if (!source.HasListeners())
        {
            return null;
        }

        // 宿主当前的 Activity 就是这条链路上的 Span（ASP.NET Core 的请求 Span、外层调用）：作为子 Span，采样随它
        if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } current
            && HasSamplingDecision(current)
            && string.Equals(current.TraceId.ToHexString(), context.TraceId, StringComparison.Ordinal))
        {
            // 父级上下文给缺省值即以当前 Activity 为父级
            return source.StartActivity(name, kind, default(ActivityContext), tags);
        }

        var traceId = ActivityTraceId.CreateFromString(context.TraceId);

        // 有上游：以它的 trace-id、parent-id 与标志为远端父级，上游「不采样」就不采样
        if (context.TraceFlags is { } flags)
        {
            return source.StartActivity(name, kind, new ActivityContext(traceId, ActivitySpanId.CreateFromString(context.SpanId), flags, isRemote: true), tags);
        }

        // 链路的根：先按根规则问一次采样器（只创建、不开始），再以上下文的 trace-id、没有父级 Span 开启，
        // 这样 SamplingRatio 生效，导出的 trace-id 也与 Problem Details、审计与日志里的一致
        // 当前若有没有采样决定的 Activity（旧式的根），先摘掉它：否则采样器把它当父级，Activity.Start 也会把它挂成父级
        var ambient = Activity.Current;
        Activity.Current = null;
        var probe = source.CreateActivity(name, kind, default(ActivityContext), tags);
        var decided = probe?.ActivityTraceFlags & ActivityTraceFlags.Recorded ?? ActivityTraceFlags.None;
        var activity = source.StartActivity(name, kind, new ActivityContext(traceId, default, decided), tags);
        if (activity is null)
        {
            // 没有开启：调用内的代码仍看到原来的当前 Activity
            Activity.Current = ambient;
        }

        return activity;
    }
}
