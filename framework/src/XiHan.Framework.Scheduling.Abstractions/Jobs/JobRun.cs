// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一次运行
/// </summary>
/// <remarks>
/// 成员都经初始化器给出，以后加字段只加属性（ADR-0142、ADR-0148）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobRun
{
    /// <summary>
    /// 运行 ID（32 位十六进制），也是本次调用的关联 ID
    /// </summary>
    /// <exception cref="ArgumentException">为空或空白</exception>
    public required string Id
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    }

    /// <summary>
    /// 作业名
    /// </summary>
    public required JobName Name { get; init; }

    /// <summary>
    /// 触发器名
    /// </summary>
    /// <remarks>
    /// 手动触发为 <see langword="null"/>
    /// </remarks>
    public string? TriggerName { get; init; }

    /// <summary>
    /// 计划的运行时刻（手动触发为触发时刻）
    /// </summary>
    public required DateTimeOffset ScheduledAt { get; init; }

    /// <summary>
    /// 触发来源
    /// </summary>
    public required JobTrigger Trigger { get; init; }

    /// <summary>
    /// 传给作业的数据：作业数据 ← 触发器数据 ← 手动触发的数据逐层覆盖
    /// </summary>
    public JsonObject Data
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = [];

    /// <summary>
    /// 第几次尝试
    /// </summary>
    /// <remarks>
    /// 从 1 起，重试一次加一（ADR-0060）
    /// </remarks>
    public int Attempt
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The attempt starts at 1.");
    } = 1;

    /// <summary>
    /// 重试时为首次运行的 ID
    /// </summary>
    /// <remarks>
    /// 首次运行为空
    /// </remarks>
    public string? RetryOf { get; init; }

    /// <summary>
    /// 生成新的运行 ID
    /// </summary>
    public static string NewId() => Guid.CreateVersion7().ToString("N");

    /// <summary>
    /// 创建一次新运行
    /// </summary>
    public static JobRun Create(JobName name, DateTimeOffset scheduledAt, JobTrigger trigger, string? triggerName = null, JsonObject? data = null)
        => new() { Id = NewId(), Name = name, TriggerName = triggerName, ScheduledAt = scheduledAt, Trigger = trigger, Data = data is null ? [] : (JsonObject)data.DeepClone() };
}
