// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// Observe 阶段的内置中间件：为每次调用开启 Activity、记录计数与耗时，并把未处理异常转为 <see cref="KernelErrors.Unexpected"/> 结果。
/// </summary>
/// <param name="loggerFactory">日志工厂。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial class ObserveMiddleware(ILoggerFactory loggerFactory) : IPipelineMiddleware
{
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<ObserveMiddleware>();

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);

        using var activity = FrameworkDiagnostics.ActivitySource.StartActivity("xihan.invoke", ActivityKind.Internal, $"00-{context.TraceId}-{context.SpanId}-01");
        var started = Stopwatch.GetTimestamp();
        try
        {
            await nextStep(context).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogUnexpected(_logger, exception, context.TraceId);
            context.SetOutcome(Error.Unexpected(KernelErrors.Unexpected, "调用期间发生未处理异常。", exception is FrameworkException framework ? framework.ToError() : null));
        }
        finally
        {
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

    [LoggerMessage(Level = LogLevel.Error, Message = "调用 {TraceId} 发生未处理异常")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string traceId);
}
