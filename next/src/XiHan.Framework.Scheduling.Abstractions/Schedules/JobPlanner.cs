// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Calendars;

namespace XiHan.Framework.Scheduling.Abstractions.Schedules;

/// <summary>
/// 计划推算：在 <see cref="JobSchedule.NextRun"/> 之上套用排除日历与次数上限。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class JobPlanner
{
    /// <summary>
    /// 跳过被排除时刻的最大尝试次数：防止日历排除一切时无限循环。
    /// </summary>
    private const int MaxCalendarSkips = 10_000;

    /// <summary>
    /// <paramref name="after"/> 之后首个未被 <paramref name="calendar"/> 排除的运行时刻；已运行 <paramref name="fireCount"/> 次且计划限次用尽时无值。
    /// </summary>
    public static Maybe<DateTimeOffset> NextFire(JobSchedule schedule, DateTimeOffset after, IJobCalendar? calendar, long fireCount = 0)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (schedule.RepeatCount is { } repeat && fireCount >= repeat)
        {
            return default;
        }

        var cursor = after;
        for (var attempt = 0; attempt < MaxCalendarSkips; attempt++)
        {
            var next = schedule.NextRun(cursor);
            if (!next.TryGetValue(out var candidate))
            {
                return default;
            }

            if (calendar is null || !calendar.IsExcluded(candidate))
            {
                return candidate;
            }

            // 被排除：一次性计划没有别的时刻；其他计划从该时刻往后找
            if (schedule.Kind == JobScheduleKind.Once)
            {
                return default;
            }

            cursor = candidate;
        }

        return default;
    }
}
