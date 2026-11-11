// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// Observe 阶段的内置中间件
/// </summary>
/// <remarks>
/// 为每次调用开启 Activity、记录计数与耗时，并把未处理异常转为 <see cref="KernelErrors.Unexpected"/> 结果。Activity 的父级与采样按 RFC-0003 规则 7：当前 Activity 与上下文同一 trace 且有采样决定时作为它的子 Span；否则上下文有标志时以上游为远端父级，采样随上游；标志为空时是链路的根 Span，trace-id 用上下文的，采样交给采样器的根规则
/// </remarks>
/// <param name="loggerFactory">日志工厂</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class ObserveMiddleware(ILoggerFactory loggerFactory) : IPipelineMiddleware
{
    /// <summary>
    /// 本次调用的 Activity 名
    /// </summary>
    private const string ActivityName = "xihan.invoke";

    /// <summary>
    /// 本中间件的日志器
    /// </summary>
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<ObserveMiddleware>();

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        using var activity = StartActivity(context);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await nextStep(context).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 取消不在这里吞掉：由 Resilience 阶段的 DeadlineMiddleware 区分超时与外部取消并写入结果
            LogUnexpected(_logger, exception, context.TraceId);
            context.SetOutcome(Error.Unexpected(KernelErrors.Unexpected, KernelMessages.UnhandledException, null, exception is FrameworkException framework ? framework.ToError() : null));
        }
        finally
        {
            // 无论成功、失败还是取消都记指标；结果未设置时标记为 unset，便于发现没有写结果的管道
            var outcome = context.FindOutcome();
            var status = outcome.Match(static r => r.IsSuccess ? "success" : "failure", static () => "unset");
            var tag = new KeyValuePair<string, object?>("xihan.outcome", status);
            FrameworkDiagnostics.Invocations.Add(1, tag);
            FrameworkDiagnostics.InvocationDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tag);
            activity?.SetTag("xihan.outcome", status);
            if (outcome.TryGetValue(out var result) && result.IsFailure)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error!.Code);
            }
        }
    }

    /// <summary>
    /// 按 RFC-0003 规则 7 选父级并开启本次调用的 Activity
    /// </summary>
    /// <remarks>
    /// 没有监听器或采样器不要时返回 <see langword="null"/>
    /// </remarks>
    private static Activity? StartActivity(InvocationContext context)
    {
        var source = FrameworkDiagnostics.ActivitySource;
        if (!source.HasListeners())
        {
            return null;
        }

        // 宿主当前的 Activity 就是这条链路上的 Span（ASP.NET Core 的请求 Span、外层调用）：作为子 Span，采样随它
        if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } current
            && FrameworkDiagnostics.HasSamplingDecision(current)
            && string.Equals(current.TraceId.ToHexString(), context.TraceId, StringComparison.Ordinal))
        {
            return source.StartActivity(ActivityName, ActivityKind.Internal);
        }

        var traceId = ActivityTraceId.CreateFromString(context.TraceId);

        // 有上游：以它的 trace-id、parent-id 与标志为远端父级，上游「不采样」就不采样
        if (context.TraceFlags is { } flags)
        {
            return source.StartActivity(ActivityName, ActivityKind.Internal, new ActivityContext(traceId, ActivitySpanId.CreateFromString(context.SpanId), flags, isRemote: true));
        }

        // 链路的根：先按根规则问一次采样器（只创建、不开始），再以上下文的 trace-id、没有父级 Span 开启，
        // 这样 SamplingRatio 生效，导出的 trace-id 也与 Problem Details、审计与日志里的一致
        // 当前若有没有采样决定的 Activity（旧式的根），先摘掉它：否则采样器把它当父级，Activity.Start 也会把它挂成父级
        // 本方法只在 InvokeAsync 这个异步方法里同步调用，对 Activity.Current 的改动在它返回时由异步状态机还原，不影响调用方
        var ambient = Activity.Current;
        Activity.Current = null;
        var probe = source.CreateActivity(ActivityName, ActivityKind.Internal, default(ActivityContext));
        var decided = probe?.ActivityTraceFlags & ActivityTraceFlags.Recorded ?? ActivityTraceFlags.None;
        var activity = source.StartActivity(ActivityName, ActivityKind.Internal, new ActivityContext(traceId, default, decided));
        if (activity is null)
        {
            // 没有开启：调用内的代码仍看到原来的当前 Activity
            Activity.Current = ambient;
        }

        return activity;
    }

    /// <summary>
    /// 记录调用期间的未处理异常
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception during invocation '{TraceId}'.")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string traceId);
}
