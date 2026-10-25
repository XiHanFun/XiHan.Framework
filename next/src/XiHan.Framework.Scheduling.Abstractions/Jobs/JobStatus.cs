// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 作业的状态。
/// </summary>
/// <param name="Definition">定义。</param>
/// <param name="NextRun">下一次按计划运行的时刻；手动计划或调度器未运行时为 <see langword="null"/>。</param>
/// <param name="Last">最近一次运行；没有运行过为 <see langword="null"/>。</param>
/// <param name="RunningCount">正在运行的次数。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobStatus(JobDefinition Definition, DateTimeOffset? NextRun, JobOutcome? Last, int RunningCount)
{
    /// <summary>
    /// 作业名。
    /// </summary>
    public JobName Name => Definition.Name;

    /// <summary>
    /// 是否正在运行。
    /// </summary>
    public bool IsRunning => RunningCount > 0;
}
