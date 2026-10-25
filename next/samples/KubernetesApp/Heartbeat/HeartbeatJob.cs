// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace KubernetesApp.Heartbeat;

/// <summary>
/// 每 10 秒一次的心跳作业；<c>LeaderOnly</c>（默认）让它只在持有租约的 Pod 上按计划运行。
/// </summary>
public sealed partial class HeartbeatJob(HeartbeatCounter counter, ILogger<HeartbeatJob> logger) : IJob
{
    /// <inheritdoc />
    public ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
    {
        var tick = counter.Increment();
        var pod = Environment.MachineName;
        LogHeartbeat(logger, tick, run.Id, pod);
        return ValueTask.FromResult(Result.Success());
    }

    /// <summary>
    /// 一次心跳。
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "Heartbeat {Tick} (run {RunId}) on {Pod}.")]
    private static partial void LogHeartbeat(ILogger logger, long tick, string runId, string pod);
}
