// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions.Calendars;

/// <summary>
/// 排除与 cron 表达式匹配的时刻（秒粒度）的日历；时区缺省 UTC。例如 <c>* * 0-7 * * *</c> 排除每天 0 点到 8 点。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CronCalendar : IJobCalendar
{
    /// <summary>
    /// 判断用的时区。
    /// </summary>
    private readonly TimeZoneInfo? _timeZone;

    /// <summary>
    /// 创建。
    /// </summary>
    public CronCalendar(CronExpression expression, TimeZoneInfo? timeZone = null)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        _timeZone = timeZone;
    }

    /// <summary>
    /// 匹配即排除的表达式。
    /// </summary>
    public CronExpression Expression { get; }

    /// <inheritdoc />
    public bool IsExcluded(DateTimeOffset at)
    {
        // 时刻按秒截断；它匹配表达式当且仅当它是前一秒之后的第一个匹配时刻
        var second = new DateTimeOffset(at.Year, at.Month, at.Day, at.Hour, at.Minute, at.Second, at.Offset);
        var next = _timeZone is null ? Expression.Next(second.AddSeconds(-1)) : Expression.Next(second.AddSeconds(-1), _timeZone);
        return next.TryGetValue(out var match) && match == second;
    }
}
