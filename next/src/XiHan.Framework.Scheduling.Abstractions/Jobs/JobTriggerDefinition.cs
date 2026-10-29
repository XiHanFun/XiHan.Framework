// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 触发器定义。
/// </summary>
/// <remarks>
/// 何时运行作业。一个作业可以有多个触发器，各自推算下一次运行。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobTriggerDefinition
{
    /// <summary>
    /// 便利构造生成的触发器名。
    /// </summary>
    public const string DefaultName = "default";

    /// <summary>
    /// 默认优先级。
    /// </summary>
    public const int DefaultPriority = 5;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// 名字须符合作业名文法，计划不能是手动。
    /// </remarks>
    public JobTriggerDefinition(string name, JobSchedule schedule)
    {
        Name = JobName.Validate(name);
        Schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        if (schedule.Kind == JobScheduleKind.Manual)
        {
            throw new ArgumentException($"Trigger '{name}' must not use the manual schedule; a job without triggers is already manual-only.", nameof(schedule));
        }
    }

    /// <summary>
    /// 名字（作业内唯一）。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 计划。
    /// </summary>
    public JobSchedule Schedule { get; }

    /// <summary>
    /// 运行时传给作业的数据，覆盖作业数据的同名键。
    /// </summary>
    public JsonObject? Data { get; init; }

    /// <summary>
    /// 优先级。
    /// </summary>
    /// <remarks>
    /// 到期的触发器多于可运行名额时大者先；默认 5。
    /// </remarks>
    public int Priority { get; init; } = DefaultPriority;

    /// <summary>
    /// misfire 策略。
    /// </summary>
    /// <remarks>
    /// 默认立即运行一次。
    /// </remarks>
    public MisfirePolicy MisfirePolicy { get; init; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; init; }
}
