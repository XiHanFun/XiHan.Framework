// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Schedules;

/// <summary>
/// 5 段 cron 表达式（分 时 日 月 周），UTC，分钟精度。每段支持 <c>*</c>、数值、列表 <c>a,b</c>、范围 <c>a-b</c>、步长 <c>*/n</c> 与 <c>a-b/n</c>；周 0 到 7（0 与 7 都是周日）。
/// 日与周同时限定时任一匹配即可（标准 cron 语义）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CronExpression
{
    /// <summary>
    /// 向前搜索的上限，防止永不匹配的表达式（如 2 月 30 日）无限循环。
    /// </summary>
    private static readonly TimeSpan _searchLimit = TimeSpan.FromDays(366 * 5);

    /// <summary>
    /// 分钟 0 到 59 是否匹配。
    /// </summary>
    private readonly bool[] _minutes;

    /// <summary>
    /// 小时 0 到 23 是否匹配。
    /// </summary>
    private readonly bool[] _hours;

    /// <summary>
    /// 日 1 到 31 是否匹配（下标 0 不用）。
    /// </summary>
    private readonly bool[] _days;

    /// <summary>
    /// 月 1 到 12 是否匹配（下标 0 不用）。
    /// </summary>
    private readonly bool[] _months;

    /// <summary>
    /// 周 0（周日）到 6 是否匹配。
    /// </summary>
    private readonly bool[] _weekdays;

    /// <summary>
    /// 日段是否限定（不是 <c>*</c>）。
    /// </summary>
    private readonly bool _daysRestricted;

    /// <summary>
    /// 周段是否限定（不是 <c>*</c>）。
    /// </summary>
    private readonly bool _weekdaysRestricted;

    /// <summary>
    /// 由解析好的各段创建。
    /// </summary>
    private CronExpression(string expression, bool[] minutes, bool[] hours, bool[] days, bool daysRestricted, bool[] months, bool[] weekdays, bool weekdaysRestricted)
    {
        Expression = expression;
        _minutes = minutes;
        _hours = hours;
        _days = days;
        _daysRestricted = daysRestricted;
        _months = months;
        _weekdays = weekdays;
        _weekdaysRestricted = weekdaysRestricted;
    }

    /// <summary>
    /// 原始表达式（各段以单个空格连接）。
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// 解析；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static CronExpression Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return TryParse(expression, out var parsed) ? parsed : throw new ArgumentException($"Cron expression '{expression}' is invalid: expected 5 fields 'minute hour day month weekday' with values in range.", nameof(expression));
    }

    /// <summary>
    /// 尝试解析。
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? expression, [NotNullWhen(true)] out CronExpression? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != 5)
        {
            return false;
        }

        if (!TryParseField(fields[0], 0, 59, out var minutes)
            || !TryParseField(fields[1], 0, 23, out var hours)
            || !TryParseField(fields[2], 1, 31, out var days)
            || !TryParseField(fields[3], 1, 12, out var months)
            || !TryParseField(fields[4], 0, 7, out var weekdaysRaw))
        {
            return false;
        }

        // 7 与 0 都是周日：折叠到 0
        var weekdays = new bool[7];
        for (var day = 0; day <= 7; day++)
        {
            if (weekdaysRaw[day])
            {
                weekdays[day % 7] = true;
            }
        }

        parsed = new CronExpression(string.Join(' ', fields), minutes, hours, days, fields[2] != "*", months, weekdays, fields[4] != "*");
        return true;
    }

    /// <summary>
    /// <paramref name="after"/> 之后（不含）的下一次匹配时刻（UTC，秒与毫秒为 0）；5 年内没有匹配为无值。
    /// </summary>
    public Maybe<DateTimeOffset> Next(DateTimeOffset after)
    {
        // 从下一个整分钟开始逐级跳过不匹配的月、日、时、分
        var utc = after.UtcDateTime;
        var candidate = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc).AddMinutes(1);
        var limit = candidate + _searchLimit;
        while (candidate < limit)
        {
            if (!_months[candidate.Month])
            {
                candidate = new DateTime(candidate.Year, candidate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                continue;
            }

            if (!DayMatches(candidate))
            {
                candidate = candidate.Date.AddDays(1);
                continue;
            }

            if (!_hours[candidate.Hour])
            {
                candidate = new DateTime(candidate.Year, candidate.Month, candidate.Day, candidate.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                continue;
            }

            if (!_minutes[candidate.Minute])
            {
                candidate = candidate.AddMinutes(1);
                continue;
            }

            return new DateTimeOffset(candidate, TimeSpan.Zero);
        }

        return default;
    }

    /// <inheritdoc />
    public override string ToString() => Expression;

    /// <summary>
    /// 日是否匹配：日与周都限定时任一匹配即可，只限定一个时看那个，都不限定总是匹配。
    /// </summary>
    private bool DayMatches(DateTime candidate)
    {
        var dayMatches = _days[candidate.Day];
        var weekdayMatches = _weekdays[(int)candidate.DayOfWeek];
        return (_daysRestricted, _weekdaysRestricted) switch
        {
            (true, true) => dayMatches || weekdayMatches,
            (true, false) => dayMatches,
            (false, true) => weekdayMatches,
            _ => true,
        };
    }

    /// <summary>
    /// 解析一段：逗号分隔的项，每项是 <c>*</c>、<c>*/n</c>、<c>a</c>、<c>a-b</c>、<c>a-b/n</c> 或 <c>a/n</c>（a 到上限按 n 步进）。
    /// </summary>
    private static bool TryParseField(string field, int min, int max, [NotNullWhen(true)] out bool[]? matches)
    {
        matches = null;
        var result = new bool[max + 1];
        foreach (var item in field.Split(','))
        {
            var parts = item.Split('/');
            if (parts.Length > 2)
            {
                return false;
            }

            var step = 1;
            if (parts.Length == 2 && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out step) || step < 1))
            {
                return false;
            }

            int start;
            int end;
            var range = parts[0];
            if (range == "*")
            {
                start = min;
                end = max;
            }
            else
            {
                var bounds = range.Split('-');
                if (bounds.Length > 2 || !int.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out start))
                {
                    return false;
                }

                if (bounds.Length == 2)
                {
                    if (!int.TryParse(bounds[1], NumberStyles.None, CultureInfo.InvariantCulture, out end))
                    {
                        return false;
                    }
                }
                else
                {
                    // 单值带步长时表示从该值到上限
                    end = parts.Length == 2 ? max : start;
                }
            }

            if (start < min || end > max || start > end)
            {
                return false;
            }

            for (var value = start; value <= end; value += step)
            {
                result[value] = true;
            }
        }

        matches = result;
        return true;
    }
}
