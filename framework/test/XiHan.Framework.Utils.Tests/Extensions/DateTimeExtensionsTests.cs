// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Tests.Extensions;

/// <summary>
/// DateTime 扩展方法测试
/// </summary>
/// <remarks>
/// 涉及"当前时间"的方法只断言相对关系与量级，不锁死绝对值，避免跨时区/跨天跑测时抖动。
/// </remarks>
public class DateTimeExtensionsTests
{
    /// <summary>
    /// 时间戳按 Unix 纪元的秒数计算
    /// </summary>
    [Fact]
    public void GetDateToTimeStamp_UsesUnixEpochInSeconds()
    {
        var moment = new DateTime(2000, 1, 1, 0, 0, 0);

        Assert.Equal(946684800L, moment.GetDateToTimeStamp());
        Assert.Equal(0L, new DateTime(1970, 1, 1).GetDateToTimeStamp());
    }

    /// <summary>
    /// 当天最小与最大时间只保留日期部分的首尾时刻
    /// </summary>
    [Fact]
    public void GetDayMinDateAndMaxDate_ClipToDayBoundaries()
    {
        var moment = new DateTime(2024, 3, 15, 13, 24, 35);

        Assert.Equal(new DateTime(2024, 3, 15, 0, 0, 0), moment.GetDayMinDate());
        Assert.Equal(new DateTime(2024, 3, 15, 23, 59, 59), moment.GetDayMaxDate());
    }

    /// <summary>
    /// 一天的范围由最小与最大时刻组成
    /// </summary>
    [Fact]
    public void GetDayDateRange_ReturnsMinThenMax()
    {
        var moment = new DateTime(2024, 3, 15, 13, 24, 35);

        var range = moment.GetDayDateRange();

        Assert.Equal(2, range.Length);
        Assert.Equal(moment.GetDayMinDate(), range[0]);
        Assert.Equal(moment.GetDayMaxDate(), range[1]);
    }

    /// <summary>
    /// 有值时原样返回
    /// </summary>
    [Fact]
    public void GetBeginTime_WhenHasValue_ReturnsIt()
    {
        DateTime? moment = new DateTime(2024, 3, 15);

        Assert.Equal(new DateTime(2024, 3, 15), moment.GetBeginTime());
    }

    /// <summary>
    /// 为 null 或最小值时按当前时间加偏移天数
    /// </summary>
    [Fact]
    public void GetBeginTime_WhenNullOrMinValue_UsesNowWithOffset()
    {
        DateTime? nothing = null;
        DateTime? minValue = DateTime.MinValue;

        Assert.Equal(DateTime.Now.Date, nothing.GetBeginTime().Date);
        Assert.Equal(DateTime.Now.AddDays(2).Date, minValue.GetBeginTime(2).Date);
    }

    /// <summary>
    /// 星期名称按中文星期日到星期六的顺序取值
    /// </summary>
    [Fact]
    public void GetWeekByDate_ReturnsChineseWeekdayName()
    {
        Assert.Equal("星期一", new DateTime(2024, 1, 1).GetWeekByDate());
        Assert.Equal("星期二", new DateTime(2024, 1, 2).GetWeekByDate());
        Assert.Equal("星期日", new DateTime(2024, 1, 7).GetWeekByDate());
    }

    /// <summary>
    /// 月内第几周从 1 开始，按自然周切分
    /// </summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(15, 3)]
    public void GetWeekNumInMonth_CountsNaturalWeeks(int day, int expected)
    {
        var moment = new DateTime(2024, 1, day);

        Assert.Equal(expected, moment.GetWeekNumInMonth());
    }

    /// <summary>
    /// 同年只显示月日，跨年显示完整日期
    /// </summary>
    [Fact]
    public void FormatDateTimeToString_ShortensWithinCurrentYear()
    {
        var thisYear = new DateTime(DateTime.Now.Year, 3, 15, 8, 9, 0);
        var otherYear = new DateTime(DateTime.Now.Year - 5, 3, 15, 8, 9, 0);

        // 只断言日期前缀：格式串里的时间分隔符会跟随当前区域，锁死整串会在非中英区域抖动
        Assert.StartsWith("03-15 ", thisYear.FormatDateTimeToString());
        Assert.StartsWith($"{DateTime.Now.Year - 5:D4}-03-15 ", otherYear.FormatDateTimeToString());
    }

