// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedules;
using XiHan.Framework.Scheduling.Abstractions.Storage;

namespace XiHan.Framework.Scheduling.Abstractions.Jobs;

/// <summary>
/// 一个触发器的状态。
/// </summary>
/// <param name="Name">触发器名。</param>
/// <param name="Schedule">计划。</param>
/// <param name="State">状态。</param>
/// <param name="NextFireAt">下一次运行时刻；没有为空。</param>
/// <param name="PreviousFireAt">上一次运行时刻；没运行过为空。</param>
/// <param name="FireCount">已运行次数。</param>
/// <param name="Error">状态为 <see cref="TriggerState.Error"/> 时的原因。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobTriggerStatus(string Name, JobSchedule Schedule, TriggerState State, DateTimeOffset? NextFireAt, DateTimeOffset? PreviousFireAt, long FireCount, string? Error);
