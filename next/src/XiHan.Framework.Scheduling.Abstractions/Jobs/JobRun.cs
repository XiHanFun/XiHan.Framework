// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Nodes;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一次运行。
/// </summary>
/// <param name="Id">运行 ID（32 位十六进制），也是本次调用的关联 ID。</param>
/// <param name="Name">作业名。</param>
/// <param name="TriggerName">触发器名；手动触发为空。</param>
/// <param name="ScheduledAt">计划的运行时刻（手动触发为触发时刻）。</param>
/// <param name="Trigger">触发来源。</param>
/// <param name="Data">传给作业的数据：作业数据 ← 触发器数据 ← 手动触发的数据逐层覆盖。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobRun(string Id, JobName Name, string? TriggerName, DateTimeOffset ScheduledAt, JobTrigger Trigger, JsonObject Data)
{
    /// <summary>
    /// 生成新的运行 ID。
    /// </summary>
    public static string NewId() => Guid.CreateVersion7().ToString("N");

    /// <summary>
    /// 创建一次新运行。
    /// </summary>
    public static JobRun Create(JobName name, DateTimeOffset scheduledAt, JobTrigger trigger, string? triggerName = null, JsonObject? data = null)
        => new(NewId(), name, triggerName, scheduledAt, trigger, data is null ? [] : (JsonObject)data.DeepClone());
}
