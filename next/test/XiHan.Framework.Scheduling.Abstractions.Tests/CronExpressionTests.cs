// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions.Tests;

/// <summary>
/// 5 段 cron 的解析与下一次匹配。
/// </summary>
public sealed class CronExpressionTests
{
    /// <summary>
    /// 2026-10-23 周五 08:30:15 UTC。
    /// </summary>
    private static readonly DateTimeOffset _friday = new(2026, 10, 23, 8, 30, 15, TimeSpan.Zero);

    /// <summary>
    /// 表达式在基准时刻之后的下一次匹配。
    /// </summary>
    private static DateTimeOffset Next(string expression, DateTimeOffset? after = null) => CronExpression.Parse(expression).Next(after ?? _friday).ValueOrThrow();

    [Fact]
    public void Next_MatchesStandardCronSemantics()
    {
        // 每分钟：下一个整分钟
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 8, 31, 0, TimeSpan.Zero), Next("* * * * *"));
        // 每天 03:00
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 3, 0, 0, TimeSpan.Zero), Next("0 3 * * *"));
        // 每小时的第 30 分：当前分钟已过（30:15 > 30:00）→ 09:30
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 9, 30, 0, TimeSpan.Zero), Next("30 * * * *"));
        // 步长与范围
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 8, 45, 0, TimeSpan.Zero), Next("*/15 * * * *"));
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 9, 5, 0, TimeSpan.Zero), Next("5-10/5 9-17 * * *"));
        // 工作日 09:00：周五 08:30 → 周五 09:00；周五 10:00 → 下周一
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 9, 0, 0, TimeSpan.Zero), Next("0 9 * * 1-5"));
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero), Next("0 9 * * 1-5", new DateTimeOffset(2026, 10, 23, 10, 0, 0, TimeSpan.Zero)));
        // 周日可写 0 或 7
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), Next("0 0 * * 7"));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), Next("0 0 * * 0"));
        // 每月 1 日与每周一同时限定：任一匹配（10-26 周一先于 11-01）
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero), Next("0 0 1 * 1"));
        // 月份与跨年
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), Next("0 0 1 1 *"));
        // 列表
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero), Next("0 0,12 * * *"));
        // 非 UTC 的输入按 UTC 计算
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 3, 0, 0, TimeSpan.Zero), Next("0 3 * * *", new DateTimeOffset(2026, 10, 23, 16, 30, 0, TimeSpan.FromHours(8))));
    }

    [Fact]
    public void Parse_RejectsInvalidExpressions_AndImpossibleDatesHaveNoNext()
    {
        foreach (var invalid in new[] { "", "* * * *", "60 * * * *", "* 24 * * *", "* * 0 * *", "* * * 13 *", "* * * * 8", "a * * * *", "*/0 * * * *", "10-5 * * * *", "1/2/3 * * * *" })
        {
            Assert.False(CronExpression.TryParse(invalid, out _));
            Assert.Throws<ArgumentException>(() => CronExpression.Parse(invalid));
        }

        Assert.Equal("0 3 * * *", CronExpression.Parse("  0   3 * *   * ").Expression);
        Assert.False(CronExpression.Parse("0 0 30 2 *").Next(_friday).HasValue);
    }

    [Fact]
    public void JobSchedule_ComputesNextRun_PerKind()
    {
        Assert.False(JobSchedule.Manual.NextRun(_friday).HasValue);
        Assert.Equal(_friday.AddMinutes(5), JobSchedule.Every(TimeSpan.FromMinutes(5)).NextRun(_friday).ValueOrThrow());
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 3, 0, 0, TimeSpan.Zero), JobSchedule.FromCron("0 3 * * *").NextRun(_friday).ValueOrThrow());
        Assert.Throws<ArgumentOutOfRangeException>(static () => JobSchedule.Every(TimeSpan.Zero));
        Assert.Throws<ArgumentException>(static () => JobSchedule.FromCron("bad"));
        Assert.Equal("every 00:05:00", JobSchedule.Every(TimeSpan.FromMinutes(5)).ToString());
        Assert.Equal("cron 0 3 * * *", JobSchedule.FromCron("0 3 * * *").ToString());
        Assert.Equal("manual", JobSchedule.Manual.ToString());
    }
}
