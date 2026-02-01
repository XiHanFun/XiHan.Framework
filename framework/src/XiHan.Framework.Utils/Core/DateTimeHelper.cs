#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DateTimeHelper
// Guid:3d2e1f0a-9b8c-7d6e-5f4a-3b2c1d0e9f8a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 23:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 日期时间帮助类
/// </summary>
public static class DateTimeHelper
{
    #region 常量定义

    /// <summary>
    /// 一周的开始日期（周一）
    /// </summary>
    public const DayOfWeek WeekStartDay = DayOfWeek.Monday;

    /// <summary>
    /// 中国时区
    /// </summary>
    private static readonly TimeZoneInfo ChinaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    #endregion

    #region 基础判断

    /// <summary>
    /// 判断是否为工作日（周一到周五）
    /// </summary>
    /// <param name="date">要判断的日期</param>
    /// <returns>如果是工作日则返回 true，否则返回 false</returns>
    public static bool IsWorkDay(DateTime date)
    {
        return date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
    }

    /// <summary>
    /// 判断是否为周末（周六或周日）
    /// </summary>
    /// <param name="date">要判断的日期</param>
    /// <returns>如果是周末则返回 true，否则返回 false</returns>
    public static bool IsWeekend(DateTime date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }

    /// <summary>
    /// 判断是否为同一天
    /// </summary>
    /// <param name="date1">第一个日期</param>
    /// <param name="date2">第二个日期</param>
    /// <returns>如果是同一天则返回 true，否则返回 false</returns>
    public static bool IsSameDay(DateTime date1, DateTime date2)
    {
        return date1.Date == date2.Date;
    }

    /// <summary>
    /// 判断是否为闰年
    /// </summary>
    /// <param name="year">年份</param>
    /// <returns>如果是闰年则返回 true，否则返回 false</returns>
    public static bool IsLeapYear(int year)
    {
        return DateTime.IsLeapYear(year);
    }

    /// <summary>
    /// 判断日期是否在指定范围内
    /// </summary>
    /// <param name="date">要判断的日期</param>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <param name="includeStartDate">是否包含开始日期</param>
    /// <param name="includeEndDate">是否包含结束日期</param>
    /// <returns>如果在范围内则返回 true，否则返回 false</returns>
    public static bool IsInRange(DateTime date, DateTime startDate, DateTime endDate,
        bool includeStartDate = true, bool includeEndDate = true)
    {
        var afterStart = includeStartDate ? date >= startDate : date > startDate;
        var beforeEnd = includeEndDate ? date <= endDate : date < endDate;
        return afterStart && beforeEnd;
    }

    #endregion

    #region 年龄计算

    /// <summary>
    /// 根据出生日期计算年龄
    /// </summary>
    /// <param name="birthDate">出生日期</param>
    /// <param name="referenceDate">参考日期，默认为当前日期</param>
    /// <returns>年龄</returns>
    public static int GetAge(DateTime birthDate, DateTime? referenceDate = null)
    {
        var reference = referenceDate ?? DateTime.Now;
        var age = reference.Year - birthDate.Year;

        // 如果还没到生日，则年龄减一
        if (reference.Month < birthDate.Month ||
            (reference.Month == birthDate.Month && reference.Day < birthDate.Day))
        {
            age--;
        }

        return Math.Max(0, age);
    }

    /// <summary>
    /// 获取详细年龄信息
    /// </summary>
    /// <param name="birthDate">出生日期</param>
    /// <param name="referenceDate">参考日期，默认为当前日期</param>
    /// <returns>年龄详情（年、月、天）</returns>
    public static (int Years, int Months, int Days) GetDetailedAge(DateTime birthDate, DateTime? referenceDate = null)
    {
        var reference = referenceDate ?? DateTime.Now;

        if (birthDate > reference)
        {
            return (0, 0, 0);
        }

        var years = reference.Year - birthDate.Year;
        var months = reference.Month - birthDate.Month;
        var days = reference.Day - birthDate.Day;

        if (days < 0)
        {
            months--;
            days += DateTime.DaysInMonth(reference.AddMonths(-1).Year, reference.AddMonths(-1).Month);
        }

        if (months < 0)
        {
            years--;
            months += 12;
        }

        return (years, months, days);
    }

    #endregion

    #region 友好时间显示

