// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// Observe 阶段的内置中间件。
/// </summary>
/// <remarks>
/// 为每次调用开启 Activity、记录计数与耗时，并把未处理异常转为 <see cref="KernelErrors.Unexpected"/> 结果。
/// </remarks>
/// <param name="loggerFactory">日志工厂。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class ObserveMiddleware(ILoggerFactory loggerFactory) : IPipelineMiddleware
{
    /// <summary>
    /// 本中间件的日志器。
    /// </summary>
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<ObserveMiddleware>();

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        // 用上下文里的 trace-id 与 parent-id 拼出 W3C traceparent，让 Activity 挂到上游链路而不是另起一条。
        using var activity = FrameworkDiagnostics.ActivitySource.StartActivity("xihan.invoke", ActivityKind.Internal, $"00-{context.TraceId}-{context.SpanId}-01");
        var started = Stopwatch.GetTimestamp();
        try
        {
            await nextStep(context).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 取消不在这里吞掉：由 Resilience 阶段的 DeadlineMiddleware 区分超时与外部取消并写入结果。
            LogUnexpected(_logger, exception, context.TraceId);
            context.SetOutcome(Error.Unexpected(KernelErrors.Unexpected, KernelMessages.UnhandledException, exception is FrameworkException framework ? framework.ToError() : null));
        }
        finally
        {
            // 无论成功、失败还是取消都记指标；结果未设置时标记为 unset，便于发现没有写结果的管道。
            var outcome = context.GetOutcome();
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
    /// 记录调用期间的未处理异常。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception during invocation '{TraceId}'.")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string traceId);
}
