// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedulers;

/// <summary>
/// 本节点正在进行的一次运行：记录、取消源与任务。
/// </summary>
internal sealed class RunningRun
{
    /// <summary>
    /// 创建。
    /// </summary>
    public RunningRun(JobRunRecord record, CancellationTokenSource cancellation)
    {
        Record = record;
        Cancellation = cancellation;
    }

    /// <summary>
    /// 运行记录。
    /// </summary>
    public JobRunRecord Record { get; }

    /// <summary>
    /// 取消源：<c>CancelRunsAsync</c> 或宿主停止时取消。
    /// </summary>
    public CancellationTokenSource Cancellation { get; }

    /// <summary>
    /// 运行结束后的收尾。
    /// </summary>
    /// <remarks>
    /// 间隔触发器推算下一次、本地触发器计数；在运行记录标为完成之前执行，只执行一次。
    /// </remarks>
    public Func<DateTimeOffset, ValueTask>? Finish { get; set; }

    /// <summary>
    /// 运行任务。
    /// </summary>
    /// <remarks>
    /// 停止时等待它。
    /// </remarks>
    public Task Task { get; set; } = Task.CompletedTask;
}
