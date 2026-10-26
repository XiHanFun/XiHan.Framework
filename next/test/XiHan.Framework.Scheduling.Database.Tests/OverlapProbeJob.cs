// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 多节点用例的作业：停留 150 毫秒后把所在节点（引擎选项里的节点 ID）与开始、结束时刻记进 <see cref="OverlapLog"/>。
/// </summary>
internal sealed class OverlapProbeJob : IJob
{
    /// <summary>
    /// 每次运行停留的时间。
    /// </summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// 记录。
    /// </summary>
    private readonly OverlapLog _log;

    /// <summary>
    /// 本节点的引擎选项。
    /// </summary>
    private readonly SchedulerOptions _options;

    /// <summary>
    /// 创建。
    /// </summary>
    public OverlapProbeJob(OverlapLog log, SchedulerOptions options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask<Result> RunAsync(JobRun run, InvocationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var started = DateTimeOffset.UtcNow;
        await Task.Delay(Duration, cancellationToken);
        _log.Record(_options.NodeId, run.Id, started, DateTimeOffset.UtcNow);
        return Result.Success();
    }
}
