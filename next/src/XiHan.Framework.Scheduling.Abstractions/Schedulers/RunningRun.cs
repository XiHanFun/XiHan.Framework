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
    /// 运行任务；停止时等待它。
    /// </summary>
    public Task Task { get; set; } = Task.CompletedTask;
}
