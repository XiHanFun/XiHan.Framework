// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Timing;

namespace XiHan.Framework.Utils.Tests.Timing;

/// <summary>
/// 农历辅助工具类测试
/// </summary>
/// <remarks>
/// 本文件锁的是一整轮"解码层与数据表对不上"的修复：
/// <list type="bullet">
/// <item>闰月与月大小的取位方式与 LunarYearData 的实际编码错位，闰月几乎恒判为无，公农历互转累计偏差约 1900 天</item>
/// <item>干支纪年以 1900 年为甲子年推算，而 1900 年是庚子年，天干恒偏 6 位</item>
/// <item>干支纪日的锚点与偏移都错，1900 年 1 月 1 日实为甲戌日却被算成庚午</item>
/// <item>节气估算表按"小寒打头"排列、名称表按"立春打头"排列，整体错位 2 位，日期全错约 15 天；
/// 且节气时刻未换算到北京时间，凌晨 8 点前的节气会落到前一天</item>
/// <item>按"立春年"归集节气，导致一月份的小寒大寒无法被 GetSolarTerm 查到</item>
/// </list>
/// 断言尽量用可独立核对的外部事实（权威春节日期、二十四节气日期、儒略日干支公式），
/// 而不是把当前实现的输出抄成期望值。
/// </remarks>
public class LunarCalendarHelperTests
{
    /// <summary>
    /// 农历数据表的起始年份
    /// </summary>
    private const int MinYear = 1900;

    /// <summary>
    /// 农历数据表的结束年份
    /// </summary>
    private const int MaxYear = 2100;

    private static readonly string[] Tiangan = ["甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸"];

    private static readonly string[] Dizhi = ["子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥"];

    #region 公农历互转

    /// <summary>
    /// 农历正月初一换算出的公历日期与权威春节日期一致
    /// </summary>
    [Theory]
    [InlineData(1900, "1900-01-31")]
    [InlineData(1949, "1949-01-29")]
    [InlineData(2000, "2000-02-05")]
    [InlineData(2019, "2019-02-05")]
    [InlineData(2020, "2020-01-25")]
    [InlineData(2021, "2021-02-12")]
    [InlineData(2022, "2022-02-01")]
    [InlineData(2023, "2023-01-22")]
    [InlineData(2024, "2024-02-10")]
    [InlineData(2025, "2025-01-29")]
    [InlineData(2026, "2026-02-17")]
    public void ConvertToSolar_ForSpringFestival_MatchesKnownDates(int lunarYear, string expected)
    {
        Assert.Equal(DateTime.Parse(expected), LunarCalendarHelper.ConvertToSolar(lunarYear, 1, 1));
    }

    /// <summary>
    /// 权威春节日期换算回农历是当年的正月初一
    /// </summary>
    [Theory]
    [InlineData("1900-01-31", 1900)]
    [InlineData("2020-01-25", 2020)]
    [InlineData("2024-02-10", 2024)]
    [InlineData("2025-01-29", 2025)]
    public void ConvertToLunar_ForSpringFestival_ReturnsFirstDayOfFirstMonth(string solar, int expectedLunarYear)
    {
        var lunar = LunarCalendarHelper.ConvertToLunar(DateTime.Parse(solar));

        Assert.Equal(expectedLunarYear, lunar.Year);
        Assert.Equal(1, lunar.Month);
        Assert.Equal(1, lunar.Day);
        Assert.False(lunar.IsLeapMonth);
        Assert.Equal("春节", lunar.Festival);
    }

