// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Scheduling.Abstractions.Calendars;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions.Tests.Schedules;

/// <summary>
/// 计划：各种类的下一次推算、起止窗口、文本往返与日历。
/// </summary>
public sealed class JobScheduleTests
{
    /// <summary>
    /// 2026-10-23 是周五。
    /// </summary>
    private static readonly DateTimeOffset _friday = new(2026, 10, 23, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void NextRun_PerKind()
    {
        Assert.False(JobSchedule.Manual.NextRun(_friday).HasValue);
        Assert.Equal(_friday.AddMinutes(5), JobSchedule.Every(TimeSpan.FromMinutes(5)).NextRun(_friday).ValueOrThrow());
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 3, 0, 0, TimeSpan.Zero), JobSchedule.FromCron("0 3 * * *").NextRun(_friday).ValueOrThrow());
        Assert.Equal(_friday.AddHours(1), JobSchedule.Once(_friday.AddHours(1)).NextRun(_friday).ValueOrThrow());
        Assert.False(JobSchedule.Once(_friday).NextRun(_friday).HasValue);
        Assert.Throws<ArgumentOutOfRangeException>(static () => JobSchedule.Every(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(static () => JobSchedule.Every(TimeSpan.FromSeconds(1), 0));
        Assert.Throws<ArgumentException>(static () => JobSchedule.FromCron("bad"));
        Assert.Throws<ArgumentException>(static () => JobSchedule.FromCron("0 3 * * *", "Mars/Olympus"));
    }

    [Fact]
    public void NextRun_HonorsStartAndEndWindow_AndTimeZone()
    {
        var start = _friday.AddDays(3);
        var windowed = JobSchedule.Every(TimeSpan.FromMinutes(5)) with { StartAt = start, EndAt = start.AddMinutes(12) };
        // 窗口开始之前从窗口开始算；超过结束时刻没有下一次
        Assert.Equal(start.AddMinutes(5), windowed.NextRun(_friday).ValueOrThrow());
        Assert.Equal(start.AddMinutes(10), windowed.NextRun(start.AddMinutes(5)).ValueOrThrow());
        Assert.False(windowed.NextRun(start.AddMinutes(10)).HasValue);
        var shanghai = JobSchedule.FromCron("0 9 * * *", "Asia/Shanghai");
        var next = shanghai.NextRun(_friday).ValueOrThrow();
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.FromHours(8)), next);
    }

    [Fact]
    public void Text_RoundTrips_AllKindsAndOptions()
    {
        var samples = new[]
        {
            JobSchedule.Manual,
            JobSchedule.Every(TimeSpan.FromMinutes(5)),
            JobSchedule.Every(TimeSpan.FromSeconds(1.5), 3),
            JobSchedule.FromCron("0 3 * * *"),
            JobSchedule.FromCron("0 0 9 * * MON-FRI", "Asia/Shanghai"),
            JobSchedule.Once(_friday),
            JobSchedule.FromCron("*/5 * * * *") with { StartAt = _friday, EndAt = _friday.AddDays(1), Calendar = "holidays" },
        };
        foreach (var sample in samples)
        {
            var text = sample.ToString();
            var parsed = JobSchedule.Parse(text);
            Assert.Equal(text, parsed.ToString());
            Assert.Equal((sample.Kind, sample.Interval, sample.RepeatCount, sample.TimeZoneId, sample.At, sample.StartAt, sample.EndAt, sample.Calendar), (parsed.Kind, parsed.Interval, parsed.RepeatCount, parsed.TimeZoneId, parsed.At, parsed.StartAt, parsed.EndAt, parsed.Calendar));
        }

        Assert.Equal("every 00:05:00", JobSchedule.Every(TimeSpan.FromMinutes(5)).ToString());
        Assert.Equal("every 00:00:01.5000000 x3", JobSchedule.Every(TimeSpan.FromSeconds(1.5), 3).ToString());
        Assert.Equal("cron 0 3 * * *", JobSchedule.FromCron("0 3 * * *").ToString());
        Assert.Equal("manual", JobSchedule.Manual.ToString());
        Assert.False(JobSchedule.TryParse("every", out _));
        Assert.False(JobSchedule.TryParse("every 00:05:00 x0", out _));
        Assert.False(JobSchedule.TryParse("cron 0 3 * * * tz=Mars/Olympus", out _));
        Assert.False(JobSchedule.TryParse("once tomorrow", out _));
        Assert.Throws<ArgumentException>(static () => JobSchedule.Parse("weekly"));
    }

    [Fact]
    public void Planner_SkipsExcludedInstants_AndStopsAtRepeatCount()
    {
        var saturday = new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);
        var weekdays = new WeeklyCalendar();
        // 周六、周日被排除：周五之后的下一次落到周一
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero), JobPlanner.NextFire(JobSchedule.FromCron("0 9 * * *"), _friday, weekdays).ValueOrThrow());
        Assert.True(weekdays.IsExcluded(saturday));
        var holidays = new HolidayCalendar([new DateOnly(2026, 10, 26)]);
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero), JobPlanner.NextFire(JobSchedule.FromCron("0 9 * * *"), _friday, new CompositeCalendar(weekdays, holidays)).ValueOrThrow());
        var nightly = new DailyCalendar(new TimeOnly(22, 0), new TimeOnly(6, 0));
        Assert.True(nightly.IsExcluded(_friday.AddHours(13)));
        Assert.False(nightly.IsExcluded(_friday));
        var everyHour = new CronCalendar(CronExpression.Parse("0 * * * *"));
        Assert.True(everyHour.IsExcluded(new DateTimeOffset(2026, 10, 23, 11, 0, 0, TimeSpan.Zero)));
        Assert.False(everyHour.IsExcluded(_friday));
        // 一次性计划被排除即没有下一次；次数用尽没有下一次
        Assert.False(JobPlanner.NextFire(JobSchedule.Once(saturday), _friday, weekdays).HasValue);
        Assert.False(JobPlanner.NextFire(JobSchedule.Every(TimeSpan.FromMinutes(1), 2), _friday, null, fireCount: 2).HasValue);
        Assert.True(JobPlanner.NextFire(JobSchedule.Every(TimeSpan.FromMinutes(1), 2), _friday, null, fireCount: 1).HasValue);
    }

    [Fact]
    public void Calendars_RegisterByName_AndRejectDuplicates()
    {
        var calendars = new JobCalendars();
        calendars.Add("weekends", new WeeklyCalendar());
        Assert.True(calendars.Find("weekends").HasValue);
        Assert.False(calendars.Find("none").HasValue);
        Assert.Throws<ArgumentException>(() => calendars.Add("weekends", new WeeklyCalendar()));
        Assert.Throws<ArgumentException>(() => calendars.Add("Bad Name", new WeeklyCalendar()));
        Assert.Equal(["weekends"], calendars.Names);
    }
}
