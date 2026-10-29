// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions.Storage;

/// <summary>
/// 触发器记录：定义的持久化形态加运行期状态。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class TriggerRecord
{
    /// <summary>
    /// 作业名。
    /// </summary>
    public required JobName Job { get; set; }

    /// <summary>
    /// 触发器名。
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 计划。
    /// </summary>
    public required JobSchedule Schedule { get; set; }

    /// <summary>
    /// 覆盖作业数据的数据。
    /// </summary>
    public JsonObject Data { get; set; } = [];

    /// <summary>
    /// 优先级。
    /// </summary>
    public int Priority { get; set; } = JobTriggerDefinition.DefaultPriority;

    /// <summary>
    /// misfire 策略。
    /// </summary>
    public MisfirePolicy MisfirePolicy { get; set; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 状态。
    /// </summary>
    public TriggerState State { get; set; }

    /// <summary>
    /// 下一次运行时刻。
    /// </summary>
    /// <remarks>
    /// 没有为空。
    /// </remarks>
    public DateTimeOffset? NextFireAt { get; set; }

    /// <summary>
    /// 上一次运行时刻。
    /// </summary>
    public DateTimeOffset? PreviousFireAt { get; set; }

    /// <summary>
    /// 抢占它的节点。
    /// </summary>
    public string? AcquiredBy { get; set; }

    /// <summary>
    /// 抢占时刻。
    /// </summary>
    public DateTimeOffset? AcquiredAt { get; set; }

    /// <summary>
    /// 已运行次数。
    /// </summary>
    public long FireCount { get; set; }

    /// <summary>
    /// 状态为 <see cref="TriggerState.Error"/> 时的原因。
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 本地触发器：作业 <see cref="JobRecord.RunOnEveryInstance"/> 时为真，不参与抢占。
    /// </summary>
    public bool Local { get; set; }

    /// <summary>
    /// 运行一次后删除（手动入队与恢复用的一次性触发器）。
    /// </summary>
    public bool Transient { get; set; }

    /// <summary>
    /// 由它产生的运行的触发来源。
    /// </summary>
    public JobTrigger RunAs { get; set; }

    /// <summary>
    /// 键。
    /// </summary>
    public TriggerKey Key => new(Job, Name);

    /// <summary>
    /// 状态视图。
    /// </summary>
    public JobTriggerStatus ToStatus() => new(Name, Schedule, State, NextFireAt, PreviousFireAt, FireCount, Error);

    /// <summary>
    /// 深拷贝。
    /// </summary>
    public TriggerRecord Clone() => new()
    {
        Job = Job,
        Name = Name,
        Schedule = Schedule,
        Data = (JsonObject)Data.DeepClone(),
        Priority = Priority,
        MisfirePolicy = MisfirePolicy,
        Description = Description,
        State = State,
        NextFireAt = NextFireAt,
        PreviousFireAt = PreviousFireAt,
        AcquiredBy = AcquiredBy,
        AcquiredAt = AcquiredAt,
        FireCount = FireCount,
        Error = Error,
        Local = Local,
        Transient = Transient,
        RunAs = RunAs,
    };
}
