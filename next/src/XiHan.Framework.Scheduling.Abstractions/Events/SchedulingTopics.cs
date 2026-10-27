// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Events;

/// <summary>
/// 调度事件的主题（装配了 <c>messaging.queue</c> 时发布）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchedulingTopics
{
    /// <summary>
    /// CloudEvents 的 <c>Source</c>。
    /// </summary>
    public const string Source = "/scheduling";

    /// <summary>
    /// 作业登记或替换。
    /// </summary>
    public const string JobScheduled = "scheduling.job.scheduled";

    /// <summary>
    /// 作业删除。
    /// </summary>
    public const string JobUnscheduled = "scheduling.job.unscheduled";

    /// <summary>
    /// 作业暂停。
    /// </summary>
    public const string JobPaused = "scheduling.job.paused";

    /// <summary>
    /// 作业恢复。
    /// </summary>
    public const string JobResumed = "scheduling.job.resumed";

    /// <summary>
    /// 运行开始。
    /// </summary>
    public const string RunStarted = "scheduling.run.started";

    /// <summary>
    /// 运行成功。
    /// </summary>
    public const string RunSucceeded = "scheduling.run.succeeded";

    /// <summary>
    /// 运行失败。
    /// </summary>
    public const string RunFailed = "scheduling.run.failed";

    /// <summary>
    /// 运行取消。
    /// </summary>
    public const string RunCanceled = "scheduling.run.canceled";

    /// <summary>
    /// 运行所在节点失联。
    /// </summary>
    public const string RunLost = "scheduling.run.lost";
}
