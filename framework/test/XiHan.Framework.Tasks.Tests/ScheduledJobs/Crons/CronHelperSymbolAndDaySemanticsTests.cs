// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.Tasks.ScheduledJobs.Crons;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Crons;

/// <summary>
/// CronHelper 特殊符号支持范围、步长展开与日/星期匹配语义测试
/// </summary>
/// <remarks>
/// 这里钉死三条契约：
/// 一是 L / W / # 明确不支持（类注释曾把它们列进"支持特殊符号"，而 docs/guide/tasks.md 写的是不支持，
/// 注释已按实际行为改正，用例把"不支持"这条锁住，避免日后有人凭注释补半套实现）；
/// 二是 "n/step" 必须按步长展开，而不是静默退化成单值 n；
/// 三是日与星期都被限定时按标准 cron（Vixie/POSIX）取"或"语义，只限定一侧时仍是"与"。
/// 全部用例以固定基准时间驱动，不依赖 DateTime.Now，也不做任何真实等待。
/// </remarks>
public class CronHelperSymbolAndDaySemanticsTests
{
    /// <summary>
    /// L / W / # 属于 Quartz 扩展记号，本解析器不支持，一律判为无效表达式
    /// </summary>
    [Theory]
    [InlineData("0 0 L * *")]
    [InlineData("0 0 LW * *")]
    [InlineData("0 0 15W * *")]
    [InlineData("0 0 * * 5#3")]
    [InlineData("0 0 * * 6L")]
    [InlineData("0 0 0 L * *")]
    public void IsValidExpression_WithQuartzOnlySymbols_ReturnsFalse(string expression)
    {
        Assert.False(CronHelper.IsValidExpression(expression));
    }

    /// <summary>
    /// 解析"月末"记号时抛出 ArgumentException 并把无法识别的记号带进消息
    /// </summary>
    [Fact]
    public void ParseExpression_WithLastDayOfMonthSymbol_ThrowsWithOffendingToken()
    {
        var exception = Assert.Throws<ArgumentException>(() => CronHelper.ParseExpression("0 0 L * *"));

        Assert.Contains("L", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 计算下次触发时间时同样直接抛出，不会因为"注释说支持"而静默给出一个时刻
    /// </summary>
    [Fact]
    public void GetNextOccurrence_WithNthWeekdaySymbol_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => CronHelper.GetNextOccurrence("0 0 * * 5#3", ParseMoment("2024-06-12 00:00:00")));
    }

    /// <summary>
    /// 描述接口对这些记号降级为固定提示，不抛给调用方
    /// </summary>
    [Fact]
    public void GetDescription_WithNearestWeekdaySymbol_ReturnsFallbackText()
    {
        Assert.Equal("无效的 Cron 表达式", CronHelper.GetDescription("0 0 15W * *"));
    }

    /// <summary>
    /// "n/step" 等价于 "n-上界/step"：从 n 起按步长展开，而不是退化成单值 n
    /// </summary>
    [Fact]
    public void ParseExpression_WithStartValueAndStep_ExpandsFromStartValue()
    {
        var cron = CronHelper.ParseExpression("0/15 * * * * *");

        Assert.False(cron.Seconds.IsWildcard);
        Assert.Equal(new[] { 0, 15, 30, 45 }, cron.Seconds.Values);
    }

    /// <summary>
    /// 起点非零时从起点起跳，且末尾不越过字段上界
    /// </summary>
    [Theory]
    [InlineData("5/20 * * * *", new[] { 5, 25, 45 })]
    [InlineData("10/30 * * * *", new[] { 10, 40 })]
    [InlineData("59/5 * * * *", new[] { 59 })]
    public void ParseExpression_WithStartValueAndStep_StopsAtUpperBound(string expression, int[] expected)
    {
        var cron = CronHelper.ParseExpression(expression);

        Assert.Equal(expected, cron.Minutes.Values);
    }

    /// <summary>
    /// 上界随字段变化：小时字段按 0-23 收口
    /// </summary>
    [Fact]
    public void ParseExpression_WithStepOnHourField_RespectsFieldUpperBound()
    {
        var cron = CronHelper.ParseExpression("0 2/6 * * *");

        Assert.Equal(new[] { 2, 8, 14, 20 }, cron.Hours.Values);
    }

