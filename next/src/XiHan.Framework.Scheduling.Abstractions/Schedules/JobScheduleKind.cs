// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Schedules;

/// <summary>
/// 计划的种类。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum JobScheduleKind
{
    /// <summary>只手动触发。</summary>
    Manual = 0,

    /// <summary>固定间隔：上一次完成后再等一个间隔。</summary>
    Interval = 1,

    /// <summary>cron 表达式（默认 UTC，可指定时区）。</summary>
    Cron = 2,

    /// <summary>指定时刻运行一次。</summary>
    Once = 3,
}
