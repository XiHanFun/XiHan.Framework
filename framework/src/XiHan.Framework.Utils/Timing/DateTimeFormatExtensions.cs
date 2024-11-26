#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DateTimeFormatExtensions
// Guid:a054878b-e7e1-4460-84fd-4bb856d375ff
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 5:25:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;

namespace XiHan.Framework.Utils.Timing;

/// <summary>
/// DateTime 扩展方法
/// </summary>
public static class DateTimeFormatExtensions
{
    /// <summary>
    /// 获取 Unix 时间戳
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static long GetUnixTimeStamp(this DateTime dateTime)
    {
        return ((DateTimeOffset)dateTime).ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// 获取当前时间的时间戳
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static long GetDateToTimeStamp(this DateTime dateTime)
    {
        var ts = dateTime - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return Convert.ToInt64(ts.TotalSeconds);
    }

    /// <summary>
    /// 获取日期天的最小时间
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime GetDayMinDate(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0);
    }

    /// <summary>
    /// 获取日期天的最大时间
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime GetDayMaxDate(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 23, 59, 59);
    }

    /// <summary>
    /// 获取一天的范围
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static List<DateTime> GetDayDateRange(this DateTime dateTime)
    {
        return
        [
            dateTime.GetDayMinDate(),
            dateTime.GetDayMaxDate()
        ];
    }

    /// <summary>
    /// 获取日期开始时间
    /// </summary>
    /// <param name="dateTime"></param>
    /// <param name="days"></param>
    /// <returns></returns>
    public static DateTime GetBeginTime(this DateTime? dateTime, int days = 0)
    {
        return dateTime == DateTime.MinValue || dateTime == null ? DateTime.Now.AddDays(days) : (DateTime)dateTime;
    }

    /// <summary>
    /// 获取星期几
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static string GetWeekByDate(this DateTime dateTime)
    {
        string[] day = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];
        return day[Convert.ToInt32(dateTime.DayOfWeek.ToString("d"))];
    }

    /// <summary>
    /// 获取这个月的第几周
    /// </summary>
    /// <param name="daytime"></param>
    /// <returns></returns>
    public static int GetWeekNumInMonth(this DateTime daytime)
    {
        var dayInMonth = daytime.Day;
        // 本月第一天
        var firstDay = daytime.AddDays(1 - daytime.Day);
        // 本月第一天是周几
        var weekday = firstDay.DayOfWeek == 0 ? 7 : (int)firstDay.DayOfWeek;
        // 本月第一周有几天
        var firstWeekEndDay = 7 - (weekday - 1);
        // 当前日期和第一周之差
        var diffDay = dayInMonth - firstWeekEndDay;
        diffDay = diffDay > 0 ? diffDay : 1;
        // 当前是第几周，若整除7就减一天
        return (diffDay % 7 == 0 ? diffDay / 7 - 1 : diffDay / 7) + 1 + (dayInMonth > firstWeekEndDay ? 1 : 0);
    }

    /// <summary>
    /// 时间转换字符串
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static string FormatDateTimeToString(this DateTime dateTime)
    {
        return dateTime.ToString(dateTime.Year == DateTime.Now.Year ? "MM-dd HH:mm" : "yyyy-MM-dd HH:mm");
    }

    /// <summary>
    /// 时间转换字符串
    /// </summary>
    /// <param name="dateTimeBefore"></param>
    /// <param name="dateTimeAfter"></param>
    /// <returns></returns>
    public static string FormatDateTimeToString(this DateTime dateTimeBefore, DateTime dateTimeAfter)
    {
        if (dateTimeBefore >= dateTimeAfter)
        {
            throw new Exception("开始日期必须小于结束日期");
        }

        var timeSpan = dateTimeAfter - dateTimeBefore;
        return timeSpan.FormatTimeSpanToString();
    }

    /// <summary>
    /// 毫秒转换字符串
    /// </summary>
    /// <param name="milliseconds"></param>
    /// <returns></returns>
    public static string FormatMilliSecondsToString(this long milliseconds)
    {
        var timeSpan = TimeSpan.FromMilliseconds(milliseconds);
        return timeSpan.FormatTimeSpanToString();
    }

    /// <summary>
    /// 时刻转换字符串
    /// </summary>
    /// <param name="ticks"></param>
    /// <returns></returns>
    public static string FormatTimeTicksToString(this long ticks)
    {
        var timeSpan = TimeSpan.FromTicks(ticks);
        return timeSpan.FormatTimeSpanToString();
    }

    /// <summary>
    /// 毫秒转换字符串
    /// </summary>
    /// <param name="ms"></param>
    /// <returns></returns>
    public static string FormatTimeMilliSecondToString(this long ms)
    {
        const int ss = 1000;
        const int mi = ss * 60;
        const int hh = mi * 60;
        const int dd = hh * 24;

        var day = ms / dd;
        var hour = (ms - day * dd) / hh;
        var minute = (ms - day * dd - hour * hh) / mi;
        var second = (ms - day * dd - hour * hh - minute * mi) / ss;
        var milliSecond = ms - day * dd - hour * hh - minute * mi - second * ss;

        // 天
        var sDay = day < 10 ? "0" + day : string.Empty + day;
        // 小时
        var sHour = hour < 10 ? "0" + hour : string.Empty + hour;
        // 分钟
        var sMinute = minute < 10 ? "0" + minute : string.Empty + minute;
        // 秒
        var sSecond = second < 10 ? "0" + second : string.Empty + second;
        // 毫秒
        var sMilliSecond = milliSecond < 10 ? "0" + milliSecond : string.Empty + milliSecond;
        sMilliSecond = milliSecond < 100 ? "0" + sMilliSecond : string.Empty + sMilliSecond;

        return $"{sDay} 天 {sHour} 小时 {sMinute} 分 {sSecond} 秒 {sMilliSecond} 毫秒";
    }

    /// <summary>
    /// 时间跨度转换字符串
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    public static string FormatTimeSpanToString(this TimeSpan timeSpan)
    {
        var day = timeSpan.Days;
        var hour = timeSpan.Hours;
        var minute = timeSpan.Minutes;
        var second = timeSpan.Seconds;
        var milliSecond = timeSpan.Milliseconds;

        // 天
        var sDay = day < 10 ? "0" + day : string.Empty + day;
        // 小时
        var sHour = hour < 10 ? "0" + hour : string.Empty + hour;
        // 分钟
        var sMinute = minute < 10 ? "0" + minute : string.Empty + minute;
        // 秒
        var sSecond = second < 10 ? "0" + second : string.Empty + second;
        // 毫秒
        var sMilliSecond = milliSecond < 10 ? "0" + milliSecond : string.Empty + milliSecond;
        sMilliSecond = milliSecond < 100 ? "0" + sMilliSecond : string.Empty + sMilliSecond;

        return $"{sDay} 天 {sHour} 小时 {sMinute} 分 {sSecond} 秒 {sMilliSecond} 毫秒";
    }

    /// <summary>
    /// 时间转换简易字符串
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string FormatDateTimeToEasyString(this DateTime value)
    {
        var now = DateTime.Now;
        var strDate = value.ToString("yyyy-MM-dd");
        if (now < value)
        {
            return strDate;
        }

        var dep = now - value;

        return dep.TotalSeconds < 10
            ? "刚刚"
            : dep.TotalSeconds is >= 10 and < 60
            ? (int)dep.TotalSeconds + "秒前"
            : dep.TotalMinutes is >= 1 and < 60
            ? (int)dep.TotalMinutes + "分钟前"
            : dep.TotalHours < 24
            ? (int)dep.TotalHours + "小时前"
            : dep.TotalDays < 7
            ? (int)dep.TotalDays + "天前"
            : dep.TotalDays is >= 7 and < 30
            ? (int)dep.TotalDays / 7 + "周前"
            : dep.TotalDays is >= 30 and < 365 ? (int)dep.TotalDays / 30 + "个月前" : now.Year - value.Year + "年前";
    }

    /// <summary>
    /// 字符串转日期
    /// </summary>
    /// <param name="thisValue"></param>
    /// <returns></returns>
    public static DateTime FormatStringToDate(this string thisValue)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(thisValue))
            {
                return DateTime.MinValue;
            }

            if (thisValue.Contains('-') || thisValue.Contains('/'))
            {
                return DateTime.Parse(thisValue);
            }

            var length = thisValue.Length;
            return length switch
            {
                4 => DateTime.ParseExact(thisValue, "yyyy", CultureInfo.CurrentCulture),
                6 => DateTime.ParseExact(thisValue, "yyyyMM", CultureInfo.CurrentCulture),
                8 => DateTime.ParseExact(thisValue, "yyyyMMdd", CultureInfo.CurrentCulture),
                10 => DateTime.ParseExact(thisValue, "yyyyMMddHH", CultureInfo.CurrentCulture),
                12 => DateTime.ParseExact(thisValue, "yyyyMMddHHmm", CultureInfo.CurrentCulture),
                14 => DateTime.ParseExact(thisValue, "yyyyMMddHHmmss", CultureInfo.CurrentCulture),
                _ => DateTime.ParseExact(thisValue, "yyyyMMddHHmmss", CultureInfo.CurrentCulture)
            };
        }
        catch
        {
            return DateTime.MinValue;
        }
    }
}
