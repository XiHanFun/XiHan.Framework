// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 调度子系统的能力 ID 与接口版本。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchedulingCapabilities
{
    /// <summary>
    /// 作业：登记、按计划运行、手动触发、状态，<see cref="IJobScheduler"/>。
    /// </summary>
    public const string Jobs = "scheduling.jobs";

    /// <summary>
    /// 领导选举：租约，<see cref="ILeaderElection"/>。
    /// </summary>
    public const string Leader = "scheduling.leader";

    /// <summary>
    /// 当前接口版本。
    /// </summary>
    public const int Version = 1;
}
