// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics;
using System.Globalization;
using XiHan.Framework.Scheduling.Abstractions.Schedules;

namespace XiHan.Framework.Scheduling.Abstractions.Tests.Schedules;

/// <summary>
/// cron 表达式的解析（5、6、7 段与 Quartz 式特殊写法）、下一次匹配（UTC 与指定时区）与边界。
/// </summary>
public sealed class CronExpressionTests
{
    /// <summary>
    /// 2026-10-23 周五 08:30:15 UTC。
    /// </summary>
    private static readonly DateTimeOffset _friday = new(2026, 10, 23, 8, 30, 15, TimeSpan.Zero);

    /// <summary>
    /// 把 ISO 8601 文本（含 <c>Z</c> 或偏移）解析成时刻。
    /// </summary>
    private static DateTimeOffset At(string iso)
    {
        return DateTimeOffset.ParseExact(iso, "yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    /// <summary>
    /// 表达式在 <paramref name="after"/>（默认基准时刻）之后按 UTC 的下一次匹配。
    /// </summary>
    private static DateTimeOffset Next(string expression, DateTimeOffset? after = null)
    {
        return CronExpression.Parse(expression).Next(after ?? _friday).ValueOrThrow();
    }

    /// <summary>
    /// 表达式在 <paramref name="after"/> 之后按指定时区的下一次匹配。
    /// </summary>
    private static DateTimeOffset NextIn(string expression, DateTimeOffset after, TimeZoneInfo timeZone)
    {
        return CronExpression.Parse(expression).Next(after, timeZone).ValueOrThrow();
    }

    /// <summary>
    /// 断言墙上时间与偏移都相等（<see cref="DateTimeOffset"/> 的相等只比较时刻，不比较偏移）。
    /// </summary>
    private static void AssertExact(DateTimeOffset expected, DateTimeOffset actual)
    {
        Assert.Equal(expected.DateTime, actual.DateTime);
        Assert.Equal(expected.Offset, actual.Offset);
    }

    /// <summary>
    /// 按 IANA 标识找时区；没有 ICU 的 Windows 上退回对应的 Windows 标识。
    /// </summary>
    private static TimeZoneInfo FindTimeZone(string ianaId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException) when (TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
    }

    [Theory]
    [InlineData("0 9 * * *", "0 0 9 * * *")]
    [InlineData("*/15 * * * *", "0 */15 * * * *")]
    [InlineData("0 0 1 * 1", "0 0 0 1 * 1")]
    [InlineData("30 2 * * 1-5", "0 30 2 * * 1-5")]
    [InlineData("0 0 L * 5L", "0 0 0 L * 5L")]
    public void Next_FiveFieldAndSixFieldForms_AreEquivalent(string fiveFields, string sixFields)
    {
        var after = _friday;
        for (var step = 0; step < 5; step++)
        {
            var expected = Next(fiveFields, after);
            Assert.Equal(expected, Next(sixFields, after));
            after = expected;
        }
    }

    [Theory]
    [InlineData("*/15 * * * * *", "2026-10-23T08:30:15Z", "2026-10-23T08:30:30Z")]
    [InlineData("5 * * * * *", "2026-10-23T08:30:15Z", "2026-10-23T08:31:05Z")]
    [InlineData("0,30 * * * * *", "2026-10-23T08:30:15Z", "2026-10-23T08:30:30Z")]
    [InlineData("30 0 9 * * *", "2026-10-23T08:30:15Z", "2026-10-23T09:00:30Z")]
    [InlineData("0 0 0 * * *", "2026-10-23T08:30:15Z", "2026-10-24T00:00:00Z")]
    [InlineData("20/20 * * * * *", "2026-10-23T08:30:15Z", "2026-10-23T08:30:20Z")]
    public void Next_WithSecondsField_MatchesSeconds(string expression, string after, string expected)
    {
        Assert.Equal(At(expected), Next(expression, At(after)));
    }

    [Fact]
    public void Next_FiveFieldExpression_MatchesOnlyWholeMinutes()
    {
        Assert.Equal(At("2026-10-23T08:31:00Z"), Next("* * * * *"));
        Assert.Equal(At("2026-10-23T08:31:00Z"), Next("* * * * *", At("2026-10-23T08:30:59Z")));
    }

    [Theory]
    [InlineData("0 3 * * *", "2026-10-24T03:00:00Z")]
    [InlineData("30 * * * *", "2026-10-23T09:30:00Z")]
    [InlineData("*/15 * * * *", "2026-10-23T08:45:00Z")]
    [InlineData("5-10/5 9-17 * * *", "2026-10-23T09:05:00Z")]
    [InlineData("0 9 * * 1-5", "2026-10-23T09:00:00Z")]
    [InlineData("0 0 1 1 *", "2027-01-01T00:00:00Z")]
    [InlineData("0 0,12 * * *", "2026-10-23T12:00:00Z")]
    [InlineData("59 23 31 12 *", "2026-12-31T23:59:00Z")]
    [InlineData("0 0 1/10 * *", "2026-10-31T00:00:00Z")]
    public void Next_StandardFields_MatchStandardCronSemantics(string expression, string expected)
    {
        Assert.Equal(At(expected), Next(expression));
    }

    [Fact]
    public void Next_WeekdayRange_RollsToNextWeek()
    {
        Assert.Equal(At("2026-10-26T09:00:00Z"), Next("0 9 * * 1-5", At("2026-10-23T10:00:00Z")));
    }

    [Theory]
    [InlineData("0 9 ? * MON", "2026-10-26T09:00:00Z")]
    [InlineData("0 9 15 * ?", "2026-11-15T09:00:00Z")]
    [InlineData("0 0 9 ? * *", "2026-10-23T09:00:00Z")]
    [InlineData("0 9 ? * ?", "2026-10-23T09:00:00Z")]
    public void Next_QuestionMark_MeansUnrestricted(string expression, string expected)
    {
        Assert.Equal(At(expected), Next(expression));
    }

    [Theory]
    [InlineData("0 9 * JAN-MAR mon-fri", "2027-01-01T09:00:00Z")]
    [InlineData("0 0 1 dec *", "2026-12-01T00:00:00Z")]
    [InlineData("0 0 * * Sat,sun", "2026-10-24T00:00:00Z")]
    [InlineData("0 0 * oct FRI", "2026-10-30T00:00:00Z")]
    [InlineData("0 0 * Nov,JAN *", "2026-11-01T00:00:00Z")]
    public void Next_MonthAndWeekdayNames_AreCaseInsensitive(string expression, string expected)
    {
        Assert.Equal(At(expected), Next(expression));
    }

    [Theory]
    [InlineData("0 0 * * 7", "2026-10-25T00:00:00Z")]
    [InlineData("0 0 * * 0", "2026-10-25T00:00:00Z")]
    [InlineData("0 0 * * 5-7", "2026-10-24T00:00:00Z")]
    [InlineData("0 0 * * 1", "2026-10-26T00:00:00Z")]
    public void Next_WeekdayNumbers_FollowUnixConvention(string expression, string expected)
    {
        Assert.Equal(At(expected), Next(expression));
    }

    [Theory]
    [InlineData("0 0 L * *", "2026-10-23T08:30:15Z", "2026-10-31T00:00:00Z")]
    [InlineData("0 0 L * *", "2026-02-01T00:00:00Z", "2026-02-28T00:00:00Z")]
    [InlineData("0 0 L * *", "2028-02-01T00:00:00Z", "2028-02-29T00:00:00Z")]
    [InlineData("0 0 L-0 * *", "2026-10-23T08:30:15Z", "2026-10-31T00:00:00Z")]
    [InlineData("0 0 L-3 * *", "2026-10-23T08:30:15Z", "2026-10-28T00:00:00Z")]
    [InlineData("0 0 L-3 * *", "2026-02-01T00:00:00Z", "2026-02-25T00:00:00Z")]
    [InlineData("0 0 L-29 * *", "2026-02-01T00:00:00Z", "2026-03-02T00:00:00Z")]
    [InlineData("0 0 l * *", "2026-10-23T08:30:15Z", "2026-10-31T00:00:00Z")]
    public void Next_LastDayOfMonth_WithOptionalOffset(string expression, string after, string expected)
    {
        Assert.Equal(At(expected), Next(expression, At(after)));
    }

    [Theory]
    [InlineData("0 0 15W * *", "2026-08-01T00:00:00Z", "2026-08-14T00:00:00Z")]
    [InlineData("0 0 15W * *", "2026-11-01T00:00:00Z", "2026-11-16T00:00:00Z")]
    [InlineData("0 0 15W * *", "2026-10-01T00:00:00Z", "2026-10-15T00:00:00Z")]
    [InlineData("0 0 1W * *", "2026-07-31T00:00:00Z", "2026-08-03T00:00:00Z")]
    [InlineData("0 0 31W * *", "2026-10-01T00:00:00Z", "2026-10-30T00:00:00Z")]
    [InlineData("0 0 31W * *", "2026-05-01T00:00:00Z", "2026-05-29T00:00:00Z")]
    [InlineData("0 0 31W * *", "2026-04-01T00:00:00Z", "2026-05-29T00:00:00Z")]
    [InlineData("0 0 15w * *", "2026-08-01T00:00:00Z", "2026-08-14T00:00:00Z")]
    public void Next_NearestWeekday_StaysInMonth(string expression, string after, string expected)
    {
        Assert.Equal(At(expected), Next(expression, At(after)));
    }

    [Theory]
    [InlineData("0 0 LW * *", "2026-10-23T08:30:15Z", "2026-10-30T00:00:00Z")]
    [InlineData("0 0 LW * *", "2026-05-01T00:00:00Z", "2026-05-29T00:00:00Z")]
    [InlineData("0 0 LW * *", "2026-11-01T00:00:00Z", "2026-11-30T00:00:00Z")]
    public void Next_LastWeekdayOfMonth(string expression, string after, string expected)
    {
        Assert.Equal(At(expected), Next(expression, At(after)));
    }

    [Theory]
    [InlineData("0 0 * * 5L", "2026-10-30T00:00:00Z")]
    [InlineData("0 0 * * FRIL", "2026-10-30T00:00:00Z")]
    [InlineData("0 0 * * 7L", "2026-10-25T00:00:00Z")]
    [InlineData("0 0 * * 0L", "2026-10-25T00:00:00Z")]
    [InlineData("0 0 * * sunL", "2026-10-25T00:00:00Z")]
    [InlineData("0 0 * * 1L", "2026-10-26T00:00:00Z")]
    public void Next_LastGivenWeekdayOfMonth(string expression, string expected)
    {
        Assert.Equal(At(expected), Next(expression));
    }

    [Theory]
    [InlineData("0 0 * * FRI#2", "2026-10-23T08:30:15Z", "2026-11-13T00:00:00Z")]
    [InlineData("0 0 * * 5#2", "2026-10-01T00:00:00Z", "2026-10-09T00:00:00Z")]
    [InlineData("0 0 * * 1#1", "2026-10-23T08:30:15Z", "2026-11-02T00:00:00Z")]
    [InlineData("0 0 * * MON#5", "2026-10-23T08:30:15Z", "2026-11-30T00:00:00Z")]
    [InlineData("0 0 * * 7#1", "2026-10-23T08:30:15Z", "2026-11-01T00:00:00Z")]
    public void Next_NthWeekdayOfMonth_SkipsMonthsWithoutIt(string expression, string after, string expected)
    {
        Assert.Equal(At(expected), Next(expression, At(after)));
    }

    [Theory]
    [InlineData("0 0 1 * 1", "2026-10-26T00:00:00Z")]
    [InlineData("0 0 L * 5L", "2026-10-30T00:00:00Z")]
    [InlineData("0 0 15W * MON#5", "2026-11-16T00:00:00Z")]
    public void Next_DayAndWeekdayBothRestricted_TakesUnion(string expression, string expected)
    {
        Assert.Equal(At(expected), Next(expression));
    }

    [Theory]
    [InlineData("0 0 0 1 1 * 2028", "2028-01-01T00:00:00Z")]
    [InlineData("0 0 0 1 1 * *", "2027-01-01T00:00:00Z")]
    [InlineData("0 0 0 1 1 * 2026-2027", "2027-01-01T00:00:00Z")]
    [InlineData("0 0 0 1 1 * 2026/4", "2030-01-01T00:00:00Z")]
    [InlineData("0 0 0 1 1 * 2027,2029", "2027-01-01T00:00:00Z")]
    [InlineData("0 0 0 1 1 * */3", "2027-01-01T00:00:00Z")]
    public void Next_YearField_RestrictsYears(string expression, string expected)
    {
        Assert.Equal(At(expected), Next(expression));
    }

    [Theory]
    [InlineData("0 0 0 1 1 * 2040")]
    [InlineData("0 0 0 1 1 * 2020")]
    [InlineData("0 0 30 2 *")]
    [InlineData("0 0 31 4,6,9,11 *")]
    [InlineData("0 0 0 ? * MON#5 2032")]
    public void Next_NoMatchWithinFiveYears_ReturnsNone(string expression)
    {
        Assert.False(CronExpression.Parse(expression).Next(_friday).HasValue);
    }

    [Theory]
    [InlineData("5/15 * * * *", "2026-10-23T08:35:00Z")]
    [InlineData("0 8/4 * * *", "2026-10-23T12:00:00Z")]
    [InlineData("0 0 * 1/6 *", "2027-01-01T00:00:00Z")]
    [InlineData("0 0 * * 1/3", "2026-10-25T00:00:00Z")]
    public void Next_StepFromValue_StartsAtValue(string expression, string expected)
    {
        Assert.Equal(At(expected), Next(expression));
    }

    [Fact]
    public void Next_ResultIsStrictlyAfterInput()
    {
        Assert.Equal(At("2026-10-24T09:00:00Z"), Next("0 9 * * *", At("2026-10-23T09:00:00Z")));
        Assert.Equal(At("2026-10-23T09:00:00Z"), Next("0 9 * * *", new DateTimeOffset(2026, 10, 23, 8, 59, 59, 999, TimeSpan.Zero)));
        Assert.Equal(At("2026-10-23T08:30:16Z"), Next("* * * * * *", new DateTimeOffset(2026, 10, 23, 8, 30, 15, 250, TimeSpan.Zero)));
        Assert.Equal(At("2026-10-23T08:30:16Z"), Next("* * * * * *", At("2026-10-23T08:30:15Z")));
    }

    [Fact]
    public void Next_UtcOverload_IgnoresInputOffset()
    {
        var after = At("2026-10-23T16:30:00+08:00");
        var expected = At("2026-10-24T03:00:00Z");
        AssertExact(expected, Next("0 3 * * *", after));
        AssertExact(expected, NextIn("0 3 * * *", after, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Next_InShanghai_MatchesWallClockAndCarriesOffset()
    {
        var shanghai = FindTimeZone("Asia/Shanghai");

        AssertExact(At("2026-10-23T09:00:00+08:00"), NextIn("0 9 * * *", At("2026-10-23T00:00:00Z"), shanghai));
        AssertExact(At("2026-10-24T09:00:00+08:00"), NextIn("0 9 * * *", At("2026-10-23T02:00:00Z"), shanghai));
        AssertExact(At("2026-11-01T00:00:00+08:00"), NextIn("0 0 1 * *", _friday, shanghai));
    }

    [Fact]
    public void Next_InNewYork_SpringForwardSkipsNonexistentWallTime()
    {
        var newYork = FindTimeZone("America/New_York");

        AssertExact(At("2026-03-07T02:30:00-05:00"), NextIn("30 2 * * *", At("2026-03-07T01:00:00-05:00"), newYork));
        AssertExact(At("2026-03-09T02:30:00-04:00"), NextIn("30 2 * * *", At("2026-03-08T01:00:00-05:00"), newYork));
        AssertExact(At("2026-03-08T03:00:00-04:00"), NextIn("0 * * * *", At("2026-03-08T01:00:00-05:00"), newYork));
    }

    [Fact]
    public void Next_InNewYork_FallBackFiresOnlyOnFirstOccurrence()
    {
        var newYork = FindTimeZone("America/New_York");

        var first = NextIn("30 1 * * *", At("2026-11-01T00:00:00-04:00"), newYork);
        AssertExact(At("2026-11-01T01:30:00-04:00"), first);
        AssertExact(At("2026-11-02T01:30:00-05:00"), NextIn("30 1 * * *", first, newYork));
        AssertExact(At("2026-11-02T01:30:00-05:00"), NextIn("30 1 * * *", At("2026-11-01T01:45:00-05:00"), newYork));
    }

    [Fact]
    public void Next_InNewYork_HourlyAcrossFallBack_SkipsRepeatedHour()
    {
        var newYork = FindTimeZone("America/New_York");

        var one = NextIn("0 * * * *", At("2026-11-01T00:30:00-04:00"), newYork);
        AssertExact(At("2026-11-01T01:00:00-04:00"), one);
        AssertExact(At("2026-11-01T02:00:00-05:00"), NextIn("0 * * * *", one, newYork));
    }

    [Fact]
    public void Next_NullTimeZone_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CronExpression.Parse("0 9 * * *").Next(_friday, null!));
    }

    [Fact]
    public void Next_LeapDayEveryFourYears_ReturnsQuickly()
    {
        var fiveFields = CronExpression.Parse("0 0 29 2 *");
        var sixFields = CronExpression.Parse("0 0 0 29 2 ?");
        var stopwatch = Stopwatch.StartNew();

        for (var round = 0; round < 100; round++)
        {
            Assert.Equal(At("2028-02-29T00:00:00Z"), fiveFields.Next(_friday).ValueOrThrow());
            Assert.Equal(At("2028-02-29T00:00:00Z"), sixFields.Next(_friday).ValueOrThrow());
            Assert.False(CronExpression.Parse("0 0 30 2 *").Next(_friday).HasValue);
        }

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Took {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData("", "the expression is empty")]
    [InlineData("   ", "the expression is empty")]
    [InlineData("* * * *", "expected 5, 6 or 7 fields but found 4")]
    [InlineData("* * * * * * * *", "expected 5, 6 or 7 fields but found 8")]
    [InlineData("60 * * * *", "value '60' in field 'minute' is out of range 0-59")]
    [InlineData("* 24 * * *", "value '24' in field 'hour' is out of range 0-23")]
    [InlineData("* * 0 * *", "value '0' in field 'day-of-month' is out of range 1-31")]
    [InlineData("* * * 13 *", "value '13' in field 'month' is out of range 1-12")]
    [InlineData("* * * * 8", "value '8' in field 'day-of-week' is out of range 0-7")]
    [InlineData("60 * * * * *", "value '60' in field 'second' is out of range 0-59")]
    [InlineData("0 0 0 1 1 * 1969", "value '1969' in field 'year' is out of range 1970-2099")]
    [InlineData("0 0 0 1 1 * 2100", "value '2100' in field 'year' is out of range 1970-2099")]
    [InlineData("a * * * *", "unknown token 'a' in field 'minute'")]
    [InlineData("0 0 * JAN,FOO *", "unknown token 'FOO' in field 'month'")]
    [InlineData("0 0 * * L", "unknown token 'L' in field 'day-of-week'")]
    [InlineData("0 0 W * *", "unknown token 'W' in field 'day-of-month'")]
    [InlineData("0 0 1-L * *", "unknown token 'L' in field 'day-of-month'")]
    [InlineData("*/0 * * * *", "step '0' in field 'minute' must be a positive number")]
    [InlineData("1/2/3 * * * *", "step '2/3' in field 'minute' must be a positive number")]
    [InlineData("10-5 * * * *", "range '10-5' in field 'minute' has its start after its end")]
    [InlineData("? * * * *", "'?' is only allowed in the day-of-month and day-of-week fields")]
    [InlineData("0 0 32W * *", "value '32' in field 'day-of-month' is out of range 1-31")]
    [InlineData("0 0 L-31 * *", "offset '31' after 'L-' in field 'day-of-month' must be 0-30")]
    [InlineData("0 0 * * 1#6", "ordinal '6' after '#' in field 'day-of-week' must be 1-5")]
    [InlineData("0 0 * * 1#0", "ordinal '0' after '#' in field 'day-of-week' must be 1-5")]
    [InlineData("0 0 * * 1#", "ordinal '' after '#' in field 'day-of-week' must be 1-5")]
    [InlineData("0 0 * * MON-", "missing value in field 'day-of-week'")]
    [InlineData("0 0 * * 1,,2", "missing value in field 'day-of-week'")]
    [InlineData("-5 * * * *", "missing value in field 'minute'")]
    public void Parse_InvalidExpression_ThrowsWithReason(string expression, string reason)
    {
        Assert.False(CronExpression.TryParse(expression, out var parsed));
        Assert.Null(parsed);

        var exception = Assert.Throws<ArgumentException>(() => CronExpression.Parse(expression));

        Assert.Equal("expression", exception.ParamName);
        Assert.StartsWith($"Cron expression '{expression}' is invalid: {reason}.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(static () => CronExpression.Parse(null!));
        Assert.False(CronExpression.TryParse(null, out var parsed));
        Assert.Null(parsed);
    }

    [Theory]
    [InlineData("  0   3 * *   * ", "0 3 * * *")]
    [InlineData("0\t3 * * *", "0 3 * * *")]
    [InlineData("0 0 * jan Mon", "0 0 * jan Mon")]
    [InlineData(" 0 0 0  L-3 * 5L  2026-2030 ", "0 0 0 L-3 * 5L 2026-2030")]
    public void Expression_IsNormalized_AndToStringMatches(string input, string expected)
    {
        var parsed = CronExpression.Parse(input);

        Assert.Equal(expected, parsed.Expression);
        Assert.Equal(expected, parsed.ToString());
    }

    [Theory]
    [InlineData("0 9 * * *", false, false)]
    [InlineData("0 0 9 * * *", true, false)]
    [InlineData("0 0 9 * * * *", true, true)]
    [InlineData("0 0 9 ? * MON#2 2027", true, true)]
    public void HasSecondsAndIncludesYears_ReflectFieldCount(string expression, bool hasSeconds, bool includesYears)
    {
        var parsed = CronExpression.Parse(expression);

        Assert.Equal(hasSeconds, parsed.HasSeconds);
        Assert.Equal(includesYears, parsed.IncludesYears);
    }
}
