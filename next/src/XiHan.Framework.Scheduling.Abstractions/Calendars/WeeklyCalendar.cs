// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Calendars;

/// <summary>
/// 排除每周指定星期几（整天）的日历。
/// </summary>
/// <remarks>
/// 星期按给定时区的墙上日期判断，缺省 UTC。缺省排除周六与周日。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class WeeklyCalendar : IJobCalendar
{
    /// <summary>
    /// 被排除的星期。
    /// </summary>
    private readonly HashSet<DayOfWeek> _days;

    /// <summary>
    /// 判断日期用的时区。
    /// </summary>
    private readonly TimeZoneInfo _timeZone;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// <paramref name="excludedDays"/> 为空时排除周六与周日。
    /// </remarks>
    public WeeklyCalendar(IEnumerable<DayOfWeek>? excludedDays = null, TimeZoneInfo? timeZone = null)
    {
        _days = excludedDays is null ? [DayOfWeek.Saturday, DayOfWeek.Sunday] : [.. excludedDays];
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
    }

    /// <summary>
    /// 被排除的星期。
    /// </summary>
    public IReadOnlyCollection<DayOfWeek> ExcludedDays => _days.Order().ToList();

    /// <inheritdoc />
    public bool IsExcluded(DateTimeOffset at) => _days.Contains(TimeZoneInfo.ConvertTime(at, _timeZone).DayOfWeek);
}
