#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LunarCalendarHelper
// Guid:76a916af-c7b4-4cad-8966-6fcb0f631cf3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 02:59:12
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Timing;

/// <summary>
/// 农历辅助工具类
/// </summary>
/// <remarks>
/// 提供公历与农历互转、天干地支、生肖、节气、农历节日等功能，
/// 支持1900年至2100年的农历计算
/// </remarks>
public static class LunarCalendarHelper
{
    #region 常量定义

    /// <summary>
    /// 农历数据起始年份
    /// </summary>
    private const int MinYear = 1900;

    /// <summary>
    /// 农历数据结束年份
    /// </summary>
    private const int MaxYear = 2100;

    /// <summary>
    /// 农历基准日期 (1900年1月31日为农历1900年正月初一)
    /// </summary>
    private static readonly DateTime BaseDate = new(1900, 1, 31);

    /// <summary>
    /// 天干数组
    /// </summary>
    private static readonly string[] Tiangan = ["甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸"];

    /// <summary>
    /// 地支数组
    /// </summary>
    private static readonly string[] Dizhi = ["子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥"];

    /// <summary>
    /// 生肖数组
    /// </summary>
    private static readonly string[] Zodiac = ["鼠", "牛", "虎", "兔", "龙", "蛇", "马", "羊", "猴", "鸡", "狗", "猪"];

