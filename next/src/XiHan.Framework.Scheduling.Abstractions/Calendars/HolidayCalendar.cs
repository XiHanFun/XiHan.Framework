// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Calendars;

/// <summary>
/// 排除指定日期（整天）的日历。
/// </summary>
/// <remarks>
/// 日期按给定时区的墙上日期判断，缺省 UTC。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HolidayCalendar : IJobCalendar
{
    /// <summary>
    /// 被排除的日期。
    /// </summary>
    private readonly HashSet<DateOnly> _dates;

    /// <summary>
    /// 判断日期用的时区。
    /// </summary>
    private readonly TimeZoneInfo _timeZone;

    /// <summary>
    /// 创建。
    /// </summary>
    public HolidayCalendar(IEnumerable<DateOnly> dates, TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(dates);
        _dates = [.. dates];
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
    }

    /// <summary>
    /// 被排除的日期（升序）。
    /// </summary>
    public IReadOnlyCollection<DateOnly> Dates => _dates.Order().ToList();

    /// <inheritdoc />
    public bool IsExcluded(DateTimeOffset at) => _dates.Contains(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, _timeZone).DateTime));
}
