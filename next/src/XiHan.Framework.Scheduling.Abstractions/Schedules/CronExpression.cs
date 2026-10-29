// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Schedules;

/// <summary>
/// cron 表达式，秒精度。
/// </summary>
/// <remarks>
/// 5 段 <c>分 时 日 月 周</c>（秒视为 0）、6 段 <c>秒 分 时 日 月 周</c>，或 7 段再加 <c>年</c>（1970 到 2099）。每段支持 <c>*</c>、数值、列表 <c>a,b</c>、范围 <c>a-b</c>、步长 <c>*/n</c>、<c>a/n</c>（从 a 起到上限每 n）与 <c>a-b/n</c>；日与周还可写 <c>?</c>（等价于 <c>*</c>）；月可写 <c>JAN</c> 到 <c>DEC</c>，周可写 <c>SUN</c> 到 <c>SAT</c>，均不区分大小写。周按 Unix 约定：0 与 7 都是周日、1 是周一（不采用 Quartz 的 1 = 周日）。日段特殊写法：<c>L</c>（当月最后一天）、<c>L-n</c>（最后一天往前 n 天）、<c>nW</c>（离 n 日最近的工作日，不跨月）、<c>LW</c>（最后一个工作日）；周段特殊写法：<c>nL</c> / <c>FRIL</c>（当月最后一个周 n）、<c>n#k</c> / <c>FRI#2</c>（当月第 k 个周 n，k 为 1 到 5，不存在时该月不触发）。日与周同时限定时任一匹配即可（标准 cron 语义，不要求其一为 <c>?</c>）。下一次匹配默认按 UTC；<see cref="Next(DateTimeOffset, TimeZoneInfo)"/> 按指定时区的墙上时间匹配，夏令时跳过的墙上时间不触发、重复的墙上时间只取第一次出现。向前最多搜索 5 年，没有匹配即无值。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class CronExpression
{
    /// <summary>
    /// 年段的下限。
    /// </summary>
    private const int MinYear = 1970;

    /// <summary>
    /// 年段的上限。
    /// </summary>
    private const int MaxYear = 2099;

    /// <summary>
    /// <see cref="DateTime"/> 能表示的最大年份。
    /// </summary>
    /// <remarks>
    /// 游标越过它就不再构造日期。
    /// </remarks>
    private const int MaxCalendarYear = 9999;

    /// <summary>
    /// 向前搜索的上限，防止永不匹配的表达式（如 2 月 30 日）无限循环。
    /// </summary>
    private static readonly TimeSpan _searchLimit = TimeSpan.FromDays(366 * 5);

    /// <summary>
    /// 分隔各段的空白字符。
    /// </summary>
    private static readonly char[] _whitespace = [' ', '\t'];

    /// <summary>
    /// 秒 0 到 59 是否匹配。
    /// </summary>
    /// <remarks>
    /// 5 段表达式只有 0 匹配。
    /// </remarks>
    private readonly bool[] _seconds;

    /// <summary>
    /// 分钟 0 到 59 是否匹配。
    /// </summary>
    private readonly bool[] _minutes;

    /// <summary>
    /// 小时 0 到 23 是否匹配。
    /// </summary>
    private readonly bool[] _hours;

    /// <summary>
    /// 月 1 到 12 是否匹配（下标 = 月 − 1）。
    /// </summary>
    private readonly bool[] _months;

    /// <summary>
    /// 年 1970 到 2099 是否匹配（下标 = 年 − 1970）。
    /// </summary>
    /// <remarks>
    /// 没有年段时为 <see langword="null"/>，任何年都匹配。
    /// </remarks>
    private readonly bool[]? _years;

    /// <summary>
    /// 日段的匹配规则。
    /// </summary>
    private readonly MonthDayRule _monthDays;

    /// <summary>
    /// 周段的匹配规则。
    /// </summary>
    private readonly WeekdayRule _weekdays;

    /// <summary>
    /// 由解析完成的各段创建。
    /// </summary>
    /// <param name="parser">已成功解析的解析器。</param>
    private CronExpression(Parser parser)
    {
        Expression = parser.Expression;
        HasSeconds = parser.HasSeconds;
        IncludesYears = parser.IncludesYears;
        _seconds = parser.Seconds;
        _minutes = parser.Minutes;
        _hours = parser.Hours;
        _months = parser.Months;
        _years = parser.Years;
        _monthDays = parser.MonthDays;
        _weekdays = parser.Weekdays;
    }

    /// <summary>
    /// 规范化的表达式文本：各段以单个空格连接，保留书写时的大小写。
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// 是否带秒段（6 段或 7 段写法）。
    /// </summary>
    public bool HasSeconds { get; }

    /// <summary>
    /// 是否带年段（7 段写法）。
    /// </summary>
    public bool IncludesYears { get; }

    /// <summary>
    /// 解析。
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>，消息说明具体原因（段数、段名、越界值或未知标记）。
    /// </remarks>
    /// <param name="expression">5、6 或 7 段的 cron 表达式。</param>
    public static CronExpression Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return TryParseCore(expression, out var parsed, out var reason)
            ? parsed
            : throw new ArgumentException($"Cron expression '{expression}' is invalid: {reason}.", nameof(expression));
    }

    /// <summary>
    /// 尝试解析。
    /// </summary>
    /// <remarks>
    /// 不抛出。
    /// </remarks>
    /// <param name="expression">5、6 或 7 段的 cron 表达式。</param>
    /// <param name="parsed">解析结果；失败时为 <see langword="null"/>。</param>
    public static bool TryParse([NotNullWhen(true)] string? expression, [NotNullWhen(true)] out CronExpression? parsed)
    {
        return TryParseCore(expression, out parsed, out _);
    }

    /// <summary>
    /// <paramref name="after"/> 之后（不含）的下一次匹配时刻（UTC，毫秒为 0）。
    /// </summary>
    /// <remarks>
    /// 5 年内没有匹配为无值。
    /// </remarks>
    /// <param name="after">起点，不含。</param>
    public Maybe<DateTimeOffset> Next(DateTimeOffset after)
    {
        return Next(after, TimeZoneInfo.Utc);
    }

    /// <summary>
    /// <paramref name="after"/> 之后（不含）按 <paramref name="timeZone"/> 的墙上时间匹配的下一次时刻。
    /// </summary>
    /// <remarks>
    /// 带该时区在该时刻的偏移，毫秒为 0。5 年内没有匹配为无值。夏令时开始时被跳过的墙上时间不触发；夏令时结束时重复出现的墙上时间只取第一次出现。
    /// </remarks>
    /// <param name="after">起点，不含。</param>
    /// <param name="timeZone">匹配墙上时间所用的时区。</param>
    public Maybe<DateTimeOffset> Next(DateTimeOffset after, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        // 在目标时区的墙上时间里搜索，从 after 的下一个整秒开始；临近日历上限时把搜索上限截到 DateTime.MaxValue
        var wall = TimeZoneInfo.ConvertTime(after, timeZone).DateTime;
        var startTicks = wall.Ticks - (wall.Ticks % TimeSpan.TicksPerSecond) + TimeSpan.TicksPerSecond;
        if (startTicks > DateTime.MaxValue.Ticks)
        {
            return default;
        }

        var start = new DateTime(startTicks, DateTimeKind.Unspecified);
        var limit = startTicks > DateTime.MaxValue.Ticks - _searchLimit.Ticks ? DateTime.MaxValue : start + _searchLimit;
        var cursor = new Cursor(start);
        while (TryAdvanceDate(cursor, limit))
        {
            if (!TryAdvanceTime(cursor))
            {
                continue;
            }

            // 夏令时开始时被跳过的墙上时间并不存在，不能作为触发点
            var candidate = cursor.ToDateTime();
            if (timeZone.IsInvalidTime(candidate))
            {
                cursor.MoveToNextSecond();
                continue;
            }

            // after 落在秋季回拨后的第二次出现时，墙上时间在它之后的候选换算成时刻可能反而不晚于 after
            var instant = new DateTimeOffset(candidate, ResolveOffset(candidate, timeZone));
            if (instant <= after)
            {
                cursor.MoveToNextSecond();
                continue;
            }

            return instant;
        }

        return default;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Expression;
    }

    /// <summary>
    /// 解析的公共入口：失败时给出原因。
    /// </summary>
    /// <param name="expression">表达式文本。</param>
    /// <param name="parsed">解析结果；失败时为 <see langword="null"/>。</param>
    /// <param name="reason">失败原因（英文、不含句号）；成功时为 <see langword="null"/>。</param>
    private static bool TryParseCore(string? expression, [NotNullWhen(true)] out CronExpression? parsed, [NotNullWhen(false)] out string? reason)
    {
        parsed = null;
        var fields = expression is null ? [] : expression.Split(_whitespace, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length is < 5 or > 7)
        {
            reason = fields.Length == 0 ? "the expression is empty" : $"expected 5, 6 or 7 fields but found {fields.Length}";
            return false;
        }

        var parser = new Parser(fields);
        reason = parser.Parse();
        if (reason is not null)
        {
            return false;
        }

        parsed = new CronExpression(parser);
        return true;
    }

    /// <summary>
    /// 把游标推进到年、月、日都匹配的第一天（时分秒归零或保持）。
    /// </summary>
    /// <remarks>
    /// 到搜索上限或年段用尽仍没有就返回 <see langword="false"/>。
    /// </remarks>
    private bool TryAdvanceDate(Cursor cursor, DateTime limit)
    {
        // 逐级跳过：年不匹配跳到下一个匹配年的 1 月 1 日，月不匹配跳到下一个匹配月的 1 日，日不匹配跳到当月下一个匹配日；永不逐秒遍历
        while (cursor.IsBefore(limit))
        {
            var year = NextMatchingYear(cursor.Year);
            if (year < 0)
            {
                return false;
            }

            if (year != cursor.Year)
            {
                cursor.MoveToYear(year);
                continue;
            }

            var month = NextSet(_months, cursor.Month - 1) + 1;
            if (month == 0)
            {
                cursor.MoveToYear(cursor.Year + 1);
                continue;
            }

            if (month != cursor.Month)
            {
                cursor.MoveToMonth(month);
                continue;
            }

            var day = NextDay(DayMask(cursor.Year, cursor.Month), cursor.Day);
            if (day < 0)
            {
                cursor.MoveToNextMonth();
                continue;
            }

            if (day != cursor.Day)
            {
                cursor.MoveToDay(day);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 在当天内把游标推进到时、分、秒都匹配的时刻。
    /// </summary>
    /// <remarks>
    /// 当天剩余时间没有匹配就把游标推到下一个小时、分钟或天的起点并返回 <see langword="false"/>。
    /// </remarks>
    private bool TryAdvanceTime(Cursor cursor)
    {
        var hour = NextSet(_hours, cursor.Hour);
        if (hour < 0)
        {
            cursor.MoveToNextDay();
            return false;
        }

        if (hour != cursor.Hour)
        {
            cursor.MoveToHour(hour);
        }

        var minute = NextSet(_minutes, cursor.Minute);
        if (minute < 0)
        {
            cursor.MoveToNextHour();
            return false;
        }

        if (minute != cursor.Minute)
        {
            cursor.MoveToMinute(minute);
        }

        var second = NextSet(_seconds, cursor.Second);
        if (second < 0)
        {
            cursor.MoveToNextMinute();
            return false;
        }

        cursor.MoveToSecond(second);
        return true;
    }

    /// <summary>
    /// 不早于 <paramref name="from"/> 的第一个匹配年。
    /// </summary>
    /// <remarks>
    /// 没有年段时就是 <paramref name="from"/> 本身，年段用尽为 −1。
    /// </remarks>
    private int NextMatchingYear(int from)
    {
        if (_years is null)
        {
            return from;
        }

        for (var year = Math.Max(from, MinYear); year <= MaxYear; year++)
        {
            if (_years[year - MinYear])
            {
                return year;
            }
        }

        return -1;
    }

    /// <summary>
    /// 某年某月匹配的日的位掩码（第 n 位表示 n 日）。
    /// </summary>
    /// <remarks>
    /// 日与周都限定时取并集（标准 cron），只限定一个时只看它，都不限定时整月都匹配。
    /// </remarks>
    private uint DayMask(int year, int month)
    {
        return (_monthDays.Restricted, _weekdays.Restricted) switch
        {
            (true, true) => _monthDays.Resolve(year, month) | _weekdays.Resolve(year, month),
            (true, false) => _monthDays.Resolve(year, month),
            (false, true) => _weekdays.Resolve(year, month),
            _ => (uint)((1UL << (DateTime.DaysInMonth(year, month) + 1)) - 2),
        };
    }

    /// <summary>
    /// <paramref name="set"/> 中不小于 <paramref name="from"/> 的第一个匹配下标。
    /// </summary>
    /// <remarks>
    /// 没有为 −1。
    /// </remarks>
    private static int NextSet(bool[] set, int from)
    {
        for (var index = from; index < set.Length; index++)
        {
            if (set[index])
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// 位掩码中不小于 <paramref name="from"/> 的第一个匹配日。
    /// </summary>
    /// <remarks>
    /// 没有为 −1。
    /// </remarks>
    private static int NextDay(uint mask, int from)
    {
        for (var day = from; day <= 31; day++)
        {
            if ((mask & (1u << day)) != 0)
            {
                return day;
            }
        }

        return -1;
    }

    /// <summary>
    /// 墙上时间对应的 UTC 偏移。
    /// </summary>
    /// <remarks>
    /// 秋季回拨造成的重复墙上时间取偏移较大的那次，即第一次出现。
    /// </remarks>
    private static TimeSpan ResolveOffset(DateTime wall, TimeZoneInfo timeZone)
    {
        if (!timeZone.IsAmbiguousTime(wall))
        {
            return timeZone.GetUtcOffset(wall);
        }

        var offsets = timeZone.GetAmbiguousTimeOffsets(wall);
        var first = offsets[0];
        foreach (var offset in offsets)
        {
            if (offset > first)
            {
                first = offset;
            }
        }

        return first;
    }

    /// <summary>
    /// 一段的名字、取值范围与可用的名称（月份或星期）。
    /// </summary>
    /// <param name="name">出现在错误原因里的段名。</param>
    /// <param name="min">最小值。</param>
    /// <param name="max">最大值。</param>
    /// <param name="names">按值顺序排列的名称，没有为 <see langword="null"/>。</param>
    private sealed class Field(string name, int min, int max, string[]? names)
    {
        /// <summary>
        /// 出现在错误原因里的段名。
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// 最小值。
        /// </summary>
        public int Min { get; } = min;

        /// <summary>
        /// 最大值。
        /// </summary>
        public int Max { get; } = max;

        /// <summary>
        /// 按值顺序排列的名称（下标 0 对应 <see cref="Min"/>），没有为 <see langword="null"/>。
        /// </summary>
        public string[]? Names { get; } = names;

        /// <summary>
        /// 取值个数。
        /// </summary>
        public int Count => Max - Min + 1;
    }

    /// <summary>
    /// 日段规则：普通日、<c>L</c> / <c>L-n</c>、<c>nW</c> 与 <c>LW</c>。
    /// </summary>
    private sealed class MonthDayRule
    {
        /// <summary>
        /// 日段是否限定（不是 <c>*</c> 或 <c>?</c>）。
        /// </summary>
        public bool Restricted { get; set; }

        /// <summary>
        /// 普通日 1 到 31 是否匹配（下标 = 日 − 1）。
        /// </summary>
        public bool[] Days { get; } = new bool[31];

        /// <summary>
        /// <c>L-n</c> 的 n（0 到 30）是否出现（下标 = n）。
        /// </summary>
        public bool[] LastDayOffsets { get; } = new bool[31];

        /// <summary>
        /// <c>nW</c> 的 n（1 到 31）是否出现（下标 = n − 1）。
        /// </summary>
        public bool[] NearestWeekdays { get; } = new bool[31];

        /// <summary>
        /// 是否出现 <c>LW</c>。
        /// </summary>
        public bool LastWeekday { get; set; }

        /// <summary>
        /// 某年某月匹配的日的位掩码（第 n 位表示 n 日）。
        /// </summary>
        /// <remarks>
        /// 不存在的日（如 4 月 31 日）不计入。
        /// </remarks>
        public uint Resolve(int year, int month)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            uint mask = 0;
            for (var day = 1; day <= daysInMonth; day++)
            {
                if (Days[day - 1])
                {
                    mask |= 1u << day;
                }

                if (NearestWeekdays[day - 1])
                {
                    mask |= 1u << NearestWeekday(year, month, day, daysInMonth);
                }
            }

            for (var back = 0; back < daysInMonth; back++)
            {
                if (LastDayOffsets[back])
                {
                    mask |= 1u << (daysInMonth - back);
                }
            }

            if (LastWeekday)
            {
                mask |= 1u << NearestWeekday(year, month, daysInMonth, daysInMonth);
            }

            return mask;
        }

        /// <summary>
        /// 离某日最近的工作日，不跨月。
        /// </summary>
        /// <remarks>
        /// 周六取前一天的周五（1 日除外，取 3 日周一），周日取后一天的周一（月末除外，取前两天的周五）。
        /// </remarks>
        private static int NearestWeekday(int year, int month, int day, int daysInMonth)
        {
            return new DateTime(year, month, day).DayOfWeek switch
            {
                DayOfWeek.Saturday => day == 1 ? 3 : day - 1,
                DayOfWeek.Sunday => day == daysInMonth ? day - 2 : day + 1,
                _ => day,
            };
        }
    }

    /// <summary>
    /// 周段规则。
    /// </summary>
    /// <remarks>
    /// 普通周、<c>nL</c> 与 <c>n#k</c>；周 0 是周日。
    /// </remarks>
    private sealed class WeekdayRule
    {
        /// <summary>
        /// 周段是否限定（不是 <c>*</c> 或 <c>?</c>）。
        /// </summary>
        public bool Restricted { get; set; }

        /// <summary>
        /// 普通周 0（周日）到 6 是否匹配。
        /// </summary>
        public bool[] Days { get; } = new bool[7];

        /// <summary>
        /// <c>nL</c>：当月最后一个周 n 是否匹配。
        /// </summary>
        public bool[] LastDays { get; } = new bool[7];

        /// <summary>
        /// <c>n#k</c>：当月第 k 个周 n 是否匹配（下标 = (k − 1) × 7 + n）。
        /// </summary>
        public bool[] NthDays { get; } = new bool[35];

        /// <summary>
        /// 某年某月匹配的日的位掩码（第 n 位表示 n 日）。
        /// </summary>
        public uint Resolve(int year, int month)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var firstWeekday = (int)new DateTime(year, month, 1).DayOfWeek;
            uint mask = 0;
            for (var day = 1; day <= daysInMonth; day++)
            {
                var weekday = (firstWeekday + day - 1) % 7;
                var ordinal = (day - 1) / 7;
                if (Days[weekday] || (LastDays[weekday] && day + 7 > daysInMonth) || NthDays[(ordinal * 7) + weekday])
                {
                    mask |= 1u << day;
                }
            }

            return mask;
        }
    }

    /// <summary>
    /// 搜索中的墙上时间游标。
    /// </summary>
    /// <remarks>
    /// 推进到某一级时把更细的字段归零，跨界时自动进位。年可能越过 9999，使用前先经 <see cref="IsBefore"/> 检查。
    /// </remarks>
    private sealed class Cursor
    {
        /// <summary>
        /// 从起点创建。
        /// </summary>
        public Cursor(DateTime start)
        {
            Year = start.Year;
            Month = start.Month;
            Day = start.Day;
            Hour = start.Hour;
            Minute = start.Minute;
            Second = start.Second;
        }

        /// <summary>
        /// 年。
        /// </summary>
        public int Year { get; private set; }

        /// <summary>
        /// 月 1 到 12。
        /// </summary>
        public int Month { get; private set; }

        /// <summary>
        /// 日 1 到 31。
        /// </summary>
        public int Day { get; private set; }

        /// <summary>
        /// 时 0 到 23。
        /// </summary>
        public int Hour { get; private set; }

        /// <summary>
        /// 分 0 到 59。
        /// </summary>
        public int Minute { get; private set; }

        /// <summary>
        /// 秒 0 到 59。
        /// </summary>
        public int Second { get; private set; }

        /// <summary>
        /// 游标是否仍在日历范围内且早于 <paramref name="limit"/>。
        /// </summary>
        public bool IsBefore(DateTime limit)
        {
            return Year <= MaxCalendarYear && ToDateTime() < limit;
        }

        /// <summary>
        /// 游标对应的墙上时间（<see cref="DateTimeKind.Unspecified"/>）。
        /// </summary>
        public DateTime ToDateTime()
        {
            return new DateTime(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Unspecified);
        }

        /// <summary>
        /// 跳到某年 1 月 1 日 00:00:00。
        /// </summary>
        public void MoveToYear(int year)
        {
            Year = year;
            MoveToMonth(1);
        }

        /// <summary>
        /// 跳到当年某月 1 日 00:00:00。
        /// </summary>
        public void MoveToMonth(int month)
        {
            Month = month;
            MoveToDay(1);
        }

        /// <summary>
        /// 跳到下个月 1 日 00:00:00。
        /// </summary>
        public void MoveToNextMonth()
        {
            if (Month == 12)
            {
                MoveToYear(Year + 1);
            }
            else
            {
                MoveToMonth(Month + 1);
            }
        }

        /// <summary>
        /// 跳到当月某日 00:00:00。
        /// </summary>
        public void MoveToDay(int day)
        {
            Day = day;
            MoveToHour(0);
        }

        /// <summary>
        /// 跳到下一天 00:00:00。
        /// </summary>
        public void MoveToNextDay()
        {
            if (Day == DateTime.DaysInMonth(Year, Month))
            {
                MoveToNextMonth();
            }
            else
            {
                MoveToDay(Day + 1);
            }
        }

        /// <summary>
        /// 跳到当天某小时的 00:00。
        /// </summary>
        public void MoveToHour(int hour)
        {
            Hour = hour;
            MoveToMinute(0);
        }

        /// <summary>
        /// 跳到下一个小时的 00:00。
        /// </summary>
        public void MoveToNextHour()
        {
            if (Hour == 23)
            {
                MoveToNextDay();
            }
            else
            {
                MoveToHour(Hour + 1);
            }
        }

        /// <summary>
        /// 跳到当前小时某分钟的第 0 秒。
        /// </summary>
        public void MoveToMinute(int minute)
        {
            Minute = minute;
            Second = 0;
        }

        /// <summary>
        /// 跳到下一分钟的第 0 秒。
        /// </summary>
        public void MoveToNextMinute()
        {
            if (Minute == 59)
            {
                MoveToNextHour();
            }
            else
            {
                MoveToMinute(Minute + 1);
            }
        }

        /// <summary>
        /// 跳到当前分钟的某秒。
        /// </summary>
        public void MoveToSecond(int second)
        {
            Second = second;
        }

        /// <summary>
        /// 跳到下一秒。
        /// </summary>
        public void MoveToNextSecond()
        {
            if (Second == 59)
            {
                MoveToNextMinute();
            }
            else
            {
                Second++;
            }
        }
    }

    /// <summary>
    /// 把分好段的表达式解析成各段的匹配表。
    /// </summary>
    /// <remarks>
    /// 每个解析方法返回失败原因，<see langword="null"/> 表示成功。
    /// </remarks>
    private sealed class Parser
    {
        /// <summary>
        /// 月份名，下标 0 是 1 月。
        /// </summary>
        private static readonly string[] _monthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

        /// <summary>
        /// 星期名，下标 0 是周日。
        /// </summary>
        private static readonly string[] _weekdayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

        /// <summary>
        /// 秒段。
        /// </summary>
        private static readonly Field _secondField = new("second", 0, 59, null);

        /// <summary>
        /// 分段。
        /// </summary>
        private static readonly Field _minuteField = new("minute", 0, 59, null);

        /// <summary>
        /// 时段。
        /// </summary>
        private static readonly Field _hourField = new("hour", 0, 23, null);

        /// <summary>
        /// 日段。
        /// </summary>
        private static readonly Field _dayOfMonthField = new("day-of-month", 1, 31, null);

        /// <summary>
        /// 月段。
        /// </summary>
        private static readonly Field _monthField = new("month", 1, 12, _monthNames);

        /// <summary>
        /// 周段。
        /// </summary>
        /// <remarks>
        /// 7 与 0 都是周日，解析后折叠。
        /// </remarks>
        private static readonly Field _dayOfWeekField = new("day-of-week", 0, 7, _weekdayNames);

        /// <summary>
        /// 年段。
        /// </summary>
        private static readonly Field _yearField = new("year", MinYear, MaxYear, null);

        /// <summary>
        /// 分好的各段。
        /// </summary>
        private readonly string[] _fields;

        /// <summary>
        /// 分段在 <see cref="_fields"/> 里的下标：带秒段时为 1，否则为 0。
        /// </summary>
        private readonly int _offset;

        /// <summary>
        /// 由分好的 5、6 或 7 段创建。
        /// </summary>
        public Parser(string[] fields)
        {
            _fields = fields;
            Expression = string.Join(' ', fields);
            HasSeconds = fields.Length >= 6;
            IncludesYears = fields.Length == 7;
            _offset = HasSeconds ? 1 : 0;
            Years = IncludesYears ? new bool[_yearField.Count] : null;
            if (!HasSeconds)
            {
                // 5 段写法等价于秒 = 0
                Seconds[0] = true;
            }
        }

        /// <summary>
        /// 规范化的表达式文本。
        /// </summary>
        public string Expression { get; }

        /// <summary>
        /// 是否带秒段。
        /// </summary>
        public bool HasSeconds { get; }

        /// <summary>
        /// 是否带年段。
        /// </summary>
        public bool IncludesYears { get; }

        /// <summary>
        /// 秒 0 到 59 的匹配表。
        /// </summary>
        public bool[] Seconds { get; } = new bool[60];

        /// <summary>
        /// 分 0 到 59 的匹配表。
        /// </summary>
        public bool[] Minutes { get; } = new bool[60];

        /// <summary>
        /// 时 0 到 23 的匹配表。
        /// </summary>
        public bool[] Hours { get; } = new bool[24];

        /// <summary>
        /// 月 1 到 12 的匹配表（下标 = 月 − 1）。
        /// </summary>
        public bool[] Months { get; } = new bool[12];

        /// <summary>
        /// 年 1970 到 2099 的匹配表（下标 = 年 − 1970）。
        /// </summary>
        /// <remarks>
        /// 没有年段为 <see langword="null"/>。
        /// </remarks>
        public bool[]? Years { get; }

        /// <summary>
        /// 日段规则。
        /// </summary>
        public MonthDayRule MonthDays { get; } = new();

        /// <summary>
        /// 周段规则。
        /// </summary>
        public WeekdayRule Weekdays { get; } = new();

        /// <summary>
        /// 按顺序解析各段，返回第一个失败原因。
        /// </summary>
        /// <remarks>
        /// 全部成功为 <see langword="null"/>。
        /// </remarks>
        public string? Parse()
        {
            return (HasSeconds ? ParseSimple(_fields[0], _secondField, Seconds) : null)
                ?? ParseSimple(_fields[_offset], _minuteField, Minutes)
                ?? ParseSimple(_fields[_offset + 1], _hourField, Hours)
                ?? ParseMonthDays(_fields[_offset + 2])
                ?? ParseSimple(_fields[_offset + 3], _monthField, Months)
                ?? ParseWeekdays(_fields[_offset + 4])
                ?? (Years is null ? null : ParseSimple(_fields[6], _yearField, Years));
        }

        /// <summary>
        /// 解析没有特殊写法的段（秒、分、时、月、年）。
        /// </summary>
        private static string? ParseSimple(string text, Field field, bool[] set)
        {
            if (text == "?")
            {
                return "'?' is only allowed in the day-of-month and day-of-week fields";
            }

            foreach (var item in text.Split(','))
            {
                var reason = ParseItem(item, field, set);
                if (reason is not null)
                {
                    return reason;
                }
            }

            return null;
        }

        /// <summary>
        /// 解析日段：<c>*</c> 与 <c>?</c> 不限定，其余按逗号分项。
        /// </summary>
        private string? ParseMonthDays(string text)
        {
            MonthDays.Restricted = text is not ("*" or "?");
            if (!MonthDays.Restricted)
            {
                return null;
            }

            foreach (var item in text.Split(','))
            {
                var reason = ParseMonthDayItem(item);
                if (reason is not null)
                {
                    return reason;
                }
            }

            return null;
        }

        /// <summary>
        /// 解析日段的一项：<c>L</c>、<c>LW</c>、<c>L-n</c>、<c>nW</c> 或普通项。
        /// </summary>
        private string? ParseMonthDayItem(ReadOnlySpan<char> item)
        {
            if (item.Equals("L", StringComparison.OrdinalIgnoreCase))
            {
                MonthDays.LastDayOffsets[0] = true;
                return null;
            }

            if (item.Equals("LW", StringComparison.OrdinalIgnoreCase))
            {
                MonthDays.LastWeekday = true;
                return null;
            }

            if (item.StartsWith("L-", StringComparison.OrdinalIgnoreCase))
            {
                var offset = item[2..];
                if (!TryParseNumber(offset, out var back) || back > 30)
                {
                    return $"offset '{offset}' after 'L-' in field '{_dayOfMonthField.Name}' must be 0-30";
                }

                MonthDays.LastDayOffsets[back] = true;
                return null;
            }

            if (item.Length > 1 && char.ToUpperInvariant(item[^1]) == 'W')
            {
                var reason = ParseValue(item[..^1], _dayOfMonthField, out var day);
                if (reason is null)
                {
                    MonthDays.NearestWeekdays[day - 1] = true;
                }

                return reason;
            }

            return ParseItem(item, _dayOfMonthField, MonthDays.Days);
        }

        /// <summary>
        /// 解析周段：<c>*</c> 与 <c>?</c> 不限定，其余按逗号分项，最后把 7 折叠到 0。
        /// </summary>
        private string? ParseWeekdays(string text)
        {
            Weekdays.Restricted = text is not ("*" or "?");
            if (!Weekdays.Restricted)
            {
                return null;
            }

            var raw = new bool[_dayOfWeekField.Count];
            foreach (var item in text.Split(','))
            {
                var reason = ParseWeekdayItem(item, raw);
                if (reason is not null)
                {
                    return reason;
                }
            }

            // 7 与 0 都是周日：折叠到 0
            for (var weekday = 0; weekday < 7; weekday++)
            {
                Weekdays.Days[weekday] = raw[weekday];
            }

            Weekdays.Days[0] |= raw[7];
            return null;
        }

        /// <summary>
        /// 解析周段的一项：<c>n#k</c>、<c>nL</c> 或普通项（写入 <paramref name="raw"/>，下标 0 到 7）。
        /// </summary>
        private string? ParseWeekdayItem(ReadOnlySpan<char> item, bool[] raw)
        {
            var hash = item.IndexOf('#');
            if (hash >= 0)
            {
                var reason = ParseValue(item[..hash], _dayOfWeekField, out var weekday);
                if (reason is not null)
                {
                    return reason;
                }

                var ordinalText = item[(hash + 1)..];
                if (!TryParseNumber(ordinalText, out var ordinal) || ordinal is < 1 or > 5)
                {
                    return $"ordinal '{ordinalText}' after '#' in field '{_dayOfWeekField.Name}' must be 1-5";
                }

                Weekdays.NthDays[((ordinal - 1) * 7) + (weekday % 7)] = true;
                return null;
            }

            if (item.Length > 1 && char.ToUpperInvariant(item[^1]) == 'L')
            {
                var reason = ParseValue(item[..^1], _dayOfWeekField, out var weekday);
                if (reason is null)
                {
                    Weekdays.LastDays[weekday % 7] = true;
                }

                return reason;
            }

            return ParseItem(item, _dayOfWeekField, raw);
        }

        /// <summary>
        /// 解析一个普通项。
        /// </summary>
        /// <remarks>
        /// <c>*</c>、<c>*/n</c>、<c>a</c>、<c>a-b</c>、<c>a/n</c>（a 到上限每 n）或 <c>a-b/n</c>；命中的值写入 <paramref name="set"/>（下标 = 值 − 下限）。
        /// </remarks>
        private static string? ParseItem(ReadOnlySpan<char> item, Field field, bool[] set)
        {
            var step = 1;
            var slash = item.IndexOf('/');
            if (slash >= 0)
            {
                var stepText = item[(slash + 1)..];
                if (!TryParseNumber(stepText, out step) || step < 1)
                {
                    return $"step '{stepText}' in field '{field.Name}' must be a positive number";
                }

                item = item[..slash];
            }

            var reason = ParseRange(item, field, slash >= 0, out var start, out var end);
            if (reason is not null)
            {
                return reason;
            }

            for (var value = start; value <= end; value += step)
            {
                set[value - field.Min] = true;
            }

            return null;
        }

        /// <summary>
        /// 解析项中步长之前的部分。
        /// </summary>
        /// <remarks>
        /// <c>*</c> 为全段，<c>a-b</c> 为范围，单值带步长时为 a 到上限，否则就是 a。
        /// </remarks>
        private static string? ParseRange(ReadOnlySpan<char> text, Field field, bool hasStep, out int start, out int end)
        {
            if (text.Equals("*", StringComparison.Ordinal))
            {
                start = field.Min;
                end = field.Max;
                return null;
            }

            var dash = text.IndexOf('-');
            if (dash < 0)
            {
                end = field.Max;
                var single = ParseValue(text, field, out start);
                if (single is null && !hasStep)
                {
                    end = start;
                }

                return single;
            }

            var reason = ParseValue(text[..dash], field, out start);
            if (reason is not null)
            {
                end = field.Max;
                return reason;
            }

            reason = ParseValue(text[(dash + 1)..], field, out end);
            if (reason is null && start > end)
            {
                return $"range '{text}' in field '{field.Name}' has its start after its end";
            }

            return reason;
        }

        /// <summary>
        /// 解析一个值：十进制数字或该段允许的名称（不区分大小写），并检查范围。
        /// </summary>
        private static string? ParseValue(ReadOnlySpan<char> text, Field field, out int value)
        {
            if (text.IsEmpty)
            {
                value = 0;
                return $"missing value in field '{field.Name}'";
            }

            if (TryParseNumber(text, out value))
            {
                return value < field.Min || value > field.Max
                    ? $"value '{value}' in field '{field.Name}' is out of range {field.Min}-{field.Max}"
                    : null;
            }

            if (field.Names is not null)
            {
                for (var index = 0; index < field.Names.Length; index++)
                {
                    if (text.Equals(field.Names[index], StringComparison.OrdinalIgnoreCase))
                    {
                        value = field.Min + index;
                        return null;
                    }
                }
            }

            return $"unknown token '{text}' in field '{field.Name}'";
        }

        /// <summary>
        /// 解析只含十进制数字的非负整数。
        /// </summary>
        private static bool TryParseNumber(ReadOnlySpan<char> text, out int value)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }
    }
}
