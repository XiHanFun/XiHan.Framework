// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
    /// 干支纪年基准年份：公元 4 年为甲子年
    /// </summary>
    private const int GanzhiEpochYear = 4;

    /// <summary>
    /// 干支纪日基准日期：1900 年 1 月 1 日为甲戌日
    /// </summary>
    private static readonly DateTime DayGanzhiEpoch = new(1900, 1, 1);

    /// <summary>
    /// 干支纪日基准日的天干下标（甲）
    /// </summary>
    private const int DayGanzhiEpochTianganIndex = 0;

    /// <summary>
    /// 干支纪日基准日的地支下标（戌）
    /// </summary>
    private const int DayGanzhiEpochDizhiIndex = 10;

    /// <summary>
    /// 节气与农历日期采用的时区偏移（东八区），节气时刻按北京时间归日
    /// </summary>
    private const double SolarTermTimeZoneOffsetDays = 8.0 / 24.0;

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
    /// </summary>
    /// <remarks>
    /// 通用农历表，每个元素按位编码一个农历年，位布局如下：
    /// <list type="bullet">
    /// <item>bit 0-3：闰月月份，0 表示该年无闰月</item>
    /// <item>bit 4-15：12 个普通月的大小，bit 15 是正月、bit 4 是腊月，置位为大月 30 天，否则小月 29 天</item>
    /// <item>bit 16：闰月的大小，置位为大月 30 天，否则小月 29 天</item>
    /// </list>
    /// 原先的注释与三个解码方法都按"低 12 位是月大小、bit 14-17 是闰月月份"理解，
    /// 与本表实际编码错位，导致 <see cref="GetLeapMonth"/> 在整个 1900-2100 区间几乎恒返回 0、
    /// 每个闰年少算一整个月，公农历互转累计偏差约 1900 天（2024-02-10 会被换算成 2027 年四月廿三）。
    /// 校验方式：1900 年闰八月、2020 年闰四月、2023 年闰二月，按本布局解出的值与史实一致。
    /// </remarks>
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

            // 闰月紧跟在同名的普通月之后，落在闰月里就带着 isLeapMonth 跳出
            if (lunarMonth == leapMonth)
            {
                var daysInLeapMonth = GetLeapMonthDays(lunarYear);
                if (daysDiff < daysInLeapMonth)
                {
                    isLeapMonth = true;
                    break;
                }

                daysDiff -= daysInLeapMonth;
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
    /// <exception cref="ArgumentOutOfRangeException">年、月、日超出有效范围时抛出</exception>
    /// <exception cref="ArgumentException">指定年份没有该闰月时抛出</exception>
    public static DateTime ConvertToSolar(int lunarYear, int lunarMonth, int lunarDay, bool isLeapMonth = false)
    {
        if (lunarYear is < MinYear or > MaxYear)
        {
            throw new ArgumentOutOfRangeException(nameof(lunarYear), $"仅支持{MinYear}年至{MaxYear}年的农历年份");
        }

        if (lunarMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(lunarMonth), "农历月份必须在 1 到 12 之间，闰月请用 isLeapMonth 指定");
        }

        // 越界的月份会让 GetLunarMonthDays 去读闰月编码位，算出的天数毫无意义，因此必须先挡住
        if (isLeapMonth && GetLeapMonth(lunarYear) != lunarMonth)
        {
            throw new ArgumentException($"{lunarYear} 年没有闰{lunarMonth}月", nameof(isLeapMonth));
        }

        var daysInTargetMonth = isLeapMonth ? GetLeapMonthDays(lunarYear) : GetLunarMonthDays(lunarYear, lunarMonth);
        if (lunarDay < 1 || lunarDay > daysInTargetMonth)
        {
            throw new ArgumentOutOfRangeException(nameof(lunarDay), $"该月只有 {daysInTargetMonth} 天");
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
    /// <remarks>生肖跟地支一一对应，因此与 <see cref="GetTianganDizhi"/> 共用同一个地支基准。</remarks>
    public static string GetZodiac(int year)
    {
        return Zodiac[Mod(year - GanzhiEpochYear, 12)];
    }

    /// <summary>
    /// 获取指定年份的天干地支
    /// </summary>
    /// <param name="year">年份（农历年）</param>
    /// <returns>天干地支组合</returns>
    /// <remarks>
    /// 原实现以 1900 年为甲子年推算天干，但 1900 年是庚子年，天干因此恒定偏 6 位
    /// （1900 算成"甲子"、2024 算成"戊辰"，正确值是"庚子"和"甲辰"）；
    /// 地支和生肖恰好因为 1900 年确实是子年而没受影响。
    /// 改用公元 4 年甲子这一通用基准，同时修正天干偏移并支持 1900 年以前的年份。
    /// </remarks>
    public static string GetTianganDizhi(int year)
    {
        return Tiangan[Mod(year - GanzhiEpochYear, 10)] + Dizhi[Mod(year - GanzhiEpochYear, 12)];
    }

    /// <summary>
    /// 获取指定公历日期的天干地支
    /// </summary>
    /// <param name="date">公历日期</param>
    /// <returns>日期天干地支</returns>
    /// <remarks>
    /// 原实现的注释声称"1900 年 1 月 1 日为甲子日、甲为第 0 位"，代码却又给两个下标都加了 6，
    /// 注释与代码自相矛盾；而 1900 年 1 月 1 日实际是甲戌日（儒略日 2415021，
    /// 按通用公式 天干=(JDN+9)%10、地支=(JDN+1)%12 可验证），
    /// 于是锚点和偏移两处都错，任何日期的日干支都算不对（1900-01-01 被算成"庚午"）。
    /// 现按该日为甲戌日重新定基准，并用非负取模支持 1900 年以前的日期。
    /// </remarks>
    public static string GetDayTianganDizhi(DateTime date)
    {
        var daysDiff = (date.Date - DayGanzhiEpoch).Days;
        return Tiangan[Mod(daysDiff + DayGanzhiEpochTianganIndex, 10)] +
            Dizhi[Mod(daysDiff + DayGanzhiEpochDizhiIndex, 12)];
    }

    #endregion

    #region 公开方法 - 节气计算

    /// <summary>
    /// 获取指定年份的所有节气日期
    /// </summary>
    /// <param name="year">公历年份</param>
    /// <returns>按日期先后排列的 24 个节气，时刻为北京时间</returns>
    /// <remarks>
    /// 返回的是落在该公历年内的 24 个节气，即小寒、大寒、立春……冬至，
    /// <see cref="SolarTerm.Order"/> 是它在本年内按日期排列的序号（1-24）。
    /// <para>
    /// 原实现有两处问题：一是估算表按"小寒打头"排列、而节气名与黄经表按"立春打头"排列，
    /// 两者整体错位 2 个下标，±15 天的搜索窗口根本框不住目标黄经，24 个节气日期全错约 15 天
    /// （2024 年立春被算成 1 月 19 日）；二是按"立春年"取 24 个节气，
    /// 一月份的小寒、大寒被算到上一年的列表里，<see cref="GetSolarTerm"/> 按公历年查找时永远查不到它们。
    /// 现在不再依赖估算表，直接按日扫描出太阳黄经跨过目标度数的那一天再二分，年份归属也改为公历年。
    /// </para>
    /// </remarks>
    public static List<SolarTerm> GetSolarTerms(int year)
    {
        var solarTerms = new List<SolarTerm>();

        for (var i = 0; i < SolarTerms.Length; i++)
        {
            solarTerms.Add(new SolarTerm
            {
                Name = SolarTerms[i],
                Date = GetSolarTermDate(year, i)
            });
        }

        solarTerms.Sort((left, right) => left.Date.CompareTo(right.Date));

        for (var i = 0; i < solarTerms.Count; i++)
        {
            solarTerms[i].Order = i + 1;
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
    /// <param name="lunarYear">农历年份，用于判断腊月二十九是不是除夕；不传则不判定这一天</param>
    /// <returns>节日名称，如果不是节日则返回null</returns>
    /// <remarks>
    /// 腊月只有 29 天时，廿九才是除夕，这取决于所查询的那一个农历年。
    /// 原实现拿 <c>DateTime.Now.Year</c>（而且是公历年）去查腊月天数，
    /// 既与被查询的日期无关，又把公历年当成农历年用，
    /// 结果同一个日期在不同年份运行会得到不同答案。
    /// 现在改为由调用方传入农历年份；<see cref="LunarDate.Festival"/> 会自动带上自己的年份。
    /// </remarks>
    public static string? GetLunarFestival(int lunarMonth, int lunarDay, bool isLeapMonth = false, int? lunarYear = null)
    {
        if (isLeapMonth)
        {
            return null; // 闰月一般不过传统节日
        }

        if (lunarMonth == 12 && lunarDay == 29)
        {
            return lunarYear is not null && GetLunarMonthDays(lunarYear.Value, 12) == 29 ? "除夕" : null;
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
    /// <exception cref="ArgumentOutOfRangeException">月份不在 1 到 12 之间时抛出</exception>
    public static string GetLunarMonthName(int month, bool isLeapMonth = false)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "农历月份必须在 1 到 12 之间");
        }

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
        // 闰月月份在 bit 0-3，不是 bit 16-19，位布局见 LunarYearData 说明
        return year is < MinYear or > MaxYear ? 0 : LunarYearData[year - MinYear] & 0xf;
    }

    /// <summary>
    /// 获取农历月份的天数
    /// </summary>
    /// <param name="year">农历年份</param>
    /// <param name="month">农历月份</param>
    /// <returns>月份天数</returns>
    private static int GetLunarMonthDays(int year, int month)
    {
        if (year is < MinYear or > MaxYear || month is < 1 or > 12)
        {
            return 29;
        }

        // 正月在 bit 15、腊月在 bit 4，因此第 month 个月对应 0x10000 >> month，
        // 而不是在低 12 位里取 1 << (12 - month)，位布局见 LunarYearData 说明
        return (LunarYearData[year - MinYear] & (0x10000 >> month)) != 0 ? 30 : 29;
    }

    /// <summary>
    /// 获取农历年份闰月的天数
    /// </summary>
    /// <param name="year">农历年份</param>
    /// <returns>闰月天数</returns>
    private static int GetLeapMonthDays(int year)
    {
        // 闰月大小在 bit 16，这一处原本就与 LunarYearData 的实际布局一致
        return !HasLeapMonth(year) ? 0 : (LunarYearData[year - MinYear] & 0x10000) != 0 ? 30 : 29;
    }

    /// <summary>
    /// 取非负余数
    /// </summary>
    /// <param name="value">被除数</param>
    /// <param name="modulus">除数，必须为正</param>
    /// <returns>落在 [0, modulus) 区间内的余数</returns>
    /// <remarks>
    /// C# 的 % 对负被除数返回负值，直接拿去索引干支/生肖数组会越界，
    /// 而干支推算本身对公元前后都成立，不应因为取模写法而限制在 1900 年以后。
    /// </remarks>
    private static int Mod(int value, int modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }

    /// <summary>
    /// 计算指定公历年内第 n 个节气的时刻（基于太阳黄经）
    /// </summary>
    /// <param name="year">公历年份</param>
    /// <param name="termIndex">节气在 <see cref="SolarTerms"/> 中的下标（0 为立春）</param>
    /// <returns>该节气在当年的北京时间时刻</returns>
    /// <exception cref="InvalidOperationException">当年内找不到太阳黄经跨过目标度数的时刻时抛出</exception>
    /// <remarks>
    /// 立春对应黄经 315 度，此后每个节气递增 15 度。
    /// 原实现靠一张按"小寒打头"排列的估算表圈定 ±15 天的搜索窗口，与本下标体系错位 2 位，
    /// 二分区间内根本不含目标黄经，只能收敛到区间端点。
    /// 现在改为按日扫描找出黄经跨过目标度数的那一天，再在这一天内二分，不依赖任何估算表。
    /// 太阳黄经一年只跨过某个度数一次，因此扫描到的首个跨越点就是当年的该节气。
    /// </remarks>
    private static DateTime GetSolarTermDate(int year, int termIndex)
    {
        var targetLongitude = (315.0 + (15.0 * termIndex)) % 360.0;

        // 按北京时间的自然日扫描，保证"落在该公历年内"的判断与返回值同一时区
        var dayCount = DateTime.IsLeapYear(year) ? 366 : 365;
        var yearStart = new DateTime(year, 1, 1);

        var previousJd = GetJulianDay(yearStart) - SolarTermTimeZoneOffsetDays;
        var previousDiff = GetAngleDifference(CalculateSolarLongitude(previousJd), targetLongitude);

        for (var day = 1; day <= dayCount; day++)
        {
            var currentJd = GetJulianDay(yearStart.AddDays(day)) - SolarTermTimeZoneOffsetDays;
            var currentDiff = GetAngleDifference(CalculateSolarLongitude(currentJd), targetLongitude);

            // 黄经自身单调增长，差值由负转正即为跨越点；
            // 由正跳负是 GetAngleDifference 在 ±180 度处的折返，不是跨越点
            if (previousDiff < 0 && currentDiff >= 0)
            {
                var crossingJd = BinarySearchSolarTerm(previousJd, currentJd, targetLongitude);
                return JulianDayToDateTime(crossingJd + SolarTermTimeZoneOffsetDays);
            }

            previousJd = currentJd;
            previousDiff = currentDiff;
        }

        throw new InvalidOperationException($"{year} 年内未找到太阳黄经跨过 {targetLongitude} 度的时刻。");
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
    /// 二分法搜索节气精确时刻
    /// </summary>
    /// <param name="startJd">跨越点之前的儒略日，此刻黄经差为负</param>
    /// <param name="endJd">跨越点之后的儒略日，此刻黄经差为非负</param>
    /// <param name="targetLongitude">目标黄经</param>
    /// <returns>精确的儒略日数</returns>
    /// <remarks>
    /// 调用方保证区间两端的黄经差异号，因此一路二分到 1 秒精度即可。
    /// 原实现在 |差值| &lt; 0.01 度时提前返回，0.01 度对应约 15 分钟的太阳视运动，
    /// 节气时刻落在午夜前后时足以把日期判到隔壁天，故去掉这个提前返回。
    /// </remarks>
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
    public string? Festival => LunarCalendarHelper.GetLunarFestival(Month, Day, IsLeapMonth, Year);

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
