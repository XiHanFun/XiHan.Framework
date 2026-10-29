// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Calendars;

/// <summary>
/// 排除每天一个时段（<see cref="From"/> 含、<see cref="To"/> 不含）的日历。
/// </summary>
/// <remarks>
/// 时段按给定时区的墙上时间判断，缺省 UTC。<see cref="From"/> 晚于 <see cref="To"/> 表示跨午夜。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class DailyCalendar : IJobCalendar
{
    /// <summary>
    /// 判断时间用的时区。
    /// </summary>
    private readonly TimeZoneInfo _timeZone;

    /// <summary>
    /// 创建。
    /// </summary>
    /// <remarks>
    /// <paramref name="from"/> 与 <paramref name="to"/> 相等是编程错误。
    /// </remarks>
    public DailyCalendar(TimeOnly from, TimeOnly to, TimeZoneInfo? timeZone = null)
    {
        if (from == to)
        {
            throw new ArgumentException("The excluded range must not be empty.", nameof(to));
        }

        From = from;
        To = to;
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
    }

    /// <summary>
    /// 时段开始（含）。
    /// </summary>
    public TimeOnly From { get; }

    /// <summary>
    /// 时段结束（不含）。
    /// </summary>
    public TimeOnly To { get; }

    /// <inheritdoc />
    public bool IsExcluded(DateTimeOffset at)
    {
        var time = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, _timeZone).DateTime);
        // 跨午夜的时段拆成两段：[From, 24:00) 与 [00:00, To)
        return From < To ? time >= From && time < To : time >= From || time < To;
    }
}
