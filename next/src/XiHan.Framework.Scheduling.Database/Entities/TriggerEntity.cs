// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Database.Entities;

/// <summary>
/// 触发器行：<c>Id</c> 是 <c>作业名/触发器名</c>（<c>TriggerKey.ToString()</c>），计划以 <c>JobSchedule.ToString()</c> 的文本持久化，数据是 JSON 文本，状态、misfire 策略与触发来源存枚举的整数值。
/// 并发戳让多个节点对同一条触发器的抢占是原子的比较并交换：先改成功的拿到，其余得到冲突。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class TriggerEntity : IEntity<string>, IHasConcurrencyStamp
{
    /// <inheritdoc />
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 作业名。
    /// </summary>
    public string Job { get; set; } = string.Empty;

    /// <summary>
    /// 触发器名。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 计划文本。
    /// </summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>
    /// 覆盖作业数据的数据，JSON 对象文本；长文本列。
    /// </summary>
    public string Data { get; set; } = "{}";

    /// <summary>
    /// 优先级。
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// misfire 策略（<c>MisfirePolicy</c> 的整数值）。
    /// </summary>
    public int MisfirePolicy { get; set; }

    /// <summary>
    /// 说明。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 状态（<c>TriggerState</c> 的整数值）。
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// 下一次运行时刻；没有为空。
    /// </summary>
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
    /// 状态为错误时的原因。
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 本地触发器：不参与抢占。
    /// </summary>
    public bool Local { get; set; }

    /// <summary>
    /// 运行一次后删除。
    /// </summary>
    public bool Transient { get; set; }

    /// <summary>
    /// 由它产生的运行的触发来源（<c>JobTrigger</c> 的整数值）。
    /// </summary>
    public int RunAs { get; set; }

    /// <inheritdoc />
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
