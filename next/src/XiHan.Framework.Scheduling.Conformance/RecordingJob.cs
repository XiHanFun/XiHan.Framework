// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// 套件作业：按 <see cref="RecordingJobLog"/> 的开关记录、停留、失败或抛出。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RecordingJob : IJob
{
    /// <summary>
    /// 记录。
    /// </summary>
    private readonly RecordingJobLog _log;

    /// <summary>
    /// 创建。
    /// </summary>
    public RecordingJob(RecordingJobLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public async ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _log.Record(run, context.TryGetBaggage(BaggageKeys.Correlation, out var correlation) ? correlation : null);
        if (_log.Delay > TimeSpan.Zero)
        {
            await Task.Delay(_log.Delay, cancellationToken).ConfigureAwait(false);
        }

        if (_log.Throw)
        {
            throw new InvalidOperationException("The conformance job was asked to throw.");
        }

        return _log.FailWith is { } error ? error : Result.Success();
    }
}
