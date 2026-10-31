// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Identifiers;

/// <summary>
/// 把调用里抛出的雪花标识不可用转为结果。
/// </summary>
/// <remarks>
/// <see cref="SnowflakeIdFeature"/> 登记在 Resilience 阶段末尾：截止时间与熔断之内，幂等与工作单元之外。<see cref="IIdGenerator{TKey}.NewId"/> 返回 <see cref="long"/>，没有机器位（<see cref="PersistenceErrors.WorkerIdUnavailable"/>）或时钟回拨超过阈值（<see cref="PersistenceErrors.ClockMovedBackwards"/>）只能抛出 <see cref="FrameworkException"/>；异常经过内层时工作单元已回滚、幂等标记已清除，到这里转为同码的 UNAVAILABLE 结果，协议适配器映射为可重试的 503，而不是由 Observe 阶段包成 <c>KERNEL_UNEXPECTED_001</c>（500）。其余异常原样抛出（ADR-0063）。
/// </remarks>
/// <param name="loggerFactory">日志工厂。</param>
internal sealed partial class SnowflakeUnavailableMiddleware(ILoggerFactory loggerFactory) : IPipelineMiddleware
{
    /// <summary>
    /// 本中间件的日志器。
    /// </summary>
    private readonly ILogger _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<SnowflakeUnavailableMiddleware>();

    /// <inheritdoc />
    public async ValueTask InvokeAsync(InvocationContext context, PipelineStep nextStep)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(nextStep);
        try
        {
            await nextStep(context).ConfigureAwait(false);
        }
        catch (FrameworkException exception) when (exception.Code is PersistenceErrors.WorkerIdUnavailable or PersistenceErrors.ClockMovedBackwards)
        {
            // 异常不再往外传，Observe 阶段不会记它：这里记一条警告，带上原始异常
            LogUnavailable(_logger, exception, exception.Code, context.TraceId);
            context.SetOutcome(string.Equals(exception.Code, PersistenceErrors.WorkerIdUnavailable, StringComparison.Ordinal)
                ? Error.Unavailable(PersistenceErrors.WorkerIdUnavailable, PersistenceMessages.WorkerIdUnavailable)
                : Error.Unavailable(PersistenceErrors.ClockMovedBackwards, PersistenceMessages.ClockMovedBackwards));
        }
    }

    /// <summary>
    /// 调用因雪花标识不可用而失败。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Snowflake ID generation is unavailable ({Code}) in invocation '{TraceId}'; the invocation fails as unavailable and can be retried.")]
    private static partial void LogUnavailable(ILogger logger, Exception exception, string code, string traceId);
}
