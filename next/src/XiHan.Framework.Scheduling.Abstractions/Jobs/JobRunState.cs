// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 运行记录的状态。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum JobRunState
{
    /// <summary>
    /// 运行中。
    /// </summary>
    Running = 0,

    /// <summary>
    /// 成功。
    /// </summary>
    Succeeded = 1,

    /// <summary>
    /// 失败（作业返回失败或抛出异常）。
    /// </summary>
    Failed = 2,

    /// <summary>
    /// 被取消（<see cref="Schedulers.IJobScheduler.CancelRunsAsync"/> 或宿主停止）。
    /// </summary>
    Canceled = 3,

    /// <summary>
    /// 运行所在节点失联，结果未知。
    /// </summary>
    Lost = 4,
}
