// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Conformance;

/// <summary>
/// 套件作业：按 <see cref="RecordingJobLog"/> 的开关记录、停留、失败或抛出。
/// </summary>
/// <remarks>
/// 运行数据里的 <c>failBeforeAttempt</c>（第几次尝试之前都失败）与 <c>delayMilliseconds</c>（停留多久，响应取消）只作用于这次运行。停留按注入的 <see cref="TimeProvider"/> 计时：套件用手动时钟，作业停在那里直到用例推进时钟或取消。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RecordingJob : IJob
{
    /// <summary>
    /// 记录。
    /// </summary>
    private readonly RecordingJobLog _log;

    /// <summary>
    /// 停留计时用的时钟。
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// 创建。
    /// </summary>
    public RecordingJob(RecordingJobLog log, TimeProvider clock)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public async ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(context);
        var stay = _log.Delay;
        if (run.Data["delayMilliseconds"] is JsonValue delay && delay.TryGetValue<int>(out var milliseconds))
        {
            stay += TimeSpan.FromMilliseconds(milliseconds);
        }

        // 先挂上计时器再记录：用例看到记录就推进时钟，计时器若在推进之后才创建，就从推进后的时刻起算、错过这次推进
        var staying = stay > TimeSpan.Zero ? Task.Delay(stay, _clock, cancellationToken) : Task.CompletedTask;
        _log.Record(run, context.TryGetBaggage(BaggageKeys.Correlation, out var correlation) ? correlation : null, context.GetOrigin().TryGetValue(out var origin) ? origin : null);
        await staying.ConfigureAwait(false);

        if (run.Data["failBeforeAttempt"] is JsonValue fail && fail.TryGetValue<int>(out var attempt) && run.Attempt < attempt)
        {
            return Error.Conflict("CONFORMANCE_CONFLICT_002", "conformance.job.attempt_failed");
        }

        if (_log.Throw)
        {
            throw new InvalidOperationException("The conformance job was asked to throw.");
        }

        return _log.FailWith is { } error ? error : Result.Success();
    }
}
