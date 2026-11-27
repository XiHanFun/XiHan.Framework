// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Diagnostics;

/// <summary>
/// Observe 阶段的内置中间件
/// </summary>
/// <remarks>
/// 为每次调用开启 Activity <c>xihan.invoke</c>、记录计数与耗时，并把未处理异常转为 <see cref="KernelErrors.Unexpected"/> 结果。XGI 入口经完整管道的调用在这里、排在后面的中间件都结束之后编码响应，再定结果，写不出时 Span 与指标照调用方拿到的记失败（ADR-0341）。Activity 的父级与采样按 RFC-0003 规则 7（<see cref="FrameworkDiagnostics.StartActivity"/>）。Span 与两个指标都带 <c>xihan.outcome</c>；操作调用另带 <c>xihan.operation</c>（操作 ID），不是操作调用时没有这个标签（ADR-0269）
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

        // 操作 ID 在进入时取：调用器与宿主在管道开始前把描述放进上下文；处理器若在同一个上下文上再发起调用会换掉它，结束时再读就成了里层的操作
        // 取的是登记过的描述的 ID，不是请求里的文本；不是操作调用时为空，Span 与指标都不带这个标签
        var operation = context.FindOperation().TryGetValue(out var descriptor) ? descriptor.Id : null;

        // XGI 入口的调用同样在进入时认出：处理器在同一个上下文上再发起的调用会换掉上下文里的处理器；只有操作调用才会是它
        var call = operation is null ? null : OperationCall.Find(context);
        using var activity = StartActivity(context, operation);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await nextStep(context).ConfigureAwait(false);

            // 响应在排在后面的中间件都结束之后、定结果之前编码：写不出时下面照换过的结果记失败，与调用方拿到的一致（RFC-0023「内核的遥测」，ADR-0341）
            call?.Encode(context);
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
            Record(operation, status, started);
            activity?.SetTag(DiagnosticTags.Outcome, status);
            if (outcome.TryGetValue(out var result) && result.IsFailure)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Error!.Code);
            }
        }
    }

    /// <summary>
    /// 记一次调用的计数与耗时
    /// </summary>
    /// <remarks>
    /// 标签以值类型传给仪表、不装箱也不建数组；两个仪表都没人订阅时连耗时也不算
    /// </remarks>
    private static void Record(string? operation, string status, long started)
    {
        var counted = FrameworkDiagnostics.Invocations.Enabled;
        var timed = FrameworkDiagnostics.InvocationDuration.Enabled;
        if (!counted && !timed)
        {
            return;
        }

        var outcome = new KeyValuePair<string, object?>(DiagnosticTags.Outcome, status);
        var elapsed = timed ? Stopwatch.GetElapsedTime(started).TotalMilliseconds : 0;
        if (operation is null)
        {
            // 不是操作调用：不带操作标签，也不拿占位值顶替
            if (counted)
            {
                FrameworkDiagnostics.Invocations.Add(1, outcome);
            }

            if (timed)
            {
                FrameworkDiagnostics.InvocationDuration.Record(elapsed, outcome);
            }

            return;
        }

        var tagged = new KeyValuePair<string, object?>(DiagnosticTags.Operation, operation);
        if (counted)
        {
            FrameworkDiagnostics.Invocations.Add(1, tagged, outcome);
        }

        if (timed)
        {
            FrameworkDiagnostics.InvocationDuration.Record(elapsed, tagged, outcome);
        }
    }

    /// <summary>
    /// 开启本次调用的 Activity，操作调用在创建时就带上操作 ID
    /// </summary>
    /// <remarks>
    /// 没有监听器时不建标签数组，直接返回 <see langword="null"/>；采样器不要时同样返回 <see langword="null"/>
    /// </remarks>
    private static Activity? StartActivity(InvocationContext context, string? operation)
        => FrameworkDiagnostics.ActivitySource.HasListeners()
            ? FrameworkDiagnostics.StartActivity(context, ActivityName, ActivityKind.Internal, operation is null ? null : [new(DiagnosticTags.Operation, operation)])
            : null;

    /// <summary>
    /// 记录调用期间的未处理异常
    /// </summary>
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception during invocation '{TraceId}'.")]
    private static partial void LogUnexpected(ILogger logger, Exception exception, string traceId);
}
