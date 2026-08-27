// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Globalization;
using XiHan.Framework.Tasks.ScheduledJobs.Crons;

namespace XiHan.Framework.Tasks.Tests.ScheduledJobs.Crons;

/// <summary>
/// CronHelper 表达式解析与触发时间计算测试
/// </summary>
/// <remarks>
/// 全部用例都以固定基准时间驱动，不依赖 DateTime.Now，也不做任何真实等待。
/// 时间计算断言锁的是"下一次/上一次触发时刻"这一对外契约，不锁内部逐分钟扫描的实现方式。
/// </remarks>
public class CronHelperTests
{
    /// <summary>
    /// 兜底超时：逐分钟扫描最多回溯 4 年，理论上限内必须完成
    /// </summary>
    private const int TimeoutMilliseconds = 60_000;

    /// <summary>
    /// 合法表达式（含 5 位、6 位、预定义、名称、步长、范围、列表）应通过校验
    /// </summary>
    [Theory]
    [InlineData("* * * * *")]
    [InlineData("0 0 * * *")]
    [InlineData("*/5 * * * *")]
    [InlineData("15,45 * * * *")]
    [InlineData("0 9-17 * * 1-5")]
    [InlineData("0-30/10 * * * *")]
    [InlineData("0 0 1 JAN *")]
    [InlineData("0 0 * * MON")]
    [InlineData("0 0 * * sun")]
    [InlineData("@daily")]
    [InlineData("@YEARLY")]
    [InlineData("0 0 0 * * *")]
    [InlineData("*/15 * * * * *")]
    [InlineData("0 0 12 ? * ?")]
    public void IsValidExpression_WhenExpressionIsWellFormed_ReturnsTrue(string expression)
    {
        Assert.True(CronHelper.IsValidExpression(expression));
    }