    /// <summary>
    /// 日期字段从 1 起算，"1/10" 展开为 1、11、21、31
    /// </summary>
    [Fact]
    public void ParseExpression_WithStepOnDayField_StartsFromGivenDay()
    {
        var cron = CronHelper.ParseExpression("0 0 1/10 * *");

        Assert.Equal(new[] { 1, 11, 21, 31 }, cron.Days.Values);
    }

    /// <summary>
    /// 步长为 1 时退化为"从 n 到上界的连续区间"，而不是单值
    /// </summary>
    [Fact]
    public void ParseExpression_WithStepOfOne_ExpandsToContiguousRange()
    {
        var cron = CronHelper.ParseExpression("0 20/1 * * *");

        Assert.Equal(new[] { 20, 21, 22, 23 }, cron.Hours.Values);
    }

    /// <summary>
    /// 起点越界或步长非法时仍然报错，展开逻辑不吞掉原有校验
    /// </summary>
    [Theory]
    [InlineData("60/5 * * * *")]
    [InlineData("5/0 * * * *")]
    [InlineData("5/abc * * * *")]
    [InlineData("abc/5 * * * *")]
    public void IsValidExpression_WithMalformedStepExpression_ReturnsFalse(string expression)
    {
        Assert.False(CronHelper.IsValidExpression(expression));
    }

    /// <summary>
    /// 步长展开真正反映到触发时刻上："0/15 * * * * *" 每 15 秒一次，而不是每分钟只在第 0 秒触发
    /// </summary>
    [Theory]
    [InlineData("2024-06-12 01:00:00", "2024-06-12 01:00:15")]
    [InlineData("2024-06-12 01:00:20", "2024-06-12 01:00:30")]
    [InlineData("2024-06-12 01:00:50", "2024-06-12 01:01:00")]
    public void GetNextOccurrence_WithStartValueAndStep_FiresOnEveryStep(string from, string expected)
    {
        var next = CronHelper.GetNextOccurrence("0/15 * * * * *", ParseMoment(from));

        Assert.NotNull(next);
        Assert.Equal(ParseMoment(expected), next!.Value);
    }

    /// <summary>
    /// "n/step" 与等价的 "n-上界/step" 解析结果完全一致
    /// </summary>
    [Fact]
    public void ParseExpression_StartValueWithStep_EqualsExplicitRangeWithStep()
    {
        var shorthand = CronHelper.ParseExpression("10/15 * * * *");
        var explicitRange = CronHelper.ParseExpression("10-59/15 * * * *");

        Assert.Equal(explicitRange.Minutes.Values, shorthand.Minutes.Values);
    }

    /// <summary>
    /// 日与星期都被限定时取"或"：每月 1 号或每周一都算命中
    /// </summary>
    [Theory]
    // 2024-07-01 是周一：两侧同时命中
    [InlineData("2024-07-01 00:00:00", true)]
    // 2024-06-01 是周六：只有"1 号"这一侧命中
    [InlineData("2024-06-01 00:00:00", true)]
    // 2024-06-10 是周一：只有"周一"这一侧命中
    [InlineData("2024-06-10 00:00:00", true)]
    // 2024-06-12 是周三、也不是 1 号：两侧都不命中
    [InlineData("2024-06-12 00:00:00", false)]
    public void IsMatch_WhenDayAndDayOfWeekBothRestricted_UsesOrSemantics(string moment, bool expected)
    {
        Assert.Equal(expected, CronHelper.IsMatch("0 0 1 * 1", ParseMoment(moment)));
    }

    /// <summary>
    /// 只限定一侧时仍是逐字段的"与"：另一侧通配不会把别的日子放进来
    /// </summary>
    [Theory]
    [InlineData("0 0 1 * *", "2024-06-01 00:00:00", true)]
    [InlineData("0 0 1 * *", "2024-06-10 00:00:00", false)]
    [InlineData("0 0 * * 1", "2024-06-10 00:00:00", true)]
    [InlineData("0 0 * * 1", "2024-06-01 00:00:00", false)]
    public void IsMatch_WhenOnlyOneSideRestricted_KeepsAndSemantics(string expression, string moment, bool expected)
    {
        Assert.Equal(expected, CronHelper.IsMatch(expression, ParseMoment(moment)));
    }