    /// <summary>
    /// 农历月份名称
    /// </summary>
    private static readonly string[] LunarMonths = ["正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "冬月", "腊月"];

    /// <summary>
    /// 农历日期名称
    /// </summary>
    private static readonly string[] LunarDays =
    [
        "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
        "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
        "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十"
    ];

    /// <summary>
    /// 二十四节气名称
    /// </summary>
    private static readonly string[] SolarTerms =
    [
        "立春", "雨水", "惊蛰", "春分", "清明", "谷雨", "立夏", "小满", "芒种", "夏至", "小暑", "大暑",
        "立秋", "处暑", "白露", "秋分", "寒露", "霜降", "立冬", "小雪", "大雪", "冬至", "小寒", "大寒"
    ];

    #endregion

    #region 农历数据表

    /// <summary>
    /// 农历年份数据 (1900-2100年)
    /// 每个数值的低12位表示12个月的大小月(1为大月30天，0为小月29天)
    /// 第13位表示闰月的大小月
    /// 第14-17位表示闰月月份(0表示无闰月)
    /// </summary>
    private static readonly int[] LunarYearData =
    [
        0x04bd8, 0x04ae0, 0x0a570, 0x054d5, 0x0d260, 0x0d950, 0x16554, 0x056a0, 0x09ad0, 0x055d2,
        0x04ae0, 0x0a5b6, 0x0a4d0, 0x0d250, 0x1d255, 0x0b540, 0x0d6a0, 0x0ada2, 0x095b0, 0x14977,
        0x04970, 0x0a4b0, 0x0b4b5, 0x06a50, 0x06d40, 0x1ab54, 0x02b60, 0x09570, 0x052f2, 0x04970,
        0x06566, 0x0d4a0, 0x0ea50, 0x06e95, 0x05ad0, 0x02b60, 0x186e3, 0x092e0, 0x1c8d7, 0x0c950,
        0x0d4a0, 0x1d8a6, 0x0b550, 0x056a0, 0x1a5b4, 0x025d0, 0x092d0, 0x0d2b2, 0x0a950, 0x0b557,
        0x06ca0, 0x0b550, 0x15355, 0x04da0, 0x0a5d0, 0x14573, 0x052d0, 0x0a9a8, 0x0e950, 0x06aa0,
        0x0aea6, 0x0ab50, 0x04b60, 0x0aae4, 0x0a570, 0x05260, 0x0f263, 0x0d950, 0x05b57, 0x056a0,
        0x096d0, 0x04dd5, 0x04ad0, 0x0a4d0, 0x0d4d4, 0x0d250, 0x0d558, 0x0b540, 0x0b5a0, 0x195a6,
        0x095b0, 0x049b0, 0x0a974, 0x0a4b0, 0x0b27a, 0x06a50, 0x06d40, 0x0af46, 0x0ab60, 0x09570,
        0x04af5, 0x04970, 0x064b0, 0x074a3, 0x0ea50, 0x06b58, 0x055c0, 0x0ab60, 0x096d5, 0x092e0,
        0x0c960, 0x0d954, 0x0d4a0, 0x0da50, 0x07552, 0x056a0, 0x0abb7, 0x025d0, 0x092d0, 0x0cab5,
        0x0a950, 0x0b4a0, 0x0baa4, 0x0ad50, 0x055d9, 0x04ba0, 0x0a5b0, 0x15176, 0x052b0, 0x0a930,
        0x07954, 0x06aa0, 0x0ad50, 0x05b52, 0x04b60, 0x0a6e6, 0x0a4e0, 0x0d260, 0x0ea65, 0x0d530,
        0x05aa0, 0x076a3, 0x096d0, 0x04bd7, 0x04ad0, 0x0a4d0, 0x1d0b6, 0x0d250, 0x0d520, 0x0dd45,
        0x0b5a0, 0x056d0, 0x055b2, 0x049b0, 0x0a577, 0x0a4b0, 0x0aa50, 0x1b255, 0x06d20, 0x0ada0,
        0x14b63, 0x09370, 0x049f8, 0x04970, 0x064b0, 0x168a6, 0x0ea50, 0x06b20, 0x1a6c4, 0x0aae0,
        0x0a2e0, 0x0d2e3, 0x0c960, 0x0d557, 0x0d4a0, 0x0da50, 0x05d55, 0x056a0, 0x0a6d0, 0x055d4,
        0x052d0, 0x0a9b8, 0x0a950, 0x0b4a0, 0x0b6a6, 0x0ad50, 0x055a0, 0x0aba4, 0x0a5b0, 0x052b0,
        0x0b273, 0x06930, 0x07337, 0x06aa0, 0x0ad50, 0x14b55, 0x04b60, 0x0a570, 0x054e4, 0x0d160,
        0x0e968, 0x0d520, 0x0daa0, 0x16aa6, 0x056d0, 0x04ae0, 0x0a9d4, 0x0a2d0, 0x0d150, 0x0f252,
        0x0d520
    ];

    #endregion

    #region 公开方法 - 公历转农历

    /// <summary>
    /// 将公历日期转换为农历日期
    /// </summary>
    /// <param name="date">公历日期</param>
    /// <returns>农历日期信息</returns>
    /// <exception cref="ArgumentOutOfRangeException">日期超出支持范围时抛出</exception>
    public static LunarDate ConvertToLunar(DateTime date)
    {
        if (date.Year is < MinYear or > MaxYear)
        {
            throw new ArgumentOutOfRangeException(nameof(date), $"仅支持{MinYear}年至{MaxYear}年的日期转换");
        }

        var daysDiff = (date - BaseDate).Days;
        var lunarYear = MinYear;

        // 计算农历年份
        while (lunarYear < MaxYear)
        {
            var daysInYear = GetLunarYearDays(lunarYear);
            if (daysDiff < daysInYear)
            {
                break;
            }
            daysDiff -= daysInYear;
            lunarYear++;
        }

        // 计算农历月份和日期
        var lunarMonth = 1;
        var isLeapMonth = false;
        var leapMonth = GetLeapMonth(lunarYear);

        while (lunarMonth <= 12)
        {
            var daysInMonth = GetLunarMonthDays(lunarYear, lunarMonth);

            if (daysDiff < daysInMonth)
            {
                break;
            }

            daysDiff -= daysInMonth;

            // 检查闰月
            if (lunarMonth == leapMonth && !isLeapMonth)
            {
                isLeapMonth = true;
                daysInMonth = GetLeapMonthDays(lunarYear);
                if (daysDiff < daysInMonth)
                {
                    break;
                }
                daysDiff -= daysInMonth;
                isLeapMonth = false;
            }

            lunarMonth++;
        }

        var lunarDay = daysDiff + 1;

        return new LunarDate
        {
            Year = lunarYear,
            Month = lunarMonth,
            Day = lunarDay,
            IsLeapMonth = isLeapMonth,
            YearName = GetLunarYearName(lunarYear),
            MonthName = GetLunarMonthName(lunarMonth, isLeapMonth),
            DayName = GetLunarDayName(lunarDay),
            Zodiac = GetZodiac(lunarYear),
            TianganDizhi = GetTianganDizhi(lunarYear),
            SolarDate = date
        };
    }

    /// <summary>
    /// 将农历日期转换为公历日期
    /// </summary>
    /// <param name="lunarYear">农历年</param>
    /// <param name="lunarMonth">农历月</param>
    /// <param name="lunarDay">农历日</param>
    /// <param name="isLeapMonth">是否闰月</param>
    /// <returns>公历日期</returns>
    public static DateTime ConvertToSolar(int lunarYear, int lunarMonth, int lunarDay, bool isLeapMonth = false)
    {
        if (lunarYear is < MinYear or > MaxYear)
        {
            throw new ArgumentOutOfRangeException(nameof(lunarYear), $"仅支持{MinYear}年至{MaxYear}年的农历年份");
        }

        var totalDays = 0;

        // 计算从基准年到目标年的总天数
        for (var year = MinYear; year < lunarYear; year++)
        {
            totalDays += GetLunarYearDays(year);
        }

        // 计算目标年中到目标月的天数
        var leapMonth = GetLeapMonth(lunarYear);
        for (var month = 1; month < lunarMonth; month++)
        {
            totalDays += GetLunarMonthDays(lunarYear, month);
            if (month == leapMonth)
            {
                totalDays += GetLeapMonthDays(lunarYear);
            }
        }

        // 如果是闰月，还需要加上正常月的天数
        if (isLeapMonth && lunarMonth == leapMonth)
        {
            totalDays += GetLunarMonthDays(lunarYear, lunarMonth);
        }

        // 加上目标日的天数
        totalDays += lunarDay - 1;

        return BaseDate.AddDays(totalDays);
    }

    #endregion

    #region 公开方法 - 天干地支与生肖

    /// <summary>
    /// 获取指定年份的生肖
    /// </summary>
    /// <param name="year">年份（农历年）</param>
    /// <returns>生肖名称</returns>
    public static string GetZodiac(int year)
    {
        var index = (year - 1900) % 12;
        return Zodiac[index];
    }

    /// <summary>
    /// 获取指定年份的天干地支
    /// </summary>
    /// <param name="year">年份（农历年）</param>
    /// <returns>天干地支组合</returns>
    public static string GetTianganDizhi(int year)
    {
        var tianganIndex = (year - 1900) % 10;
        var dizhiIndex = (year - 1900) % 12;
        return Tiangan[tianganIndex] + Dizhi[dizhiIndex];
    }

    /// <summary>
    /// 获取指定公历日期的天干地支
    /// </summary>
    /// <param name="date">公历日期</param>
    /// <returns>日期天干地支</returns>
    public static string GetDayTianganDizhi(DateTime date)
    {
        // 以1900年1月1日为甲子日计算
        var baseDay = new DateTime(1900, 1, 1);
        var daysDiff = (date - baseDay).Days;
        var tianganIndex = (daysDiff + 6) % 10;  // 1900年1月1日为甲子日，甲为第0位
        var dizhiIndex = (daysDiff + 6) % 12;
        return Tiangan[tianganIndex] + Dizhi[dizhiIndex];
    }

    #endregion

    #region 公开方法 - 节气计算

    /// <summary>
    /// 获取指定年份的所有节气日期
    /// </summary>
    /// <param name="year">公历年份</param>
    /// <returns>节气日期列表</returns>
    public static List<SolarTerm> GetSolarTerms(int year)
    {
        var solarTerms = new List<SolarTerm>();

        for (var i = 0; i < 24; i++)
        {
            var date = GetSolarTermDate(year, i);
            solarTerms.Add(new SolarTerm
            {
                Name = SolarTerms[i],
                Date = date,
                Order = i + 1
            });
        }

        return solarTerms;
    }

    /// <summary>
    /// 获取指定日期所属的节气
    /// </summary>
    /// <param name="date">公历日期</param>
    /// <returns>节气信息，如果不是节气日则返回null</returns>
    public static SolarTerm? GetSolarTerm(DateTime date)
    {
        var solarTerms = GetSolarTerms(date.Year);
        return solarTerms.FirstOrDefault(st => st.Date.Date == date.Date);
    }

    /// <summary>
    /// 判断指定日期是否为节气
    /// </summary>
    /// <param name="date">公历日期</param>
    /// <returns>是否为节气日</returns>
    public static bool IsSolarTerm(DateTime date)
    {
        return GetSolarTerm(date) != null;
    }

    #endregion

    #region 公开方法 - 农历节日

    /// <summary>
    /// 获取指定农历日期的传统节日名称
    /// </summary>
    /// <param name="lunarMonth">农历月</param>
    /// <param name="lunarDay">农历日</param>
    /// <param name="isLeapMonth">是否闰月</param>
    /// <returns>节日名称，如果不是节日则返回null</returns>
    public static string? GetLunarFestival(int lunarMonth, int lunarDay, bool isLeapMonth = false)
    {
        if (isLeapMonth)
        {
            return null; // 闰月一般不过传统节日
        }

        return (lunarMonth, lunarDay) switch
        {
            (1, 1) => "春节",
            (1, 15) => "元宵节",
            (2, 2) => "龙抬头",
            (5, 5) => "端午节",
            (7, 7) => "七夕节",
            (7, 15) => "中元节",
            (8, 15) => "中秋节",
            (9, 9) => "重阳节",
            (10, 1) => "寒衣节",
            (10, 15) => "下元节",
            (12, 8) => "腊八节",
            (12, 23) => "小年",
            (12, 24) => "小年",
            (12, 30) => "除夕",
            (12, 29) => GetLunarMonthDays(DateTime.Now.Year, 12) == 29 ? "除夕" : null,
            _ => null
        };
    }

    /// <summary>
    /// 获取指定公历日期的传统节日名称
    /// </summary>
    /// <param name="date">公历日期</param>
    /// <returns>节日名称，如果不是节日则返回null</returns>
    public static string? GetSolarFestival(DateTime date)
    {
        return (date.Month, date.Day) switch
        {
            (1, 1) => "元旦",
            (2, 14) => "情人节",
            (3, 8) => "妇女节",
            (3, 12) => "植树节",
            (4, 1) => "愚人节",
            (5, 1) => "劳动节",
            (5, 4) => "青年节",
            (6, 1) => "儿童节",
            (7, 1) => "建党节",
            (8, 1) => "建军节",
            (9, 10) => "教师节",
            (10, 1) => "国庆节",
            (12, 25) => "圣诞节",
            _ => null
        };
    }

    #endregion

    #region 公开方法 - 工具方法

    /// <summary>
    /// 获取农历年份的中文名称
    /// </summary>
    /// <param name="year">农历年份</param>
    /// <returns>中文年份名称</returns>
    public static string GetLunarYearName(int year)
    {
        var yearStr = year.ToString();
        var chineseNumbers = new[] { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        var result = "";

        foreach (var digit in yearStr)
        {
            result += chineseNumbers[digit - '0'];
        }

        return result + "年";
    }

    /// <summary>
    /// 获取农历月份的中文名称
    /// </summary>
    /// <param name="month">农历月份</param>
    /// <param name="isLeapMonth">是否闰月</param>
    /// <returns>中文月份名称</returns>
    public static string GetLunarMonthName(int month, bool isLeapMonth = false)
    {
        var monthName = LunarMonths[month - 1];
        return isLeapMonth ? "闰" + monthName : monthName;
    }

    /// <summary>
    /// 获取农历日期的中文名称
    /// </summary>
    /// <param name="day">农历日期</param>
    /// <returns>中文日期名称</returns>
    public static string GetLunarDayName(int day)
    {
        return day is >= 1 and <= 30 ? LunarDays[day - 1] : day.ToString();
    }

    /// <summary>
    /// 判断指定农历年份是否有闰月
    /// </summary>
    /// <param name="year">农历年份</param>
    /// <returns>是否有闰月</returns>
    public static bool HasLeapMonth(int year)
    {
        return GetLeapMonth(year) > 0;
    }

    /// <summary>
    /// 获取农历年份的总天数
    /// </summary>
    /// <param name="year">农历年份</param>
    /// <returns>总天数</returns>
    public static int GetLunarYearDays(int year)
    {
        var days = 0;
        for (var month = 1; month <= 12; month++)
        {
            days += GetLunarMonthDays(year, month);
        }

        // 如果有闰月，加上闰月的天数
        if (HasLeapMonth(year))
        {
            days += GetLeapMonthDays(year);
        }

        return days;
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 获取农历年份的闰月月份
    /// </summary>
    /// <param name="year">农历年份</param>
    /// <returns>闰月月份，0表示无闰月</returns>
    private static int GetLeapMonth(int year)
    {
        return year is < MinYear or > MaxYear ? 0 : (LunarYearData[year - MinYear] & 0xf0000) >> 16;
    }

    /// <summary>
    /// 获取农历月份的天数
    /// </summary>
    /// <param name="year">农历年份</param>
    /// <param name="month">农历月份</param>
    /// <returns>月份天数</returns>
    private static int GetLunarMonthDays(int year, int month)
    {
        if (year is < MinYear or > MaxYear)
        {
            return 29;
        }

        var monthData = LunarYearData[year - MinYear] & 0xfff;
        return (monthData & (1 << (12 - month))) != 0 ? 30 : 29;
    }

    /// <summary>
    /// 获取农历年份闰月的天数
    /// </summary>
    /// <param name="year">农历年份</param>
    /// <returns>闰月天数</returns>
    private static int GetLeapMonthDays(int year)
    {
        return !HasLeapMonth(year) ? 0 : (LunarYearData[year - MinYear] & 0x10000) != 0 ? 30 : 29;
    }

    /// <summary>
    /// 计算指定年份第n个节气的日期（基于太阳黄经的真实算法）
    /// </summary>
    /// <param name="year">公历年份</param>
    /// <param name="termIndex">节气索引（0-23）</param>
    /// <returns>节气日期</returns>
    private static DateTime GetSolarTermDate(int year, int termIndex)
    {
        // 每个节气对应的太阳黄经度数
        var solarLongitudes = new double[]
        {
            315, 330, 345, 0, 15, 30, 45, 60, 75, 90, 105, 120,
            135, 150, 165, 180, 195, 210, 225, 240, 255, 270, 285, 300
        };

        var targetLongitude = solarLongitudes[termIndex];

        // 计算当年1月1日的儒略日数
        var jan1 = new DateTime(year, 1, 1);
        var julianDay = GetJulianDay(jan1);

        // 估算节气可能的日期范围
        var estimatedDay = GetEstimatedSolarTermDay(year, termIndex);
        var searchStart = julianDay + estimatedDay - 15;
        var searchEnd = julianDay + estimatedDay + 15;

        // 二分法搜索精确的节气时刻
        var result = BinarySearchSolarTerm(searchStart, searchEnd, targetLongitude);

        return JulianDayToDateTime(result);
    }

    /// <summary>
    /// 计算儒略日数
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>儒略日数</returns>
    private static double GetJulianDay(DateTime date)
    {
        var year = date.Year;
        var month = date.Month;
        var day = date.Day + (date.Hour / 24.0) + (date.Minute / 1440.0) + (date.Second / 86400.0);

        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }

        var a = year / 100;
        var b = 2 - a + (a / 4);

        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
    }

    /// <summary>
    /// 将儒略日数转换为DateTime
    /// </summary>
    /// <param name="julianDay">儒略日数</param>
    /// <returns>日期时间</returns>
    private static DateTime JulianDayToDateTime(double julianDay)
    {
        var z = Math.Floor(julianDay + 0.5);
        var f = julianDay + 0.5 - z;

        double a;
        if (z < 2299161)
        {
            a = z;
        }
        else
        {
            var alpha = Math.Floor((z - 1867216.25) / 36524.25);
            a = z + 1 + alpha - Math.Floor(alpha / 4);
        }

        var b = a + 1524;
        var c = Math.Floor((b - 122.1) / 365.25);
        var d = Math.Floor(365.25 * c);
        var e = Math.Floor((b - d) / 30.6001);

        var day = b - d - Math.Floor(30.6001 * e) + f;
        var month = e < 14 ? e - 1 : e - 13;
        var year = month > 2 ? c - 4716 : c - 4715;

        var wholeDays = Math.Floor(day);
        var fractionalDay = day - wholeDays;

        var hours = fractionalDay * 24;
        var wholeHours = Math.Floor(hours);
        var fractionalHours = hours - wholeHours;

        var minutes = fractionalHours * 60;
        var wholeMinutes = Math.Floor(minutes);
        var fractionalMinutes = minutes - wholeMinutes;

        var seconds = Math.Floor(fractionalMinutes * 60);

        return new DateTime((int)year, (int)month, (int)wholeDays, (int)wholeHours, (int)wholeMinutes, (int)seconds);
    }

    /// <summary>
    /// 计算太阳黄经（简化版VSOP87算法）
    /// </summary>
    /// <param name="julianDay">儒略日数</param>
    /// <returns>太阳黄经（度）</returns>
    private static double CalculateSolarLongitude(double julianDay)
    {
        // 儒略世纪数
        var t = (julianDay - 2451545.0) / 36525.0;

        // 太阳的平黄经
        var l0 = 280.46646 + (36000.76983 * t) + (0.0003032 * t * t);

        // 太阳的平近点角
        var m = 357.52911 + (35999.05029 * t) - (0.0001537 * t * t);

        // 转换为弧度
        var mRad = m * Math.PI / 180.0;

        // 黄经修正项（主要项）
        var c = ((1.914602 - (0.004817 * t) - (0.000014 * t * t)) * Math.Sin(mRad)) +
                ((0.019993 - (0.000101 * t)) * Math.Sin(2 * mRad)) +
                (0.000289 * Math.Sin(3 * mRad));

        // 真黄经
        var lambda = l0 + c;

        // 章动修正（简化）
        var omega = 125.04452 - (1934.136261 * t);
        var omegaRad = omega * Math.PI / 180.0;
        var deltaPsi = -17.20 * Math.Sin(omegaRad) / 3600.0;

        lambda += deltaPsi;

        // 确保角度在0-360度范围内
        lambda %= 360.0;
        if (lambda < 0)
        {
            lambda += 360.0;
        }

        return lambda;
    }

    /// <summary>
    /// 获取节气的估算日期（从年初开始的天数）
    /// </summary>
    /// <param name="year">年份</param>
    /// <param name="termIndex">节气索引</param>
    /// <returns>估算天数</returns>
    private static int GetEstimatedSolarTermDay(int year, int termIndex)
    {
        // 基于统计平均值的估算表（从1月1日开始的天数）
        var estimatedDays = new[]
        {
            4, 19, 35, 51, 66, 81, 96, 112, 128, 144, 160, 176,
            192, 208, 224, 240, 256, 272, 288, 304, 320, 336, 352, 3
        };

        var baseDay = estimatedDays[termIndex];

        // 对于小寒，如果是下一年的，需要调整
        if (termIndex == 23 && baseDay < 10)
        {
            baseDay += 365;
            if (IsLeapYear(year))
            {
                baseDay += 1;
            }
        }

        return baseDay;
    }

    /// <summary>
    /// 二分法搜索节气精确时刻
    /// </summary>
    /// <param name="startJd">搜索开始的儒略日</param>
    /// <param name="endJd">搜索结束的儒略日</param>
    /// <param name="targetLongitude">目标黄经</param>
    /// <returns>精确的儒略日数</returns>
    private static double BinarySearchSolarTerm(double startJd, double endJd, double targetLongitude)
    {
        const double Precision = 1.0 / 86400.0; // 1秒的精度
        const int MaxIterations = 50;

        var iterations = 0;
        while (endJd - startJd > Precision && iterations < MaxIterations)
        {
            var midJd = (startJd + endJd) / 2.0;
            var longitude = CalculateSolarLongitude(midJd);

            // 处理角度跨越0度的情况
            var diff = GetAngleDifference(longitude, targetLongitude);

            if (Math.Abs(diff) < 0.01) // 0.01度的精度
            {
                return midJd;
            }

            // 判断太阳是否还未到达目标黄经
            if (diff > 0)
            {
                endJd = midJd;
            }
            else
            {
                startJd = midJd;
            }

            iterations++;
        }

        return (startJd + endJd) / 2.0;
    }

    /// <summary>
    /// 计算两个角度之间的差值（考虑360度循环）
    /// </summary>
    /// <param name="angle1">角度1</param>
    /// <param name="angle2">角度2</param>
    /// <returns>角度差</returns>
    private static double GetAngleDifference(double angle1, double angle2)
    {
        var diff = angle1 - angle2;
        while (diff > 180)
        {
            diff -= 360;
        }

        while (diff < -180)
        {
            diff += 360;
        }

        return diff;
    }

    /// <summary>
    /// 判断是否为闰年
    /// </summary>
    /// <param name="year">年份</param>
    /// <returns>是否为闰年</returns>
    private static bool IsLeapYear(int year)
    {
        return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
    }

    #endregion
}

/// <summary>
/// 农历日期信息
/// </summary>
public class LunarDate
{
    /// <summary>
    /// 农历年份
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// 农历月份
    /// </summary>
    public int Month { get; set; }

    /// <summary>
    /// 农历日期
    /// </summary>
    public int Day { get; set; }

    /// <summary>
    /// 是否闰月
    /// </summary>
    public bool IsLeapMonth { get; set; }

    /// <summary>
    /// 农历年份中文名称
    /// </summary>
    public string YearName { get; set; } = string.Empty;

    /// <summary>
    /// 农历月份中文名称
    /// </summary>
    public string MonthName { get; set; } = string.Empty;

    /// <summary>
    /// 农历日期中文名称
    /// </summary>
    public string DayName { get; set; } = string.Empty;

    /// <summary>
    /// 生肖
    /// </summary>
    public string Zodiac { get; set; } = string.Empty;

    /// <summary>
    /// 天干地支
    /// </summary>
    public string TianganDizhi { get; set; } = string.Empty;

    /// <summary>
    /// 对应的公历日期
    /// </summary>
    public DateTime SolarDate { get; set; }

    /// <summary>
    /// 农历节日名称
    /// </summary>
    public string? Festival => LunarCalendarHelper.GetLunarFestival(Month, Day, IsLeapMonth);

    /// <summary>
    /// 农历日期的完整中文表示
    /// </summary>
    public string FullName => $"{YearName}{MonthName}{DayName}";

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的农历日期</returns>
    public override string ToString()
    {
        var festival = Festival;
        var festivalText = !string.IsNullOrEmpty(festival) ? $" ({festival})" : "";
        return $"{FullName} {Zodiac}年 {TianganDizhi}{festivalText}";
    }
}

/// <summary>
/// 节气信息
/// </summary>
public class SolarTerm
{
    /// <summary>
    /// 节气名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 节气日期
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// 节气序号（1-24）
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    /// <returns>格式化的节气信息</returns>
    public override string ToString()
    {
        return $"{Name} ({Date:yyyy年MM月dd日})";
    }
}
