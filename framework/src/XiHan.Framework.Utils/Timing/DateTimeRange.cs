#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DateTimeRange
// Guid:7095cfc3-fe7c-4c7e-9077-67a0782bf41b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 5:38:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Timing;

/// <summary>
/// 表示一个时间范围
/// </summary>
public class DateTimeRange
{
    /// <summary>
    /// 初始化一个<see cref="DateTimeRange"/>类型的新实例
    /// </summary>
    public DateTimeRange() : this(DateTime.MinValue, DateTime.MaxValue)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="DateTimeRange"/>类型的新实例
    /// </summary>
    public DateTimeRange(DateTime startTime, DateTime endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
    }

    /// <summary>
    /// 获取昨天的时间范围
    /// </summary>
    public static DateTimeRange Yesterday
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(now.Date.AddDays(-1), now.Date.AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取今天的时间范围
    /// </summary>
    public static DateTimeRange Today
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(now.Date.Date, now.Date.AddDays(1).AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取明天的时间范围
    /// </summary>
    public static DateTimeRange Tomorrow
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(now.Date.AddDays(1), now.Date.AddDays(2).AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取上周的时间范围
    /// </summary>
    public static DateTimeRange LastWeek
    {
        get
        {
            var now = DateTime.Now;
            DayOfWeek[] weeks =
            [
                DayOfWeek.Sunday,
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday
            ];
            var index = Array.IndexOf(weeks, now.DayOfWeek);
            return new DateTimeRange(now.Date.AddDays(-index - 7), now.Date.AddDays(-index).AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取本周的时间范围
    /// </summary>
    public static DateTimeRange ThisWeek
    {
        get
        {
            var now = DateTime.Now;
            DayOfWeek[] weeks =
            [
                DayOfWeek.Sunday,
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday
            ];
            var index = Array.IndexOf(weeks, now.DayOfWeek);
            return new DateTimeRange(now.Date.AddDays(-index), now.Date.AddDays(7 - index).AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取下周的时间范围
    /// </summary>
    public static DateTimeRange NextWeek
    {
        get
        {
            var now = DateTime.Now;
            DayOfWeek[] weeks =
            [
                DayOfWeek.Sunday,
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday,
                DayOfWeek.Saturday
            ];
            var index = Array.IndexOf(weeks, now.DayOfWeek);
            return new DateTimeRange(now.Date.AddDays(-index + 7), now.Date.AddDays(14 - index).AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取上个月的时间范围
    /// </summary>
    public static DateTimeRange LastMonth
    {
        get
        {
            var now = DateTime.Now;
            var startTime = now.Date.AddDays(-now.Day + 1).AddMonths(-1);
            var endTime = startTime.AddMonths(1).AddMilliseconds(-1);
            return new DateTimeRange(startTime, endTime);
        }
    }

    /// <summary>
    /// 获取本月的时间范围
    /// </summary>
    public static DateTimeRange ThisMonth
    {
        get
        {
            var now = DateTime.Now;
            var startTime = now.Date.AddDays(-now.Day + 1);
            var endTime = startTime.AddMonths(1).AddMilliseconds(-1);
            return new DateTimeRange(startTime, endTime);
        }
    }

    /// <summary>
    /// 获取下个月的时间范围
    /// </summary>
    public static DateTimeRange NextMonth
    {
        get
        {
            var now = DateTime.Now;
            var startTime = now.Date.AddDays(-now.Day + 1).AddMonths(1);
            var endTime = startTime.AddMonths(1).AddMilliseconds(-1);
            return new DateTimeRange(startTime, endTime);
        }
    }

    /// <summary>
    /// 获取上一年的时间范围
    /// </summary>
    public static DateTimeRange LastYear
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(new DateTime(now.Year - 1, 1, 1), new DateTime(now.Year, 1, 1).AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取本年的时间范围
    /// </summary>
    public static DateTimeRange ThisYear
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(new DateTime(now.Year, 1, 1), new DateTime(now.Year + 1, 1, 1).AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取下一年的时间范围
    /// </summary>
    public static DateTimeRange NextYear
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(new DateTime(now.Year + 1, 1, 1), new DateTime(now.Year + 2, 1, 1).AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取相对当前时间过去30天的时间范围
    /// </summary>
    public static DateTimeRange Last30Days
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(now.AddDays(-30), now);
        }
    }

    /// <summary>
    /// 获取截止到昨天的最近30天的天数范围
    /// </summary>
    public static DateTimeRange Last30DaysExceptToday
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(now.Date.AddDays(-30), now.Date.AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 获取相对当前时间过去7天的时间范围
    /// </summary>
    public static DateTimeRange Last7Days
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(now.AddDays(-7), now);
        }
    }

    /// <summary>
    /// 获取截止到昨天的最近7天的天数范围
    /// </summary>
    public static DateTimeRange Last7DaysExceptToday
    {
        get
        {
            var now = DateTime.Now;
            return new DateTimeRange(now.Date.AddDays(-7), now.Date.AddMilliseconds(-1));
        }
    }

    /// <summary>
    /// 起始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// ToString
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"[{StartTime} - {EndTime}]";
    }
}