    /// <summary>
    /// 将时间格式化为友好的显示格式（如：刚刚、5分钟前、昨天等）
    /// </summary>
    /// <param name="dateTime">要格式化的时间</param>
    /// <param name="referenceTime">参考时间，默认为当前时间</param>
    /// <returns>友好的时间显示</returns>
    public static string FormatFriendly(DateTime dateTime, DateTime? referenceTime = null)
    {
        var reference = referenceTime ?? DateTime.Now;
        var timeSpan = reference - dateTime;

        // 未来时间
        if (timeSpan.TotalSeconds < 0)
        {
            var futureSpan = dateTime - reference;
            if (futureSpan.TotalMinutes < 1)
            {
                return "即将";
            }
            if (futureSpan.TotalHours < 1)
            {
                return $"{(int)futureSpan.TotalMinutes}分钟后";
            }
            if (futureSpan.TotalDays < 1)
            {
                return $"{(int)futureSpan.TotalHours}小时后";
            }
            return dateTime.ToString("yyyy-MM-dd HH:mm");
        }

        // 过去时间
        return timeSpan.TotalSeconds switch
        {
            < 30 => "刚刚",
            < 60 => $"{(int)timeSpan.TotalSeconds}秒前",
            < 3600 => $"{(int)timeSpan.TotalMinutes}分钟前",
            < 86400 => $"{(int)timeSpan.TotalHours}小时前",
            < 172800 => "昨天",
            < 259200 => "前天",
            < 604800 => $"{(int)timeSpan.TotalDays}天前",
            < 2592000 => $"{(int)(timeSpan.TotalDays / 7)}周前",
            < 31536000 => $"{(int)(timeSpan.TotalDays / 30)}个月前",
            _ => $"{(int)(timeSpan.TotalDays / 365)}年前"
        };
    }

    /// <summary>
    /// 格式化时间段为友好显示
    /// </summary>
    /// <param name="timeSpan">时间段</param>
    /// <returns>友好的时间段显示</returns>
    public static string FormatFriendlyDuration(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{(int)timeSpan.TotalDays}天{timeSpan.Hours}小时{timeSpan.Minutes}分钟";
        }