    /// <summary>
    /// 闰月日期能正确互转，且与同名的普通月区分开
    /// </summary>
    [Theory]
    [InlineData(2020, 4, "2020-04-23", "2020-05-23")]
    [InlineData(2023, 2, "2023-02-20", "2023-03-22")]
    [InlineData(2025, 6, "2025-06-25", "2025-07-25")]
    public void ConvertToSolar_DistinguishesLeapMonthFromNormalMonth(int lunarYear, int lunarMonth, string normalFirstDay, string leapFirstDay)
    {
        Assert.Equal(DateTime.Parse(normalFirstDay), LunarCalendarHelper.ConvertToSolar(lunarYear, lunarMonth, 1));
        Assert.Equal(DateTime.Parse(leapFirstDay), LunarCalendarHelper.ConvertToSolar(lunarYear, lunarMonth, 1, true));

        var lunar = LunarCalendarHelper.ConvertToLunar(DateTime.Parse(leapFirstDay));
        Assert.True(lunar.IsLeapMonth);
        Assert.Equal(lunarMonth, lunar.Month);
        Assert.Equal(1, lunar.Day);
    }

    /// <summary>
    /// 支持区间内逐日往返转换是恒等映射
    /// </summary>
    /// <remarks>
    /// 这条覆盖全部 73384 天，是整个解码层最强的约束：
    /// 位布局只要错一位，闰月或月大小就会偏，某一天必然换算不回去。
    /// </remarks>
    [Fact]
    public void ConvertToLunar_ThenConvertToSolar_RoundTripsForEveryDayInRange()
    {
        var failures = new List<string>();

        for (var date = new DateTime(MinYear, 1, 31); date <= new DateTime(MaxYear, 12, 31); date = date.AddDays(1))
        {
            var lunar = LunarCalendarHelper.ConvertToLunar(date);
            var roundTrip = LunarCalendarHelper.ConvertToSolar(lunar.Year, lunar.Month, lunar.Day, lunar.IsLeapMonth);

            if (roundTrip != date)
            {
                failures.Add($"{date:yyyy-MM-dd} -> {lunar.Year}/{(lunar.IsLeapMonth ? "闰" : "")}{lunar.Month}/{lunar.Day} -> {roundTrip:yyyy-MM-dd}");
                if (failures.Count >= 5)
                {
                    break;
                }
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// 农历日号逐日连续，换月时归一
    /// </summary>
    [Fact]
    public void ConvertToLunar_ProducesContinuousDayNumbers()
    {
        var previous = LunarCalendarHelper.ConvertToLunar(new DateTime(MinYear, 1, 31));
        var failures = new List<string>();

        for (var date = new DateTime(MinYear, 2, 1); date <= new DateTime(MaxYear, 12, 31); date = date.AddDays(1))
        {
            var current = LunarCalendarHelper.ConvertToLunar(date);
            var continuous = current.Day == previous.Day + 1 || (current.Day == 1 && previous.Day is 29 or 30);

            if (!continuous)
            {
                failures.Add($"{date:yyyy-MM-dd} 日号由 {previous.Day} 跳到 {current.Day}");
                if (failures.Count >= 5)
                {
                    break;
                }
            }

            previous = current;
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// 每个农历年的春节都落在公历 1 月 21 日至 2 月 21 日之间
    /// </summary>
    /// <remarks>农历新年只能落在这个天文区间内，闰月少算一个月会让春节整体前移出界。</remarks>
    [Fact]
    public void ConvertToSolar_KeepsEverySpringFestivalInsideAstronomicalWindow()
    {
        for (var year = MinYear; year <= MaxYear; year++)
        {
            var springFestival = LunarCalendarHelper.ConvertToSolar(year, 1, 1);

            Assert.Equal(year, springFestival.Year);
            Assert.InRange(springFestival.DayOfYear, 21, 52);
        }
    }

    #endregion 公农历互转

    #region 闰月与年长

    /// <summary>
    /// 闰月年份能被识别，闰月月份与史实一致
    /// </summary>
    [Theory]
    [InlineData(1900, 8)]
    [InlineData(2020, 4)]
    [InlineData(2023, 2)]
    [InlineData(2025, 6)]
    public void HasLeapMonth_ForKnownLeapYears_ReturnsTrueWithExpectedMonth(int year, int expectedLeapMonth)
    {
        Assert.True(LunarCalendarHelper.HasLeapMonth(year));

        // 闰月月份没有公开 API，用"只有该月存在闰月"反证
        for (var month = 1; month <= 12; month++)
        {
            if (month == expectedLeapMonth)
            {
                Assert.Equal(1, LunarCalendarHelper.ConvertToLunar(LunarCalendarHelper.ConvertToSolar(year, month, 1, true)).Day);
            }
            else
            {
                Assert.Throws<ArgumentException>(() => LunarCalendarHelper.ConvertToSolar(year, month, 1, true));
            }
        }
    }

    /// <summary>
    /// 无闰月的年份不会被误判
    /// </summary>
    [Theory]
    [InlineData(2021)]
    [InlineData(2022)]
    [InlineData(2024)]
    public void HasLeapMonth_ForKnownPlainYears_ReturnsFalse(int year)
    {
        Assert.False(LunarCalendarHelper.HasLeapMonth(year));
    }

    /// <summary>
    /// 农历年长度落在平年 353-355 天、闰年 383-385 天的区间内
    /// </summary>
    /// <remarks>闰月被漏算时年长会恒定在 354 天上下，这条能直接把它照出来。</remarks>
    [Fact]
    public void GetLunarYearDays_StaysWithinCalendricalBounds()
    {
        for (var year = MinYear; year <= MaxYear; year++)
        {
            var days = LunarCalendarHelper.GetLunarYearDays(year);

            if (LunarCalendarHelper.HasLeapMonth(year))
            {
                Assert.InRange(days, 383, 385);
            }
            else
            {
                Assert.InRange(days, 353, 355);
            }
        }
    }

    /// <summary>
    /// 闰月按 19 年 7 闰的节律分布，相邻闰年只隔 2 年或 3 年
    /// </summary>
    [Fact]
    public void HasLeapMonth_FollowsMetonicCycle()
    {
        var leapYears = Enumerable.Range(MinYear, MaxYear - MinYear + 1)
            .Where(LunarCalendarHelper.HasLeapMonth)
            .ToArray();

        var expected = (MaxYear - MinYear + 1) * 7 / 19.0;
        Assert.InRange(leapYears.Length, (int)(expected - 2), (int)(expected + 2));
        Assert.All(leapYears.Zip(leapYears.Skip(1), (first, second) => second - first), gap => Assert.InRange(gap, 2, 3));
    }

    #endregion 闰月与年长

    #region 干支与生肖

    /// <summary>
    /// 干支纪年与史实一致
    /// </summary>
    [Theory]
    [InlineData(1900, "庚子", "鼠")]
    [InlineData(1911, "辛亥", "猪")]
    [InlineData(1949, "己丑", "牛")]
    [InlineData(1984, "甲子", "鼠")]
    [InlineData(2023, "癸卯", "兔")]
    [InlineData(2024, "甲辰", "龙")]
    [InlineData(2025, "乙巳", "蛇")]
    public void GetTianganDizhi_MatchesHistoricalYearNames(int year, string expectedGanzhi, string expectedZodiac)
    {
        Assert.Equal(expectedGanzhi, LunarCalendarHelper.GetTianganDizhi(year));
        Assert.Equal(expectedZodiac, LunarCalendarHelper.GetZodiac(year));
    }

    /// <summary>
    /// 1900 年以前的年份也能推算干支，不会因为取模为负而越界
    /// </summary>
    [Theory]
    [InlineData(4, "甲子")]
    [InlineData(1644, "甲申")]
    [InlineData(1840, "庚子")]
    public void GetTianganDizhi_SupportsYearsBeforeDataTableRange(int year, string expected)
    {
        Assert.Equal(expected, LunarCalendarHelper.GetTianganDizhi(year));
    }

    /// <summary>
    /// 干支纪日与儒略日通用公式一致
    /// </summary>
    /// <remarks>
    /// 参照公式：天干下标 = (JDN + 9) % 10、地支下标 = (JDN + 1) % 12，
    /// 与被测实现各自独立推导，能把锚点日和偏移量两类错误都查出来。
    /// </remarks>
    [Fact]
    public void GetDayTianganDizhi_MatchesJulianDayFormula()
    {
        var failures = new List<string>();

        for (var date = new DateTime(MinYear, 1, 1); date <= new DateTime(MaxYear, 12, 31); date = date.AddDays(29))
        {
            var julianDayNumber = ToJulianDayNumber(date);
            var expected = Tiangan[(julianDayNumber + 9) % 10] + Dizhi[(julianDayNumber + 1) % 12];
            var actual = LunarCalendarHelper.GetDayTianganDizhi(date);

            if (actual != expected)
            {
                failures.Add($"{date:yyyy-MM-dd} 实得 {actual} 期望 {expected}");
                if (failures.Count >= 5)
                {
                    break;
                }
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// 1900 年 1 月 1 日是甲戌日
    /// </summary>
    [Fact]
    public void GetDayTianganDizhi_ForEpochDay_IsJiaXu()
    {
        Assert.Equal("甲戌", LunarCalendarHelper.GetDayTianganDizhi(new DateTime(1900, 1, 1)));
    }

    /// <summary>
    /// 干支纪日逐日推进一位，六十天一循环
    /// </summary>
    [Fact]
    public void GetDayTianganDizhi_CyclesEverySixtyDays()
    {
        var start = new DateTime(2024, 1, 1);

        Assert.Equal(LunarCalendarHelper.GetDayTianganDizhi(start), LunarCalendarHelper.GetDayTianganDizhi(start.AddDays(60)));
        Assert.NotEqual(LunarCalendarHelper.GetDayTianganDizhi(start), LunarCalendarHelper.GetDayTianganDizhi(start.AddDays(59)));

        var distinct = Enumerable.Range(0, 60)
            .Select(offset => LunarCalendarHelper.GetDayTianganDizhi(start.AddDays(offset)))
            .Distinct()
            .Count();
        Assert.Equal(60, distinct);
    }

    #endregion 干支与生肖

    #region 节气

    /// <summary>
    /// 二十四节气的名称与日期配对与权威数据一致（北京时间）
    /// </summary>
    /// <remarks>
    /// 必须连名字一起比：原实现的缺陷是名称表与黄经表整体错位 2 位，
    /// 算出的 24 个日期作为集合是对的，错的是名字和日期的对应关系，
    /// 只比日期序列的断言会放过这种错位。
    /// </remarks>
    [Theory]
    [InlineData(2024, "小寒 01-06,大寒 01-20,立春 02-04,雨水 02-19,惊蛰 03-05,春分 03-20,清明 04-04,谷雨 04-19,立夏 05-05,小满 05-20,芒种 06-05,夏至 06-21,小暑 07-06,大暑 07-22,立秋 08-07,处暑 08-22,白露 09-07,秋分 09-22,寒露 10-08,霜降 10-23,立冬 11-07,小雪 11-22,大雪 12-06,冬至 12-21")]
    [InlineData(2025, "小寒 01-05,大寒 01-20,立春 02-03,雨水 02-18,惊蛰 03-05,春分 03-20,清明 04-04,谷雨 04-20,立夏 05-05,小满 05-21,芒种 06-05,夏至 06-21,小暑 07-07,大暑 07-22,立秋 08-07,处暑 08-23,白露 09-07,秋分 09-23,寒露 10-08,霜降 10-23,立冬 11-07,小雪 11-22,大雪 12-07,冬至 12-21")]
    public void GetSolarTerms_MatchesAuthoritativeNamesAndDates(int year, string expectedTerms)
    {
        var actual = LunarCalendarHelper.GetSolarTerms(year);

        Assert.Equal(expectedTerms.Split(','), actual.Select(term => $"{term.Name} {term.Date:MM-dd}"));
    }

    /// <summary>
    /// 节气按公历年归集，列表以小寒开头、冬至结尾，序号按日期排列
    /// </summary>
    /// <remarks>
    /// 原实现按"立春年"归集，一月份的小寒大寒被算到上一年的列表里，
    /// <see cref="LunarCalendarHelper.GetSolarTerm"/> 按公历年查找时永远查不到它们。
    /// </remarks>
    [Fact]
    public void GetSolarTerms_GroupsByCalendarYearAndOrdersByDate()
    {
        var terms = LunarCalendarHelper.GetSolarTerms(2024);

        Assert.Equal(24, terms.Count);
        Assert.Equal("小寒", terms[0].Name);
        Assert.Equal("冬至", terms[^1].Name);
        Assert.All(terms, term => Assert.Equal(2024, term.Date.Year));
        Assert.Equal(Enumerable.Range(1, 24), terms.Select(term => term.Order));
        Assert.Equal(terms.Select(term => term.Date).OrderBy(date => date), terms.Select(term => term.Date));
    }

    /// <summary>
    /// 一月份的小寒大寒能被查到
    /// </summary>
    [Theory]
    [InlineData("2024-01-06", "小寒")]
    [InlineData("2024-01-20", "大寒")]
    [InlineData("2024-02-04", "立春")]
    [InlineData("2024-12-21", "冬至")]
    public void GetSolarTerm_FindsTermsInEveryPartOfTheYear(string date, string expectedName)
    {
        var parsed = DateTime.Parse(date);

        Assert.True(LunarCalendarHelper.IsSolarTerm(parsed));
        Assert.Equal(expectedName, LunarCalendarHelper.GetSolarTerm(parsed)?.Name);
    }

    /// <summary>
    /// 凌晨发生的节气按北京时间归日，不会掉到前一天
    /// </summary>
    /// <remarks>
    /// 2024 年霜降是北京时间 10 月 23 日 06:15，换算成 UTC 落在 22 日晚间；
    /// 原实现不做时区换算，这一天会被算成 10 月 22 日。
    /// </remarks>
    [Fact]
    public void GetSolarTerms_UsesBeijingTimeForDateBoundary()
    {
        var frostsDescent = LunarCalendarHelper.GetSolarTerms(2024).Single(term => term.Name == "霜降");

        Assert.Equal(new DateTime(2024, 10, 23), frostsDescent.Date.Date);
    }

    /// <summary>
    /// 整个支持区间内每年都能算出 24 个节气，且间隔均匀
    /// </summary>
    [Fact]
    public void GetSolarTerms_ProducesTwentyFourEvenlySpacedTermsForEveryYear()
    {
        for (var year = MinYear; year <= MaxYear; year++)
        {
            var terms = LunarCalendarHelper.GetSolarTerms(year);

            Assert.Equal(24, terms.Count);
            Assert.All(terms, term => Assert.Equal(year, term.Date.Year));
            Assert.All(
                terms.Zip(terms.Skip(1), (first, second) => (second.Date - first.Date).TotalDays),
                gap => Assert.InRange(gap, 13, 17));
        }
    }

    /// <summary>
    /// 非节气日返回 null
    /// </summary>
    [Fact]
    public void GetSolarTerm_WhenDateIsNotTerm_ReturnsNull()
    {
        Assert.Null(LunarCalendarHelper.GetSolarTerm(new DateTime(2024, 3, 1)));
        Assert.False(LunarCalendarHelper.IsSolarTerm(new DateTime(2024, 3, 1)));
    }

    #endregion 节气

    #region 节日与名称

    /// <summary>
    /// 腊月只有 29 天时廿九才是除夕，且取决于被查询的农历年
    /// </summary>
    /// <remarks>
    /// 农历 2024 年腊月只有 29 天，所以 2025 年"没有大年三十"，除夕落在廿九；
    /// 农历 2023 年腊月有 30 天，廿九不是除夕。
    /// 原实现拿 <c>DateTime.Now.Year</c> 当农历年去查腊月天数，答案随运行时间漂移。
    /// </remarks>
    [Fact]
    public void GetLunarFestival_DecidesNewYearsEveByRequestedLunarYear()
    {
        Assert.Equal("除夕", LunarCalendarHelper.GetLunarFestival(12, 29, false, 2024));
        Assert.Null(LunarCalendarHelper.GetLunarFestival(12, 29, false, 2023));

        // 不传年份就无从判断，不猜
        Assert.Null(LunarCalendarHelper.GetLunarFestival(12, 29));

        // 腊月三十只要存在就一定是除夕，与年份无关
        Assert.Equal("除夕", LunarCalendarHelper.GetLunarFestival(12, 30));
    }

    /// <summary>
    /// 农历日期对象会带上自己的年份去判断节日
    /// </summary>
    [Theory]
    [InlineData("2024-02-09", "除夕")]
    [InlineData("2025-01-28", "除夕")]
    [InlineData("2024-02-10", "春节")]
    [InlineData("2024-02-24", "元宵节")]
    [InlineData("2024-03-01", null)]
    public void LunarDate_Festival_UsesOwnYear(string solar, string? expected)
    {
        Assert.Equal(expected, LunarCalendarHelper.ConvertToLunar(DateTime.Parse(solar)).Festival);
    }

    /// <summary>
    /// 闰月不过传统节日
    /// </summary>
    [Fact]
    public void GetLunarFestival_ForLeapMonth_ReturnsNull()
    {
        Assert.Null(LunarCalendarHelper.GetLunarFestival(1, 1, true));
    }

    /// <summary>
    /// 农历日期的中文表示完整可读
    /// </summary>
    [Theory]
    [InlineData("2024-02-10", "二零二四年正月初一")]
    [InlineData("2023-03-22", "二零二三年闰二月初一")]
    [InlineData("2024-02-09", "二零二三年腊月三十")]
    public void LunarDate_FullName_RendersChineseText(string solar, string expected)
    {
        Assert.Equal(expected, LunarCalendarHelper.ConvertToLunar(DateTime.Parse(solar)).FullName);
    }

    #endregion 节日与名称

    #region 参数校验

    /// <summary>
    /// 超出数据表范围的公历日期被拒绝
    /// </summary>
    [Fact]
    public void ConvertToLunar_WhenDateOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LunarCalendarHelper.ConvertToLunar(new DateTime(1899, 12, 31)));
        Assert.Throws<ArgumentOutOfRangeException>(() => LunarCalendarHelper.ConvertToLunar(new DateTime(2101, 1, 1)));
    }

    /// <summary>
    /// 非法的农历年月日被拒绝，而不是拿去读错位的编码
    /// </summary>
    [Fact]
    public void ConvertToSolar_WhenArgumentsInvalid_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LunarCalendarHelper.ConvertToSolar(1899, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LunarCalendarHelper.ConvertToSolar(2024, 13, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LunarCalendarHelper.ConvertToSolar(2024, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LunarCalendarHelper.ConvertToSolar(2024, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LunarCalendarHelper.ConvertToSolar(2024, 1, 31));
    }

    /// <summary>
    /// 指定年份没有该闰月时被拒绝
    /// </summary>
    [Fact]
    public void ConvertToSolar_WhenLeapMonthDoesNotExist_Throws()
    {
        Assert.Throws<ArgumentException>(() => LunarCalendarHelper.ConvertToSolar(2024, 2, 1, true));
        Assert.Throws<ArgumentException>(() => LunarCalendarHelper.ConvertToSolar(2023, 3, 1, true));
    }

    /// <summary>
    /// 非法月份不会被拿去索引月份名数组
    /// </summary>
    [Fact]
    public void GetLunarMonthName_WhenMonthOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LunarCalendarHelper.GetLunarMonthName(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LunarCalendarHelper.GetLunarMonthName(13));
        Assert.Equal("闰二月", LunarCalendarHelper.GetLunarMonthName(2, true));
    }

    #endregion 参数校验

    /// <summary>
    /// 计算公历日期的儒略日数
    /// </summary>
    private static int ToJulianDayNumber(DateTime date)
    {
        var a = (14 - date.Month) / 12;
        var year = date.Year + 4800 - a;
        var month = date.Month + (12 * a) - 3;

        return date.Day + (((153 * month) + 2) / 5) + (365 * year) + (year / 4) - (year / 100) + (year / 400) - 32045;
    }
}
