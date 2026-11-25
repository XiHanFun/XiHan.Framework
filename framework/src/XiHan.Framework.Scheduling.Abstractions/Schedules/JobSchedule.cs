// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using XiHan.Framework.Primitives;
using XiHan.Framework.Scheduling.Abstractions.Jobs;

namespace XiHan.Framework.Scheduling.Abstractions.Schedules;

/// <summary>
/// 作业的计划
/// </summary>
/// <remarks>
/// 手动、固定间隔（可限次数）、cron（可指定时区）或一次；任何计划可带起止窗口与排除日历。文本形式可往返（<see cref="ToString"/> / <see cref="Parse"/>），存储以文本持久化；文法见规范 RFC-0013「计划的文本形式」（ADR-0297）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record JobSchedule
{
    /// <summary>
    /// 文本里的起始时刻选项
    /// </summary>
    private const string StartOption = "start=";

    /// <summary>
    /// 文本里的结束时刻选项
    /// </summary>
    private const string EndOption = "end=";

    /// <summary>
    /// 文本里的日历选项
    /// </summary>
    private const string CalendarOption = "calendar=";

    /// <summary>
    /// 文本里的时区选项（只对 cron）
    /// </summary>
    private const string TimeZoneOption = "tz=";

    /// <summary>
    /// 文本里各部分之间的分隔：正好一个空格
    /// </summary>
    private const char Separator = ' ';

    /// <summary>
    /// 间隔里天数的最多位数
    /// </summary>
    /// <remarks>
    /// <see cref="TimeSpan.MaxValue"/> 是 10675199 天多
    /// </remarks>
    private const int MaxDayDigits = 8;

    /// <summary>
    /// 间隔里不足一秒的部分的位数（100 纳秒为单位）
    /// </summary>
    private const int FractionDigits = 7;

    /// <summary>
    /// 由种类与参数创建
    /// </summary>
    private JobSchedule(JobScheduleKind kind, TimeSpan? interval, int? repeatCount, CronExpression? cron, string? timeZoneId, DateTimeOffset? at)
    {
        Kind = kind;
        Interval = interval;
        RepeatCount = repeatCount;
        Cron = cron;
        TimeZoneId = timeZoneId;
        At = at;
    }

    /// <summary>
    /// 只手动触发
    /// </summary>
    public static JobSchedule Manual { get; } = new(JobScheduleKind.Manual, null, null, null, null, null);

    /// <summary>
    /// 种类
    /// </summary>
    public JobScheduleKind Kind { get; }

    /// <summary>
    /// 间隔
    /// </summary>
    /// <remarks>
    /// 只有 <see cref="JobScheduleKind.Interval"/> 有值，其余为 <see langword="null"/>（RFC-0001「缺失用 null」，ADR-0161）
    /// </remarks>
    public TimeSpan? Interval { get; }

    /// <summary>
    /// 总运行次数上限（只对 <see cref="JobScheduleKind.Interval"/>）
    /// </summary>
    /// <remarks>
    /// 空为无限
    /// </remarks>
    public int? RepeatCount { get; }

    /// <summary>
    /// cron 表达式（只对 <see cref="JobScheduleKind.Cron"/> 有值）
    /// </summary>
    public CronExpression? Cron { get; }

    /// <summary>
    /// cron 的时区名（IANA tz 数据库的名字）
    /// </summary>
    /// <remarks>
    /// 空为 UTC
    /// </remarks>
    public string? TimeZoneId { get; }

    /// <summary>
    /// 运行时刻
    /// </summary>
    /// <remarks>
    /// 只有 <see cref="JobScheduleKind.Once"/> 有值，其余为 <see langword="null"/>（ADR-0161）
    /// </remarks>
    public DateTimeOffset? At { get; }

    /// <summary>
    /// 起始时刻：之前不运行（推算下一次时从它开始）
    /// </summary>
    public DateTimeOffset? StartAt { get; init; }

    /// <summary>
    /// 结束时刻：之后没有下一次
    /// </summary>
    public DateTimeOffset? EndAt { get; init; }

    /// <summary>
    /// 排除日历的名字（<c>services.AddJobCalendar</c> 登记）
    /// </summary>
    /// <remarks>
    /// 空为不排除
    /// </remarks>
    public string? Calendar { get; init; }

    /// <summary>
    /// 固定间隔
    /// </summary>
    /// <remarks>
    /// 上一次完成后再等一个间隔；<paramref name="interval"/> 必须为正，<c>repeatCount</c> 为总运行次数（空为无限，否则至少 1）
    /// <c>repeatCount</c> 取 <c>null</c>
    /// </remarks>
    public static JobSchedule Every(TimeSpan interval)
        => Every(interval, null);

    /// <summary>
    /// 固定间隔
    /// </summary>
    /// <remarks>
    /// 上一次完成后再等一个间隔；<paramref name="interval"/> 必须为正，<paramref name="repeatCount"/> 为总运行次数（空为无限，否则至少 1）
    /// </remarks>
    public static JobSchedule Every(TimeSpan interval, int? repeatCount)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The interval must be positive.");
        }

        if (repeatCount is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(repeatCount), repeatCount, "The repeat count must be at least 1.");
        }

        return new JobSchedule(JobScheduleKind.Interval, interval, repeatCount, null, null, null);
    }

    /// <summary>
    /// cron 表达式
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>；时区为 UTC
    /// <c>timeZoneId</c> 取 <c>null</c>
    /// </remarks>
    public static JobSchedule FromCron(string expression)
        => FromCron(expression, null);

    /// <summary>
    /// cron 表达式
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>。<paramref name="timeZoneId"/> 是 IANA tz 数据库的名字（<c>Asia/Shanghai</c>、<c>UTC</c>），空为 UTC；不合时区名的文法、本机的时区库里找不到或不是 IANA 的名字（Windows 的时区 ID 等）都抛出 <see cref="ArgumentException"/>：写进计划文本的名字必须读得回来（RFC-0013「计划的文本形式」，ADR-0297）
    /// </remarks>
    public static JobSchedule FromCron(string expression, string? timeZoneId)
    {
        var cron = CronExpression.Parse(expression);
        if (!string.IsNullOrWhiteSpace(timeZoneId) && !TryResolveIanaTimeZone(timeZoneId, out _))
        {
            throw new ArgumentException($"Time zone '{timeZoneId}' is invalid: it must be an IANA time zone name that the local time zone database knows.", nameof(timeZoneId));
        }

        return new JobSchedule(JobScheduleKind.Cron, null, null, cron, string.IsNullOrWhiteSpace(timeZoneId) ? null : timeZoneId, null);
    }

    /// <summary>
    /// 在 <paramref name="at"/> 运行一次
    /// </summary>
    public static JobSchedule Once(DateTimeOffset at) => new(JobScheduleKind.Once, null, null, null, null, at);

    /// <summary>
    /// <paramref name="after"/> 之后的下一次运行时刻（不考虑日历）
    /// </summary>
    /// <remarks>
    /// 手动计划、超过 <see cref="EndAt"/> 或一次性计划已过都无值。间隔计划的 <paramref name="after"/> 应是上一次的完成时刻
    /// </remarks>
    public Maybe<DateTimeOffset> NextRun(DateTimeOffset after)
    {
        // 起始窗口：窗口开始之前的"之后"一律从窗口开始算（间隔计划的首次也落在窗口开始 + 间隔）
        if (StartAt is { } start && after < start)
        {
            after = start;
        }

        Maybe<DateTimeOffset> next = Kind switch
        {
            JobScheduleKind.Interval when Interval is { } interval => after + interval,
            JobScheduleKind.Cron => TimeZoneId is null ? Cron!.Next(after) : Cron!.Next(after, ResolveTimeZone(TimeZoneId)),
            JobScheduleKind.Once when At is { } at => at > after ? Maybe.Some(at) : default,
            _ => default,
        };
        return next.TryGetValue(out var value) && EndAt is { } end && value > end ? default : next;
    }

    /// <summary>
    /// 解析 <see cref="ToString"/> 产生的文本
    /// </summary>
    /// <remarks>
    /// 不合法抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static JobSchedule Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var schedule) ? schedule : throw new ArgumentException($"Schedule text '{text}' is invalid.", nameof(text));
    }

    /// <summary>
    /// 尝试解析 <see cref="ToString"/> 产生的文本
    /// </summary>
    /// <remarks>
    /// 文法见规范 RFC-0013「计划的文本形式」：各部分之间正好一个空格，种类之后是种类自己的部分，再按 <c>tz=</c>（只跟在 cron 后面）、<c>start=</c>、<c>end=</c>、<c>calendar=</c> 的次序接选项，每个至多一次；间隔、次数与 cron 的写法与 <see cref="ToString"/> 写出的相同，时刻按 RFC-0003 规则 8 的读法（<see cref="TimestampText"/>）。时区名在本机的时区库里找不到时也返回 <see langword="false"/>
    /// </remarks>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out JobSchedule? schedule)
    {
        schedule = null;

        // 只认单个空格分隔：开头、结尾与连着的空格分出空的部分，不合法。制表符是 cron 自己认的分隔符，
        // 放进去会被 cron 当成两段，在这里先挡掉；别的空白落在某一部分里，由那一部分的文法拒绝
        if (string.IsNullOrEmpty(text) || text.Contains('\t', StringComparison.Ordinal))
        {
            return false;
        }

        var parts = text.Split(Separator);
        if (Array.Exists(parts, static part => part.Length == 0))
        {
            return false;
        }

        var index = 1;
        JobSchedule parsed;
        switch (parts[0])
        {
            case "manual":
                parsed = Manual;
                break;
            case "every":
                if (index >= parts.Length || !TryParseInterval(parts[index++], out var interval))
                {
                    return false;
                }

                int? repeat = null;
                if (index < parts.Length && parts[index].StartsWith('x'))
                {
                    if (!TryParseRepeatCount(parts[index++], out var count))
                    {
                        return false;
                    }

                    repeat = count;
                }

                parsed = Every(interval, repeat);
                break;
            case "cron":
                // cron 的各段不含 '='，选项都含：表达式到第一个含 '=' 的部分为止
                var first = index;
                while (index < parts.Length && !parts[index].Contains('=', StringComparison.Ordinal))
                {
                    index++;
                }

                if (!CronExpression.TryParse(string.Join(Separator, parts, first, index - first), out var cron))
                {
                    return false;
                }

                string? timeZoneId = null;
                if (index < parts.Length && parts[index].StartsWith(TimeZoneOption, StringComparison.Ordinal))
                {
                    timeZoneId = parts[index++][TimeZoneOption.Length..];
                    if (!TryResolveIanaTimeZone(timeZoneId, out _))
                    {
                        return false;
                    }
                }

                parsed = new JobSchedule(JobScheduleKind.Cron, null, null, cron, timeZoneId, null);
                break;
            case "once":
                if (index >= parts.Length || !TimestampText.TryParse(parts[index++], out var at))
                {
                    return false;
                }

                parsed = Once(at);
                break;
            default:
                return false;
        }

        // 选项按固定次序、每个至多一次：次序不对、重复与不认得的选项都会剩下没读完的部分
        DateTimeOffset? startAt = null;
        if (index < parts.Length && parts[index].StartsWith(StartOption, StringComparison.Ordinal))
        {
            if (!TimestampText.TryParse(parts[index++][StartOption.Length..], out var start))
            {
                return false;
            }

            startAt = start;
        }

        DateTimeOffset? endAt = null;
        if (index < parts.Length && parts[index].StartsWith(EndOption, StringComparison.Ordinal))
        {
            if (!TimestampText.TryParse(parts[index++][EndOption.Length..], out var end))
            {
                return false;
            }

            endAt = end;
        }

        string? calendar = null;
        if (index < parts.Length && parts[index].StartsWith(CalendarOption, StringComparison.Ordinal))
        {
            calendar = parts[index++][CalendarOption.Length..];
            if (!JobName.IsValid(calendar))
            {
                return false;
            }
        }

        if (index != parts.Length)
        {
            return false;
        }

        schedule = parsed with { StartAt = startAt, EndAt = endAt, Calendar = calendar };
        return true;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var text = Kind switch
        {
            JobScheduleKind.Interval when Interval is { } interval => "every " + interval.ToString("c", CultureInfo.InvariantCulture) + (RepeatCount is { } count ? " x" + count.ToString(CultureInfo.InvariantCulture) : string.Empty),
            JobScheduleKind.Cron => "cron " + Cron + (TimeZoneId is null ? string.Empty : " " + TimeZoneOption + TimeZoneId),
            JobScheduleKind.Once when At is { } at => "once " + Format(at),
            _ => "manual",
        };
        if (StartAt is { } start)
        {
            text += " " + StartOption + Format(start);
        }

        if (EndAt is { } end)
        {
            text += " " + EndOption + Format(end);
        }

        if (Calendar is not null)
        {
            text += " " + CalendarOption + Calendar;
        }

        return text;
    }

    /// <summary>
    /// 按 ID 找时区
    /// </summary>
    /// <remarks>
    /// 找不到抛出 <see cref="ArgumentException"/>
    /// </remarks>
    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        => TryResolveTimeZone(timeZoneId, out var zone) ? zone : throw new ArgumentException($"Time zone '{timeZoneId}' is unknown.", nameof(timeZoneId));

    /// <summary>
    /// 按 ID 找时区（IANA 与 Windows ID 都接受）
    /// </summary>
    private static bool TryResolveTimeZone(string timeZoneId, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out zone);
    }

    /// <summary>
    /// 按 IANA 的名字找时区
    /// </summary>
    /// <remarks>
    /// 名字合时区名的文法、本机的时区库里找得到、且是 IANA 的名字（Windows 的时区 ID 不收：别的机器与别的语言读不出）
    /// </remarks>
    private static bool TryResolveIanaTimeZone(string timeZoneId, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;
        return IsTimeZoneName(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out zone) && zone.HasIanaId;
    }

    /// <summary>
    /// 是否合时区名的文法
    /// </summary>
    /// <remarks>
    /// 以 <c>/</c> 分开的一到多个部分，每部分是一个以上的 ASCII 字母、数字、<c>_</c>、<c>+</c> 或 <c>-</c>：<c>^[A-Za-z0-9_+-]+(/[A-Za-z0-9_+-]+)*$</c>
    /// </remarks>
    private static bool IsTimeZoneName(string text)
    {
        if (text.Length == 0 || text[0] == '/' || text[^1] == '/')
        {
            return false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '/')
            {
                // 部分不能为空：不能有连着的两个 '/'
                if (text[index - 1] == '/')
                {
                    return false;
                }
            }
            else if (!char.IsAsciiLetterOrDigit(character) && character is not ('_' or '+' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 解析间隔：<c>[d.]hh:mm:ss[.fffffff]</c>
    /// </summary>
    /// <remarks>
    /// 与 <see cref="TimeSpan"/> 的 <c>c</c> 格式对正值写出的形状相同：天数不带前导 0、不足一天不写，时 00 到 23、分秒 00 到 59 各两位，不足一秒的部分正好七位、为 0 时不写；间隔大于 0、不超过 <see cref="TimeSpan.MaxValue"/>
    /// </remarks>
    private static bool TryParseInterval(string text, out TimeSpan interval)
    {
        interval = default;
        var span = text.AsSpan();
        long days = 0;
        var colon = span.IndexOf(':');
        var dot = span.IndexOf('.');
        if (dot >= 0 && (colon < 0 || dot < colon))
        {
            // 天数：一到八位、不带前导 0
            var dayText = span[..dot];
            if (dayText.Length is 0 or > MaxDayDigits || dayText[0] == '0' || !TryDigits(dayText, out days))
            {
                return false;
            }

            span = span[(dot + 1)..];
        }

        if (span.Length < 8 || span[2] != ':' || span[5] != ':'
            || !TryDigits(span[0..2], out var hours) || !TryDigits(span[3..5], out var minutes) || !TryDigits(span[6..8], out var seconds)
            || hours > 23 || minutes > 59 || seconds > 59)
        {
            return false;
        }

        long fraction = 0;
        span = span[8..];
        if (span.Length > 0)
        {
            // 不足一秒的部分：正好七位，为 0 时写法里不出现
            if (span.Length != FractionDigits + 1 || span[0] != '.' || !TryDigits(span[1..], out fraction) || fraction == 0)
            {
                return false;
            }
        }

        // 天数最多八位，乘出来装得下 long；与不足一天的部分相加之前先核对上限
        var dayTicks = days * TimeSpan.TicksPerDay;
        var rest = (((((hours * 60L) + minutes) * 60L) + seconds) * TimeSpan.TicksPerSecond) + fraction;
        if (dayTicks > TimeSpan.MaxValue.Ticks - rest)
        {
            return false;
        }

        interval = TimeSpan.FromTicks(dayTicks + rest);
        return interval > TimeSpan.Zero;
    }

    /// <summary>
    /// 解析总运行次数：<c>x</c> 接 1 到 2147483647、不带前导 0 的十进制整数
    /// </summary>
    private static bool TryParseRepeatCount(string text, out int count)
    {
        count = 0;
        return text.Length > 1 && text[0] == 'x' && text[1] != '0'
            && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out count);
    }

    /// <summary>
    /// 只由 ASCII 数字组成的十进制数
    /// </summary>
    private static bool TryDigits(ReadOnlySpan<char> text, out long value)
    {
        value = 0;
        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }

            value = (value * 10) + (character - '0');
        }

        return true;
    }

    /// <summary>
    /// 时刻的往返格式
    /// </summary>
    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

}