    /// <summary>
    /// 问号与星号等价：写成 "?" 的一侧同样算作未限定，仍走"与"
    /// </summary>
    [Fact]
    public void IsMatch_WhenOneSideIsQuestionMark_KeepsAndSemantics()
    {
        Assert.True(CronHelper.IsMatch("0 0 1 * ?", ParseMoment("2024-06-01 00:00:00")));
        Assert.False(CronHelper.IsMatch("0 0 1 * ?", ParseMoment("2024-06-10 00:00:00")));
    }

    /// <summary>
    /// 时分秒仍然是硬性条件："或"只作用在日与星期两个字段上
    /// </summary>
    [Fact]
    public void IsMatch_WhenTimeFieldsMismatch_OrSemanticsDoesNotRescueTheMoment()
    {
        Assert.False(CronHelper.IsMatch("0 0 1 * 1", ParseMoment("2024-06-10 09:00:00")));
        Assert.False(CronHelper.IsMatch("0 0 1 * 1", ParseMoment("2024-06-01 00:30:00")));
    }

    /// <summary>
    /// 月份字段同样是硬性条件，不参与日与星期的"或"
    /// </summary>
    [Fact]
    public void IsMatch_WhenMonthMismatch_ReturnsFalse()
    {
        Assert.False(CronHelper.IsMatch("0 0 1 7 1", ParseMoment("2024-06-10 00:00:00")));
        Assert.True(CronHelper.IsMatch("0 0 1 7 1", ParseMoment("2024-07-08 00:00:00")));
    }

    /// <summary>
    /// 下次触发时间同样走"或"：先等到最近的那一侧（周一 6-17），而不是拖到下个月 1 号
    /// </summary>
    [Fact]
    public void GetNextOccurrence_WhenBothDayFieldsRestricted_TakesTheEarlierSide()
    {
        var next = CronHelper.GetNextOccurrence("0 0 1 * 1", ParseMoment("2024-06-12 12:00:00"));

        Assert.NotNull(next);
        Assert.Equal(ParseMoment("2024-06-17 00:00:00"), next!.Value);
    }

    /// <summary>
    /// 6 段表达式的分钟级预筛与 IsMatch 用的是同一套日/星期语义，按秒推进不会漏掉"或"的那一侧
    /// </summary>
    [Fact]
    public void GetNextOccurrence_WithSixPartExpression_AppliesSameDaySemantics()
    {
        var next = CronHelper.GetNextOccurrence("30 0 0 1 * 1", ParseMoment("2024-06-12 12:00:00"));

        Assert.NotNull(next);
        Assert.Equal(ParseMoment("2024-06-17 00:00:30"), next!.Value);
    }

    /// <summary>
    /// 上一次触发时间同样按"或"回溯
    /// </summary>
    [Fact]
    public void GetPreviousOccurrence_WhenBothDayFieldsRestricted_TakesTheLaterSide()
    {
        var previous = CronHelper.GetPreviousOccurrence("0 0 1 * 1", ParseMoment("2024-06-12 12:00:00"));

        Assert.NotNull(previous);
        Assert.Equal(ParseMoment("2024-06-10 00:00:00"), previous!.Value);
    }

    /// <summary>
    /// 连续取多次时把两侧的命中按时间顺序合并起来
    /// </summary>
    [Fact]
    public void GetNextOccurrences_WhenBothDayFieldsRestricted_MergesBothSidesInOrder()
    {
        var occurrences = CronHelper.GetNextOccurrences("0 0 1 * 1", 3, ParseMoment("2024-06-25 12:00:00"));

        Assert.Equal(
            new[]
            {
                ParseMoment("2024-07-01 00:00:00"),
                ParseMoment("2024-07-08 00:00:00"),
                ParseMoment("2024-07-15 00:00:00")
            },
            occurrences);
    }

    /// <summary>
    /// 解析固定格式的基准时间，避免受运行机器区域设置影响
    /// </summary>
    private static DateTime ParseMoment(string value)
    {
        return DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
