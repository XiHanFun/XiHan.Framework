// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 作业定义：名字、计划、作业类型（实现 <see cref="IJob"/> 的作用域服务），以及运行约束。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobDefinition
{
    /// <summary>
    /// 创建；<paramref name="jobType"/> 必须实现 <see cref="IJob"/>。
    /// </summary>
    public JobDefinition(JobName name, JobSchedule schedule, Type jobType)
    {
        Name = name;
        Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        JobType = jobType ?? throw new ArgumentNullException(nameof(jobType));
        if (!typeof(IJob).IsAssignableFrom(jobType))
        {
            throw new ArgumentException($"Job type '{jobType}' must implement {nameof(IJob)}.", nameof(jobType));
        }
    }

    /// <summary>
    /// 名字。
    /// </summary>
    public JobName Name { get; }

    /// <summary>
    /// 计划。
    /// </summary>
    public JobSchedule Schedule { get; }

    /// <summary>
    /// 作业类型。
    /// </summary>
    public Type JobType { get; }

    /// <summary>
    /// 只在持有领导租约的实例上按计划运行；手动触发不受影响。默认 <see langword="true"/>。
    /// </summary>
    public bool LeaderOnly { get; init; } = true;

    /// <summary>
    /// 允许多次运行重叠；默认 <see langword="false"/>：上一次未结束时按计划的运行跳过、手动触发返回 <see cref="SchedulingErrors.JobRunning"/>。
    /// </summary>
    public bool AllowConcurrent { get; init; }
}
