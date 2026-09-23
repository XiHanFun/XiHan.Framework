// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Core;

namespace XiHan.Framework.Utils.Tests.Core;

/// <summary>
/// 时间边界测试
/// </summary>
/// <remarks>
/// <c>GetEndOfYear</c> 原先写死 <c>23:59:59.999</c>，
/// 而同族的 <c>GetEndOfDay</c> / <c>GetEndOfWeek</c> / <c>GetEndOfMonth</c> 用的是"下一周期起点减一刻度"，
/// 两者相差将近 1 毫秒，用作闭区间上界时会漏掉跨年前最后 1 毫秒内的数据。
/// </remarks>
public class DateTimeHelperBoundaryTests
{
    /// <summary>
    /// 各周期的结束时间都精确到该周期的最后一个刻度
    /// </summary>
    [Fact]
    public void GetEndOfPeriod_IsLastTickOfThePeriod()
    {
        var date = new DateTime(2024, 6, 15, 10, 30, 0);

        Assert.Equal(DateTimeHelper.GetStartOfDay(date).AddDays(1).AddTicks(-1), DateTimeHelper.GetEndOfDay(date));
        Assert.Equal(DateTimeHelper.GetStartOfWeek(date).AddDays(7).AddTicks(-1), DateTimeHelper.GetEndOfWeek(date));
        Assert.Equal(DateTimeHelper.GetStartOfMonth(date).AddMonths(1).AddTicks(-1), DateTimeHelper.GetEndOfMonth(date));
        Assert.Equal(DateTimeHelper.GetStartOfYear(date).AddYears(1).AddTicks(-1), DateTimeHelper.GetEndOfYear(date));
    }

    /// <summary>
    /// 四个结束时间的亚秒部分口径一致
    /// </summary>
    [Fact]
    public void GetEndOfPeriod_UsesConsistentSubSecondPrecision()
    {
        var date = new DateTime(2024, 6, 15);

        var subSecondTicks = new[]
        {
            DateTimeHelper.GetEndOfDay(date).Ticks % TimeSpan.TicksPerSecond,
            DateTimeHelper.GetEndOfWeek(date).Ticks % TimeSpan.TicksPerSecond,
            DateTimeHelper.GetEndOfMonth(date).Ticks % TimeSpan.TicksPerSecond,
            DateTimeHelper.GetEndOfYear(date).Ticks % TimeSpan.TicksPerSecond
        };

        Assert.All(subSecondTicks, ticks => Assert.Equal(TimeSpan.TicksPerSecond - 1, ticks));
    }

    /// <summary>
    /// 年末最后一毫秒内的时刻落在本年区间内
    /// </summary>
    /// <remarks>原实现下 12-31 23:59:59.9995 会被判定为不在本年区间内。</remarks>
    [Fact]
    public void GetEndOfYear_IncludesLastMillisecondOfTheYear()
    {
        var almostNewYear = new DateTime(2024, 12, 31, 23, 59, 59).AddTicks(9_995_000);

        Assert.True(DateTimeHelper.IsInRange(almostNewYear, DateTimeHelper.GetStartOfYear(almostNewYear), DateTimeHelper.GetEndOfYear(almostNewYear)));
    }

    /// <summary>
    /// 相邻周期的结束时间与下一周期的开始时间严丝合缝
    /// </summary>
    [Fact]
    public void GetEndOfYear_AbutsStartOfNextYear()
    {
        var date = new DateTime(2024, 3, 1);
        var nextYear = DateTimeHelper.GetStartOfYear(date.AddYears(1));

        Assert.Equal(nextYear, DateTimeHelper.GetEndOfYear(date).AddTicks(1));
    }

    /// <summary>
    /// 纯日期计算不依赖时区数据库
    /// </summary>
    /// <remarks>
    /// 中国时区原先在静态字段初始化器里解析，精简容器缺时区库时会抛
    /// <see cref="TypeInitializationException"/>，把整个类型拖垮，
    /// 连这些不涉及时区的方法也一并不可用。现改为延迟解析。
    /// </remarks>
    [Fact]
    public void PureDateCalculations_DoNotDependOnTimeZoneData()
    {
        Assert.True(DateTimeHelper.IsWorkDay(new DateTime(2024, 6, 17)));
        Assert.True(DateTimeHelper.IsWeekend(new DateTime(2024, 6, 16)));
        Assert.Equal(2, DateTimeHelper.GetQuarter(new DateTime(2024, 6, 15)));
        Assert.Equal(new DateTime(2024, 1, 1), DateTimeHelper.GetStartOfYear(new DateTime(2024, 6, 15)));
    }

    /// <summary>
    /// 中国时区转换仍然可用
    /// </summary>
    [Fact]
    public void ToChinaTime_ConvertsUtcToUtcPlusEight()
    {
        var utc = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2024, 6, 15, 8, 0, 0), DateTimeHelper.ToChinaTime(utc));
    }
}