    /// <summary>
    /// 两个时间之间的跨度按固定格式输出
    /// </summary>
    [Fact]
    public void FormatDateTimeToString_WithRange_FormatsTimeSpan()
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0);
        var end = start.AddHours(1).AddMinutes(2).AddSeconds(3);

        Assert.Equal("00 天 01 小时 02 分 03 秒 000 毫秒", start.FormatDateTimeToString(end));
    }

    /// <summary>
    /// 开始时间不早于结束时间时抛异常
    /// </summary>
    [Fact]
    public void FormatDateTimeToString_WhenStartNotBeforeEnd_Throws()
    {
        var moment = new DateTime(2024, 1, 1);

        Assert.Throws<Exception>(() => moment.FormatDateTimeToString(moment));
    }

    /// <summary>
    /// 毫秒与刻度都能格式化为同一套跨度文本
    /// </summary>
    [Fact]
    public void FormatMilliSecondsAndTicks_ProduceSameSpanText()
    {
        const long OneHourMilliseconds = 60 * 60 * 1000;
        var ticks = TimeSpan.FromMilliseconds(OneHourMilliseconds).Ticks;

        Assert.Equal("00 天 01 小时 00 分 00 秒 000 毫秒", OneHourMilliseconds.FormatMilliSecondsToString());
        Assert.Equal("00 天 01 小时 00 分 00 秒 000 毫秒", ticks.FormatTimeTicksToString());
    }

    /// <summary>
    /// 毫秒直接拆分为天时分秒毫秒，毫秒段固定三位
    /// </summary>
    [Fact]
    public void FormatTimeMilliSecondToString_PadsEachSegment()
    {
        const long Value = (2 * 24 * 60 * 60 * 1000L) + (3 * 60 * 60 * 1000L) + (4 * 60 * 1000L) + (5 * 1000L) + 60;

        Assert.Equal("02 天 03 小时 04 分 05 秒 060 毫秒", Value.FormatTimeMilliSecondToString());
    }

    /// <summary>
    /// 时间跨度格式化对各段补零，毫秒补到三位
    /// </summary>
    [Theory]
    [InlineData(0, "00 天 00 小时 00 分 00 秒 000 毫秒")]
    [InlineData(50, "00 天 00 小时 00 分 00 秒 050 毫秒")]
    [InlineData(500, "00 天 00 小时 00 分 00 秒 500 毫秒")]
    public void FormatTimeSpanToString_PadsMilliseconds(int milliseconds, string expected)
    {
        var span = TimeSpan.FromMilliseconds(milliseconds);

        Assert.Equal(expected, span.FormatTimeSpanToString());
    }

    /// <summary>
    /// 相对时间描述按秒、分、小时、天、周、月递进
    /// </summary>
    [Fact]
    public void FormatDateTimeToEasyString_DescribesRelativeDistance()
    {
        var now = DateTime.Now;

        Assert.Equal("刚刚", now.AddSeconds(-1).FormatDateTimeToEasyString());
        Assert.Equal("30秒前", now.AddSeconds(-30).FormatDateTimeToEasyString());
        Assert.Equal("5分钟前", now.AddMinutes(-5).FormatDateTimeToEasyString());
        Assert.Equal("3小时前", now.AddHours(-3).FormatDateTimeToEasyString());
        Assert.Equal("3天前", now.AddDays(-3).FormatDateTimeToEasyString());
        Assert.Equal("1周前", now.AddDays(-8).FormatDateTimeToEasyString());
        Assert.Equal("2个月前", now.AddDays(-70).FormatDateTimeToEasyString());
    }

    /// <summary>
    /// 未来时间直接给出完整日期文本
    /// </summary>
    [Fact]
    public void FormatDateTimeToEasyString_WhenInFuture_ReturnsFullText()
    {
        var future = DateTime.Now.AddDays(1);

        Assert.Equal(future.ToString("yyyy-MM-dd HH:mm:ss"), future.FormatDateTimeToEasyString());
    }

    /// <summary>
    /// 带分隔符的日期串走通用解析
    /// </summary>
    [Fact]
    public void FormatStringToDate_ParsesSeparatedText()
    {
        Assert.Equal(new DateTime(2024, 1, 2), "2024-01-02".FormatStringToDate());
        Assert.Equal(new DateTime(2024, 1, 2), "2024/01/02".FormatStringToDate());
    }

    /// <summary>
    /// 无分隔符时按长度选择固定格式
    /// </summary>
    [Fact]
    public void FormatStringToDate_ParsesCompactTextByLength()
    {
        Assert.Equal(new DateTime(2024, 1, 1), "2024".FormatStringToDate());
        Assert.Equal(new DateTime(2024, 3, 1), "202403".FormatStringToDate());
        Assert.Equal(new DateTime(2024, 3, 15), "20240315".FormatStringToDate());
        Assert.Equal(new DateTime(2024, 3, 15, 8, 0, 0), "2024031508".FormatStringToDate());
        Assert.Equal(new DateTime(2024, 3, 15, 8, 9, 0), "202403150809".FormatStringToDate());
        Assert.Equal(new DateTime(2024, 3, 15, 8, 9, 10), "20240315080910".FormatStringToDate());
    }

    /// <summary>
    /// 空白或非法文本回落到最小日期
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void FormatStringToDate_WhenInvalid_ReturnsMinValue(string value)
    {
        Assert.Equal(DateTime.MinValue, value.FormatStringToDate());
    }
}
