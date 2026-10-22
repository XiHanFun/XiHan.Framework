// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions;

/// <summary>
/// 一次运行的触发来源。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum JobTrigger
{
    /// <summary>按计划。</summary>
    Schedule = 0,

    /// <summary>手动触发（<see cref="IJobScheduler.TriggerAsync"/>）。</summary>
    Manual = 1,
}
