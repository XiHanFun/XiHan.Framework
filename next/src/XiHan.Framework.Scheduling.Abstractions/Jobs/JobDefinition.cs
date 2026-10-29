// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 作业定义：名字、作业类型、触发器、数据与运行方式。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobDefinition
{
    /// <summary>
    /// 创建没有触发器（只能手动触发）的作业。
    /// </summary>
    /// <remarks>
    /// <paramref name="jobType"/> 必须实现 <see cref="IJob"/>。
    /// </remarks>
    public JobDefinition(JobName name, Type jobType)
    {
        Name = name;
        JobType = jobType ?? throw new ArgumentNullException(nameof(jobType));
        if (!typeof(IJob).IsAssignableFrom(jobType))
        {
            throw new ArgumentException($"Job type '{jobType}' must implement {nameof(IJob)}.", nameof(jobType));
        }
    }

    /// <summary>
    /// 创建带一个触发器（名为 <see cref="JobTriggerDefinition.DefaultName"/>）的作业。
    /// </summary>
    /// <remarks>
    /// 手动计划即没有触发器。
    /// </remarks>
    public JobDefinition(JobName name, JobSchedule schedule, Type jobType)
        : this(name, jobType)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        Triggers = schedule.Kind == JobScheduleKind.Manual ? [] : [new JobTriggerDefinition(JobTriggerDefinition.DefaultName, schedule)];
    }

    /// <summary>
    /// 名字。
    /// </summary>
    public JobName Name { get; }

    /// <summary>
    /// 作业类型（实现 <see cref="IJob"/>，须已登记）。
    /// </summary>
    public Type JobType { get; }

    /// <summary>
    /// 触发器。
    /// </summary>
    /// <remarks>
    /// 空为只手动触发。名字重复是定义不合法（<see cref="SchedulingErrors.DefinitionInvalid"/>）。
    /// </remarks>
    public IReadOnlyList<JobTriggerDefinition> Triggers { get; init; } = [];

    /// <summary>
    /// 运行时传给作业的数据（<see cref="JobRun.Data"/>），被触发器与手动触发的数据逐层覆盖。
    /// </summary>
    public JsonObject? Data { get; init; }

    /// <summary>
    /// 允许多次运行重叠。
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="false"/>：整个集群同一时刻最多一次运行，按计划的触发被阻塞到上一次结束、手动触发返回 <see cref="SchedulingErrors.JobRunning"/>。
    /// </remarks>
    public bool AllowConcurrent { get; init; }

    /// <summary>
    /// 每个实例各自按本地时钟运行。
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="false"/>：按计划的触发在整个集群只运行一次。
    /// </remarks>
    public bool RunOnEveryInstance { get; init; }

    /// <summary>
    /// 运行所在节点失联时由领导者重新入队一次（<see cref="Schedulers.JobTrigger.Recovery"/>）。
    /// </summary>
    /// <remarks>
    /// 默认 <see langword="false"/>。
    /// </remarks>
    public bool RequestsRecovery { get; init; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; init; }
}
