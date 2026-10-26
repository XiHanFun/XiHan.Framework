// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Calendars;

/// <summary>
/// 排除日历：判断某个时刻是否不允许运行；推算下一次运行时跳过被排除的时刻。按名登记（<c>services.AddJobCalendar</c>），计划以 <c>Calendar</c> 引用。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IJobCalendar
{
    /// <summary>
    /// <paramref name="at"/> 是否被排除。
    /// </summary>
    bool IsExcluded(DateTimeOffset at);
}