        if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours}小时{timeSpan.Minutes}分钟";
        }

        if (timeSpan.TotalMinutes >= 1)
        {
            return $"{timeSpan.Minutes}分钟{timeSpan.Seconds}秒";
        }

        return $"{timeSpan.Seconds}秒";
    }

    #endregion

    #region 时间计算

    /// <summary>
    /// 添加工作日（跳过周末）
    /// </summary>
    /// <param name="date">起始日期</param>
    /// <param name="workDays">要添加的工作日数</param>
    /// <returns>添加工作日后的日期</returns>
    public static DateTime AddWorkDays(DateTime date, int workDays)
    {
        if (workDays == 0)
        {
            return date;
        }

        var direction = workDays > 0 ? 1 : -1;
        var daysToAdd = Math.Abs(workDays);
        var result = date;

        while (daysToAdd > 0)
        {
            result = result.AddDays(direction);
            if (IsWorkDay(result))
            {
                daysToAdd--;
            }
        }

        return result;
    }

    /// <summary>
    /// 计算两个日期之间的工作日数量
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <param name="includeStartDate">是否包含开始日期</param>
    /// <param name="includeEndDate">是否包含结束日期</param>
    /// <returns>工作日数量</returns>
    public static int GetWorkDaysBetween(DateTime startDate, DateTime endDate,
        bool includeStartDate = true, bool includeEndDate = true)
    {
        if (startDate > endDate)
        {
            return -GetWorkDaysBetween(endDate, startDate, includeEndDate, includeStartDate);
        }

        var workDays = 0;
        var current = includeStartDate ? startDate : startDate.AddDays(1);
        var end = includeEndDate ? endDate : endDate.AddDays(-1);

        while (current <= end)
        {
            if (IsWorkDay(current))
            {
                workDays++;
            }
            current = current.AddDays(1);
        }

        return workDays;
    }

    #endregion

    #region 周期获取

    /// <summary>
    /// 获取一年中的第几周
    /// </summary>
    /// <param name="date">日期</param>
    /// <param name="cultureInfo">区域信息，默认为当前区域</param>
    /// <returns>周数</returns>
    public static int GetWeekOfYear(DateTime date, CultureInfo? cultureInfo = null)
    {
        var culture = cultureInfo ?? CultureInfo.CurrentCulture;
        var calendar = culture.Calendar;
        var dateTimeFormat = culture.DateTimeFormat;

        return calendar.GetWeekOfYear(date, dateTimeFormat.CalendarWeekRule, dateTimeFormat.FirstDayOfWeek);
    }

    /// <summary>
    /// 获取季度
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>季度（1-4）</returns>
    public static int GetQuarter(DateTime date)
    {
        return ((date.Month - 1) / 3) + 1;
    }

    /// <summary>
    /// 获取季度名称
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>季度名称</returns>
    public static string GetQuarterName(DateTime date)
    {
        return $"第{GetQuarter(date)}季度";
    }

    #endregion

    #region 时间边界

    /// <summary>
    /// 获取一天的开始时间（00:00:00）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>当天开始时间</returns>
    public static DateTime GetStartOfDay(DateTime date)
    {
        return date.Date;
    }

    /// <summary>
    /// 获取一天的结束时间（23:59:59.999）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>当天结束时间</returns>
    public static DateTime GetEndOfDay(DateTime date)
    {
        return date.Date.AddDays(1).AddTicks(-1);
    }

    /// <summary>
    /// 获取一周的开始时间（周一 00:00:00）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>本周开始时间</returns>
    public static DateTime GetStartOfWeek(DateTime date)
    {
        var diff = date.DayOfWeek - WeekStartDay;
        if (diff < 0)
        {
            diff += 7;
        }
        return date.AddDays(-diff).Date;
    }

    /// <summary>
    /// 获取一周的结束时间（周日 23:59:59.999）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>本周结束时间</returns>
    public static DateTime GetEndOfWeek(DateTime date)
    {
        return GetStartOfWeek(date).AddDays(7).AddTicks(-1);
    }

    /// <summary>
    /// 获取一个月的开始时间（1号 00:00:00）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>本月开始时间</returns>
    public static DateTime GetStartOfMonth(DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1);
    }

    /// <summary>
    /// 获取一个月的结束时间（月末 23:59:59.999）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>本月结束时间</returns>
    public static DateTime GetEndOfMonth(DateTime date)
    {
        return GetStartOfMonth(date).AddMonths(1).AddTicks(-1);
    }

    /// <summary>
    /// 获取一年的开始时间（1月1日 00:00:00）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>本年开始时间</returns>
    public static DateTime GetStartOfYear(DateTime date)
    {
        return new DateTime(date.Year, 1, 1);
    }

    /// <summary>
    /// 获取一年的结束时间（12月31日 23:59:59.999）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>本年结束时间</returns>
    public static DateTime GetEndOfYear(DateTime date)
    {
        return new DateTime(date.Year, 12, 31, 23, 59, 59, 999);
    }

    #endregion

    #region 时区转换

    /// <summary>
    /// 将本地时间转换为 UTC 时间
    /// </summary>
    /// <param name="localTime">本地时间</param>
    /// <returns>UTC 时间</returns>
    public static DateTime ToUtc(DateTime localTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(localTime);
    }

    /// <summary>
    /// 将 UTC 时间转换为本地时间
    /// </summary>
    /// <param name="utcTime">UTC 时间</param>
    /// <returns>本地时间</returns>
    public static DateTime FromUtc(DateTime utcTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, TimeZoneInfo.Local);
    }

    /// <summary>
    /// 将时间转换到指定时区
    /// </summary>
    /// <param name="dateTime">原始时间</param>
    /// <param name="targetTimeZone">目标时区</param>
    /// <returns>目标时区时间</returns>
    public static DateTime ConvertToTimeZone(DateTime dateTime, TimeZoneInfo targetTimeZone)
    {
        return TimeZoneInfo.ConvertTime(dateTime, targetTimeZone);
    }

    /// <summary>
    /// 将时间转换为中国时区
    /// </summary>
    /// <param name="dateTime">原始时间</param>
    /// <returns>中国时区时间</returns>
    public static DateTime ToChinaTime(DateTime dateTime)
    {
        return TimeZoneInfo.ConvertTime(dateTime, ChinaTimeZone);
    }

    #endregion

    #region 常用格式

    /// <summary>
    /// 格式化为 yyyy-MM-dd 格式
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>格式化字符串</returns>
    public static string ToDateString(DateTime date)
    {
        return date.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// 格式化为 HH:mm:ss 格式
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>格式化字符串</returns>
    public static string ToTimeString(DateTime date)
    {
        return date.ToString("HH:mm:ss");
    }

    /// <summary>
    /// 格式化为 yyyy-MM-dd HH:mm:ss 格式
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>格式化字符串</returns>
    public static string ToDateTimeString(DateTime date)
    {
        return date.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// 格式化为中文日期格式（xxxx年xx月xx日）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>中文格式字符串</returns>
    public static string ToChineseDateString(DateTime date)
    {
        return date.ToString("yyyy年MM月dd日");
    }

    /// <summary>
    /// 格式化为文件名安全的时间格式（yyyy-MM-dd_HH-mm-ss）
    /// </summary>
    /// <param name="date">日期</param>
    /// <returns>文件名安全格式字符串</returns>
    public static string ToFileNameSafeString(DateTime date)
    {
        return date.ToString("yyyy-MM-dd_HH-mm-ss");
    }

    #endregion
}