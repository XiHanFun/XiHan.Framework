// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Scheduling.Abstractions.Schedules;

/// <summary>
/// 作业的计划：手动、固定间隔（可限次数）、cron（可指定时区）或一次；任何计划可带起止窗口与排除日历。文本形式可往返（<see cref="ToString"/> / <see cref="Parse"/>），存储以文本持久化。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JobSchedule
{
    /// <summary>
    /// 文本里的起始时刻选项。
    /// </summary>
    private const string StartOption = "start=";

    /// <summary>
    /// 文本里的结束时刻选项。
    /// </summary>
    private const string EndOption = "end=";

    /// <summary>
    /// 文本里的日历选项。
    /// </summary>
    private const string CalendarOption = "calendar=";

    /// <summary>
    /// 文本里的时区选项（只对 cron）。
    /// </summary>
    private const string TimeZoneOption = "tz=";

    /// <summary>
    /// 由种类与参数创建。
    /// </summary>
    private JobSchedule(JobScheduleKind kind, TimeSpan interval, int? repeatCount, CronExpression? cron, string? timeZoneId, DateTimeOffset at)
    {
        Kind = kind;
        Interval = interval;
        RepeatCount = repeatCount;
        Cron = cron;
        TimeZoneId = timeZoneId;
        At = at;
    }

    /// <summary>
    /// 只手动触发。
    /// </summary>
    public static JobSchedule Manual { get; } = new(JobScheduleKind.Manual, TimeSpan.Zero, null, null, null, default);

    /// <summary>
    /// 种类。
    /// </summary>
    public JobScheduleKind Kind { get; }

    /// <summary>
    /// 间隔（只对 <see cref="JobScheduleKind.Interval"/> 有意义）。
    /// </summary>
    public TimeSpan Interval { get; }

    /// <summary>
    /// 总运行次数上限（只对 <see cref="JobScheduleKind.Interval"/>）；空为无限。
    /// </summary>
    public int? RepeatCount { get; }

    /// <summary>
    /// cron 表达式（只对 <see cref="JobScheduleKind.Cron"/> 有值）。
    /// </summary>
    public CronExpression? Cron { get; }

    /// <summary>
    /// cron 的时区 ID（<see cref="TimeZoneInfo"/> 的 ID）；空为 UTC。
    /// </summary>
    public string? TimeZoneId { get; }

    /// <summary>
    /// 运行时刻（只对 <see cref="JobScheduleKind.Once"/>）。
    /// </summary>
    public DateTimeOffset At { get; }

    /// <summary>
    /// 起始时刻：之前不运行（推算下一次时从它开始）。
    /// </summary>
    public DateTimeOffset? StartAt { get; init; }

    /// <summary>
    /// 结束时刻：之后没有下一次。
    /// </summary>
    public DateTimeOffset? EndAt { get; init; }

    /// <summary>
    /// 排除日历的名字（<c>services.AddJobCalendar</c> 登记）；空为不排除。
    /// </summary>
    public string? Calendar { get; init; }

    /// <summary>
    /// 固定间隔：上一次完成后再等一个间隔；<paramref name="interval"/> 必须为正，<paramref name="repeatCount"/> 为总运行次数（空为无限，否则至少 1）。
    /// </summary>
    public static JobSchedule Every(TimeSpan interval, int? repeatCount = null)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The interval must be positive.");
        }

        if (repeatCount is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(repeatCount), repeatCount, "The repeat count must be at least 1.");
        }

        return new JobSchedule(JobScheduleKind.Interval, interval, repeatCount, null, null, default);
    }

    /// <summary>
    /// cron 表达式；不合法抛出 <see cref="ArgumentException"/>。<paramref name="timeZoneId"/> 为 <see cref="TimeZoneInfo"/> 的 ID（IANA 或 Windows），空为 UTC；找不到的时区抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static JobSchedule FromCron(string expression, string? timeZoneId = null)
    {
        var cron = CronExpression.Parse(expression);
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            _ = ResolveTimeZone(timeZoneId);
        }

        return new JobSchedule(JobScheduleKind.Cron, TimeSpan.Zero, null, cron, string.IsNullOrWhiteSpace(timeZoneId) ? null : timeZoneId, default);
    }

    /// <summary>
    /// 在 <paramref name="at"/> 运行一次。
    /// </summary>
    public static JobSchedule Once(DateTimeOffset at) => new(JobScheduleKind.Once, TimeSpan.Zero, null, null, null, at);

    /// <summary>
    /// <paramref name="after"/> 之后的下一次运行时刻（不考虑日历）；手动计划、超过 <see cref="EndAt"/> 或一次性计划已过都无值。间隔计划的 <paramref name="after"/> 应是上一次的完成时刻。
    /// </summary>
    public Maybe<DateTimeOffset> NextRun(DateTimeOffset after)
    {
        // 起始窗口：窗口开始之前的"之后"一律从窗口开始算（间隔计划的首次也落在窗口开始 + 间隔）
        if (StartAt is { } start && after < start)
        {
            after = start;
        }

        Maybe<DateTimeOffset> next = Kind switch
        {
            JobScheduleKind.Interval => after + Interval,
            JobScheduleKind.Cron => TimeZoneId is null ? Cron!.Next(after) : Cron!.Next(after, ResolveTimeZone(TimeZoneId)),
            JobScheduleKind.Once => At > after ? Maybe.Some(At) : default,
            _ => default,
        };
        return next.TryGetValue(out var value) && EndAt is { } end && value > end ? default : next;
    }

    /// <summary>
    /// 解析 <see cref="ToString"/> 产生的文本；不合法抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static JobSchedule Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var schedule) ? schedule : throw new ArgumentException($"Schedule text '{text}' is invalid.", nameof(text));
    }

    /// <summary>
    /// 尝试解析 <see cref="ToString"/> 产生的文本。
    /// </summary>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out JobSchedule? schedule)
    {
        schedule = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // 尾部形如 k=v 的词是选项；cron 表达式本身不含 '='，所以从尾部剥离不会误伤
        DateTimeOffset? startAt = null;
        DateTimeOffset? endAt = null;
        string? calendar = null;
        string? timeZoneId = null;
        var end = tokens.Length;
        while (end > 1 && tokens[end - 1].Contains('=', StringComparison.Ordinal))
        {
            var option = tokens[end - 1];
            if (option.StartsWith(StartOption, StringComparison.Ordinal) && TryParseTime(option[StartOption.Length..], out var start))
            {
                startAt = start;
            }
            else if (option.StartsWith(EndOption, StringComparison.Ordinal) && TryParseTime(option[EndOption.Length..], out var endTime))
            {
                endAt = endTime;
            }
            else if (option.StartsWith(CalendarOption, StringComparison.Ordinal) && option.Length > CalendarOption.Length)
            {
                calendar = option[CalendarOption.Length..];
            }
            else if (option.StartsWith(TimeZoneOption, StringComparison.Ordinal) && option.Length > TimeZoneOption.Length)
            {
                timeZoneId = option[TimeZoneOption.Length..];
            }
            else
            {
                return false;
            }

            end--;
        }

        JobSchedule? parsed;
        switch (tokens[0])
        {
            case "manual" when end == 1:
                parsed = Manual;
                break;
            case "every" when end is 2 or 3 && TimeSpan.TryParseExact(tokens[1], "c", CultureInfo.InvariantCulture, out var interval) && interval > TimeSpan.Zero:
                int? repeat = null;
                if (end == 3)
                {
                    if (tokens[2].Length < 2 || tokens[2][0] != 'x' || !int.TryParse(tokens[2].AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
                    {
                        return false;
                    }

                    repeat = count;
                }

                parsed = Every(interval, repeat);
                break;
            case "cron" when end >= 2:
                if (!CronExpression.TryParse(string.Join(' ', tokens, 1, end - 1), out var cron))
                {
                    return false;
                }

                if (timeZoneId is not null && !TryResolveTimeZone(timeZoneId, out _))
                {
                    return false;
                }

                parsed = new JobSchedule(JobScheduleKind.Cron, TimeSpan.Zero, null, cron, timeZoneId, default);
                break;
            case "once" when end == 2 && TryParseTime(tokens[1], out var at):
                parsed = Once(at);
                break;
            default:
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
            JobScheduleKind.Interval => "every " + Interval.ToString("c", CultureInfo.InvariantCulture) + (RepeatCount is { } count ? " x" + count.ToString(CultureInfo.InvariantCulture) : string.Empty),
            JobScheduleKind.Cron => "cron " + Cron + (TimeZoneId is null ? string.Empty : " " + TimeZoneOption + TimeZoneId),
            JobScheduleKind.Once => "once " + Format(At),
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
    /// 按 ID 找时区；找不到抛出 <see cref="ArgumentException"/>。
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        => TryResolveTimeZone(timeZoneId, out var zone) ? zone : throw new ArgumentException($"Time zone '{timeZoneId}' is unknown.", nameof(timeZoneId));

    /// <summary>
    /// 按 ID 找时区（IANA 与 Windows ID 都接受）。
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
    /// 时刻的往返格式。
    /// </summary>
    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    /// <summary>
    /// 解析往返格式的时刻。
    /// </summary>
    private static bool TryParseTime(string text, out DateTimeOffset value)
        => DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
}