    /// <summary>
    /// 非法表达式（段数错误、越界、逆序范围、非法步长、无法识别的记号）应被拒绝
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("* * * *")]
    [InlineData("* * * * * * *")]
    [InlineData("60 * * * *")]
    [InlineData("* 24 * * *")]
    [InlineData("* * 0 * *")]
    [InlineData("* * 32 * *")]
    [InlineData("* * * 13 *")]
    [InlineData("* * * * 7")]
    [InlineData("5-1 * * * *")]
    [InlineData("*/0 * * * *")]
    [InlineData("*/abc * * * *")]
    [InlineData("1-2-3 * * * *")]
    [InlineData("abc * * * *")]
    [InlineData("* * * FOO *")]
    public void IsValidExpression_WhenExpressionIsMalformed_ReturnsFalse(string expression)
    {
        Assert.False(CronHelper.IsValidExpression(expression));
    }

    /// <summary>
    /// 空白表达式解析时抛出 ArgumentException 并指明参数名
    /// </summary>
    [Fact]
    public void ParseExpression_WhenExpressionIsBlank_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => CronHelper.ParseExpression("   "));

        Assert.Equal("cronExpression", exception.ParamName);
    }

    /// <summary>
    /// 段数不是 5 或 6 时抛出 ArgumentException，且消息里带出实际段数
    /// </summary>
    [Fact]
    public void ParseExpression_WhenPartCountIsNotFiveOrSix_ThrowsArgumentExceptionWithCount()
    {
        var exception = Assert.Throws<ArgumentException>(() => CronHelper.ParseExpression("* * * *"));

        Assert.Contains("4", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 5 位表达式解析出的各字段值与通配标记正确
    /// </summary>
    [Fact]
    public void ParseExpression_WithFivePartExpression_MapsFieldsInOrder()
    {
        var cron = CronHelper.ParseExpression("30 2 1 6 3");

        Assert.False(cron.HasSeconds);
        Assert.Equal(new[] { 30 }, cron.Minutes.Values);
        Assert.Equal(new[] { 2 }, cron.Hours.Values);
        Assert.Equal(new[] { 1 }, cron.Days.Values);
        Assert.Equal(new[] { 6 }, cron.Months.Values);
        Assert.Equal(new[] { 3 }, cron.DaysOfWeek.Values);
    }

    /// <summary>
    /// 6 位表达式的第一段是秒，且 HasSeconds 置位
    /// </summary>
    [Fact]
    public void ParseExpression_WithSixPartExpression_TreatsFirstPartAsSeconds()
    {
        var cron = CronHelper.ParseExpression("5 30 2 1 6 3");

        Assert.True(cron.HasSeconds);
        Assert.Equal(new[] { 5 }, cron.Seconds.Values);
        Assert.Equal(new[] { 30 }, cron.Minutes.Values);
        Assert.Equal(new[] { 2 }, cron.Hours.Values);
    }

    /// <summary>
    /// 星号与问号都表示通配
    /// </summary>
    [Theory]
    [InlineData("*")]
    [InlineData("?")]
    public void ParseExpression_WithWildcardToken_MarksFieldAsWildcard(string token)
    {
        var cron = CronHelper.ParseExpression($"{token} * * * *");

        Assert.True(cron.Minutes.IsWildcard);
        Assert.Empty(cron.Minutes.Values);
    }

    /// <summary>
    /// 步长表达式展开为等差序列，且不再是通配字段
    /// </summary>
    [Fact]
    public void ParseExpression_WithStepValue_ExpandsToArithmeticSequence()
    {
        var cron = CronHelper.ParseExpression("*/5 * * * *");

        Assert.False(cron.Minutes.IsWildcard);
        Assert.Equal(12, cron.Minutes.Values.Count);
        Assert.Equal(new[] { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55 }, cron.Minutes.Values);
    }

    /// <summary>
    /// 范围表达式展开为闭区间全部取值
    /// </summary>
    [Fact]
    public void ParseExpression_WithRange_ExpandsInclusiveBounds()
    {
        var cron = CronHelper.ParseExpression("1-5 * * * *");

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, cron.Minutes.Values);
    }

    /// <summary>
    /// 范围叠加步长时按步长跳跃取值
    /// </summary>
    [Fact]
    public void ParseExpression_WithRangeAndStep_SkipsByStep()
    {
        var cron = CronHelper.ParseExpression("0-30/10 * * * *");

        Assert.Equal(new[] { 0, 10, 20, 30 }, cron.Minutes.Values);
    }

    /// <summary>
    /// 列表表达式去重并升序排列
    /// </summary>
    [Fact]
    public void ParseExpression_WithDuplicatedList_DistinctsAndSortsValues()
    {
        var cron = CronHelper.ParseExpression("45,15,45 * * * *");

        Assert.Equal(new[] { 15, 45 }, cron.Minutes.Values);
    }

    /// <summary>
    /// 月份与星期名称大小写不敏感地映射为数值
    /// </summary>
    [Theory]
    [InlineData("0 0 1 jan *", 1)]
    [InlineData("0 0 1 DEC *", 12)]
    public void ParseExpression_WithMonthName_MapsToMonthNumber(string expression, int expectedMonth)
    {
        var cron = CronHelper.ParseExpression(expression);

        Assert.Equal(new[] { expectedMonth }, cron.Months.Values);
    }

    /// <summary>
    /// 星期名称 SUN 映射为 0，SAT 映射为 6
    /// </summary>
    [Theory]
    [InlineData("0 0 * * SUN", 0)]
    [InlineData("0 0 * * sat", 6)]
    public void ParseExpression_WithDayName_MapsToDayOfWeekNumber(string expression, int expectedDay)
    {
        var cron = CronHelper.ParseExpression(expression);

        Assert.Equal(new[] { expectedDay }, cron.DaysOfWeek.Values);
    }

    /// <summary>
    /// 首尾空白与制表分隔符不影响解析
    /// </summary>
    [Fact]
    public void ParseExpression_WithTabsAndPadding_ParsesSameAsNormalized()
    {
        var cron = CronHelper.ParseExpression(" 0\t2 * * * ");

        Assert.Equal(new[] { 0 }, cron.Minutes.Values);
        Assert.Equal(new[] { 2 }, cron.Hours.Values);
    }

    /// <summary>
    /// 预定义表达式被展开为等价的 5 位表达式
    /// </summary>
    [Fact]
    public void ParseExpression_WithPredefinedAlias_ExpandsToEquivalentFields()
    {
        var cron = CronHelper.ParseExpression("@daily");

        Assert.Equal(new[] { 0 }, cron.Minutes.Values);
        Assert.Equal(new[] { 0 }, cron.Hours.Values);
        Assert.True(cron.Days.IsWildcard);
        Assert.True(cron.Months.IsWildcard);
        Assert.True(cron.DaysOfWeek.IsWildcard);
    }

    /// <summary>
    /// 预定义表达式大小写不敏感
    /// </summary>
    [Fact]
    public void ParseExpression_WithUpperCaseAlias_IsCaseInsensitive()
    {
        var cron = CronHelper.ParseExpression("@MIDNIGHT");

        Assert.Equal(new[] { 0 }, cron.Minutes.Values);
        Assert.Equal(new[] { 0 }, cron.Hours.Values);
    }

    /// <summary>
    /// 表驱动验证下一次触发时刻：覆盖步长、范围、列表、月末、星期、名称、预定义与秒级表达式
    /// </summary>
    [Theory]
    // 每日固定时刻
    [InlineData("0 2 * * *", "2024-06-12 01:30:00", "2024-06-12 02:00:00")]
    // 起始时刻本身命中时应跳到下一次，不能返回原时刻
    [InlineData("0 2 * * *", "2024-06-12 02:00:00", "2024-06-13 02:00:00")]
    // 步长
    [InlineData("*/5 * * * *", "2024-06-12 01:31:00", "2024-06-12 01:35:00")]
    [InlineData("*/5 * * * *", "2024-06-12 01:59:30", "2024-06-12 02:00:00")]
    // 列表
    [InlineData("15,45 * * * *", "2024-06-12 01:20:00", "2024-06-12 01:45:00")]
    // 范围
    [InlineData("0 9-17 * * *", "2024-06-12 08:10:00", "2024-06-12 09:00:00")]
    [InlineData("0 0 1-5 * *", "2024-06-06 00:00:00", "2024-07-01 00:00:00")]
    // 月末：2 月没有 31 号，必须跳到 3 月
    [InlineData("0 0 31 * *", "2024-01-31 12:00:00", "2024-03-31 00:00:00")]
    // 星期（2024-06-12 是周三，下一个周一是 06-17）
    [InlineData("0 0 * * 1", "2024-06-12 12:00:00", "2024-06-17 00:00:00")]
    [InlineData("0 0 * * MON", "2024-06-12 12:00:00", "2024-06-17 00:00:00")]
    // 指定月份需跨年
    [InlineData("0 0 1 JAN *", "2024-06-12 12:00:00", "2025-01-01 00:00:00")]
    // 预定义
    [InlineData("@daily", "2024-06-12 05:00:00", "2024-06-13 00:00:00")]
    [InlineData("@hourly", "2024-06-12 05:30:00", "2024-06-12 06:00:00")]
    [InlineData("@weekly", "2024-06-12 05:30:00", "2024-06-16 00:00:00")]
    [InlineData("@monthly", "2024-06-12 05:30:00", "2024-07-01 00:00:00")]
    [InlineData("@yearly", "2024-06-12 05:30:00", "2025-01-01 00:00:00")]
    // 秒级表达式
    [InlineData("30 * * * * *", "2024-06-12 01:00:00", "2024-06-12 01:00:30")]
    [InlineData("*/15 * * * * *", "2024-06-12 01:00:05", "2024-06-12 01:00:15")]
    [InlineData("0 0 2 * * *", "2024-06-12 01:00:00", "2024-06-12 02:00:00")]
    // 秒位不含当前秒时应滚动到下一分钟
    [InlineData("0 * * * * *", "2024-06-12 01:00:30", "2024-06-12 01:01:00")]
    public void GetNextOccurrence_WithFixedBaseTime_ReturnsExpectedMoment(string expression, string from, string expected)
    {
        var next = CronHelper.GetNextOccurrence(expression, ParseMoment(from));

        Assert.NotNull(next);
        Assert.Equal(ParseMoment(expected), next!.Value);
    }

    /// <summary>
    /// 永远不可能命中的日期组合（2 月 30 日）返回 null 而不是死循环
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public void GetNextOccurrence_WhenDateCombinationNeverOccurs_ReturnsNull()
    {
        var next = CronHelper.GetNextOccurrence("0 0 30 2 *", ParseMoment("2024-06-12 00:00:00"));

        Assert.Null(next);
    }

    /// <summary>
    /// 非法表达式在计算下次时间时直接抛出，而不是静默返回 null
    /// </summary>
    [Fact]
    public void GetNextOccurrence_WhenExpressionIsInvalid_Throws()
    {
        Assert.Throws<ArgumentException>(() => CronHelper.GetNextOccurrence("bad expression here", ParseMoment("2024-06-12 00:00:00")));
    }

    /// <summary>
    /// 传入已解析对象的重载与字符串重载结果一致
    /// </summary>
    [Fact]
    public void GetNextOccurrence_WithParsedExpression_MatchesStringOverload()
    {
        var from = ParseMoment("2024-06-12 01:30:00");
        var cron = CronHelper.ParseExpression("0 2 * * *");

        var fromObject = CronHelper.GetNextOccurrence(cron, from);
        var fromString = CronHelper.GetNextOccurrence("0 2 * * *", from);

        Assert.Equal(fromString, fromObject);
    }

    /// <summary>
    /// 表驱动验证上一次触发时刻，含秒级回溯
    /// </summary>
    [Theory]
    [InlineData("0 2 * * *", "2024-06-12 01:30:00", "2024-06-11 02:00:00")]
    [InlineData("0 2 * * *", "2024-06-12 02:00:00", "2024-06-11 02:00:00")]
    [InlineData("*/5 * * * *", "2024-06-12 01:31:00", "2024-06-12 01:30:00")]
    [InlineData("30 * * * * *", "2024-06-12 01:00:45", "2024-06-12 01:00:30")]
    [InlineData("30 * * * * *", "2024-06-12 01:00:10", "2024-06-12 00:59:30")]
    public void GetPreviousOccurrence_WithFixedBaseTime_ReturnsExpectedMoment(string expression, string from, string expected)
    {
        var previous = CronHelper.GetPreviousOccurrence(expression, ParseMoment(from));

        Assert.NotNull(previous);
        Assert.Equal(ParseMoment(expected), previous!.Value);
    }

    /// <summary>
    /// 连续取多次触发时间时结果严格递增且按 Cron 周期排列
    /// </summary>
    [Fact]
    public void GetNextOccurrences_WithCount_ReturnsStrictlyIncreasingSequence()
    {
        var occurrences = CronHelper.GetNextOccurrences("0 2 * * *", 3, ParseMoment("2024-06-12 01:00:00"));

        Assert.Equal(3, occurrences.Count);
        Assert.Equal(ParseMoment("2024-06-12 02:00:00"), occurrences[0]);
        Assert.Equal(ParseMoment("2024-06-13 02:00:00"), occurrences[1]);
        Assert.Equal(ParseMoment("2024-06-14 02:00:00"), occurrences[2]);
    }

    /// <summary>
    /// 取零次时返回空列表而不是 null
    /// </summary>
    [Fact]
    public void GetNextOccurrences_WithZeroCount_ReturnsEmptyList()
    {
        var occurrences = CronHelper.GetNextOccurrences("0 2 * * *", 0, ParseMoment("2024-06-12 01:00:00"));

        Assert.Empty(occurrences);
    }

    /// <summary>
    /// 无解的表达式取多次时提前收敛为空列表
    /// </summary>
    [Fact(Timeout = TimeoutMilliseconds)]
    public void GetNextOccurrences_WhenNoOccurrenceExists_ReturnsEmptyList()
    {
        var occurrences = CronHelper.GetNextOccurrences("0 0 30 2 *", 3, ParseMoment("2024-06-12 00:00:00"));

        Assert.Empty(occurrences);
    }

    /// <summary>
    /// 时刻匹配判断覆盖命中与未命中
    /// </summary>
    [Theory]
    [InlineData("0 2 * * *", "2024-06-12 02:00:00", true)]
    [InlineData("0 2 * * *", "2024-06-12 02:01:00", false)]
    [InlineData("0 2 * * *", "2024-06-12 03:00:00", false)]
    [InlineData("* * * * *", "2024-06-12 03:07:00", true)]
    [InlineData("0 0 * * 3", "2024-06-12 00:00:00", true)]
    [InlineData("0 0 * * 4", "2024-06-12 00:00:00", false)]
    [InlineData("30 0 2 * * *", "2024-06-12 02:00:30", true)]
    [InlineData("30 0 2 * * *", "2024-06-12 02:00:31", false)]
    public void IsMatch_WithFixedMoment_ReturnsExpected(string expression, string moment, bool expected)
    {
        Assert.Equal(expected, CronHelper.IsMatch(expression, ParseMoment(moment)));
    }

    /// <summary>
    /// 格式化会归一化多余空白并把步长展开成显式值列表
    /// </summary>
    [Theory]
    [InlineData("0   2  *  *  *", "0 2 * * *")]
    [InlineData("*/30 * * * *", "0,30 * * * *")]
    [InlineData("? * * * *", "* * * * *")]
    [InlineData("0 0 12 * * *", "0 0 12 * * *")]
    public void FormatExpression_WithVariousInputs_ReturnsNormalizedForm(string expression, string expected)
    {
        Assert.Equal(expected, CronHelper.FormatExpression(expression));
    }

    /// <summary>
    /// 格式化非法表达式时向调用方抛出，不做静默兜底
    /// </summary>
    [Fact]
    public void FormatExpression_WhenExpressionIsInvalid_Throws()
    {
        Assert.Throws<ArgumentException>(() => CronHelper.FormatExpression("* * *"));
    }

    /// <summary>
    /// 全通配表达式的描述为"每分钟执行"
    /// </summary>
    [Fact]
    public void GetDescription_WithAllWildcards_DescribesEveryMinute()
    {
        Assert.Equal("每分钟执行", CronHelper.GetDescription("* * * * *"));
    }

    /// <summary>
    /// 具体时刻的描述里带出分钟与小时
    /// </summary>
    [Fact]
    public void GetDescription_WithConcreteFields_MentionsMinuteAndHour()
    {
        var description = CronHelper.GetDescription("0 2 * * *");

        Assert.Contains("0", description, StringComparison.Ordinal);
        Assert.Contains("2", description, StringComparison.Ordinal);
    }

    /// <summary>
    /// 非法表达式的描述降级为固定提示而不抛出
    /// </summary>
    [Fact]
    public void GetDescription_WhenExpressionIsInvalid_ReturnsFallbackText()
    {
        Assert.Equal("无效的 Cron 表达式", CronHelper.GetDescription("nonsense"));
    }

    /// <summary>
    /// 预定义表达式字典包含全部别名且取值稳定
    /// </summary>
    [Fact]
    public void GetPredefinedExpressions_ReturnsStableAliasMapping()
    {
        var predefined = CronHelper.GetPredefinedExpressions();

        Assert.Equal("0 0 1 1 *", predefined["@yearly"]);
        Assert.Equal("0 0 1 1 *", predefined["@annually"]);
        Assert.Equal("0 0 1 * *", predefined["@monthly"]);
        Assert.Equal("0 0 * * 0", predefined["@weekly"]);
        Assert.Equal("0 0 * * *", predefined["@daily"]);
        Assert.Equal("0 0 * * *", predefined["@midnight"]);
        Assert.Equal("0 * * * *", predefined["@hourly"]);
    }

    /// <summary>
    /// 预定义字典返回的是副本，调用方改动不会污染后续调用
    /// </summary>
    [Fact]
    public void GetPredefinedExpressions_ReturnsDefensiveCopy()
    {
        var first = CronHelper.GetPredefinedExpressions();
        first["@daily"] = "篡改";
        first.Remove("@hourly");

        var second = CronHelper.GetPredefinedExpressions();

        Assert.Equal("0 0 * * *", second["@daily"]);
        Assert.True(second.ContainsKey("@hourly"));
    }

    /// <summary>
    /// 5 位表达式拼装默认全通配，按位替换
    /// </summary>
    [Fact]
    public void CreateExpression_WithDefaults_ProducesAllWildcards()
    {
        Assert.Equal("* * * * *", CronHelper.CreateExpression());
        Assert.Equal("30 2 * * *", CronHelper.CreateExpression("30", "2"));
        Assert.Equal("30 2 1 6 3", CronHelper.CreateExpression("30", "2", "1", "6", "3"));
    }

    /// <summary>
    /// 6 位表达式拼装把秒放在首位
    /// </summary>
    [Fact]
    public void CreateExpressionWithSeconds_PutsSecondsFirst()
    {
        Assert.Equal("* * * * * *", CronHelper.CreateExpressionWithSeconds());
        Assert.Equal("5 30 2 * * *", CronHelper.CreateExpressionWithSeconds("5", "30", "2"));
    }

    /// <summary>
    /// 拼装出来的表达式必须能被自己解析回去（构造与解析闭环）
    /// </summary>
    [Fact]
    public void CreateExpression_Result_IsParsableByHelper()
    {
        Assert.True(CronHelper.IsValidExpression(CronHelper.CreateExpression("0", "3", "*", "*", "*")));
        Assert.True(CronHelper.IsValidExpression(CronHelper.CreateExpressionWithSeconds("0", "0", "3")));
    }

    /// <summary>
    /// 解析固定格式的基准时间，避免受运行机器区域设置影响
    /// </summary>
    private static DateTime ParseMoment(string value)
    {
        return DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
