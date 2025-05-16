#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TimeZoneHelper
// Guid:9b8c7d6e-5f4e-3b2c-1a9d-8e7f6d5c4b3a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/4/29 0:47:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Globalization;

namespace XiHan.Framework.Utils.Localization;

/// <summary>
/// 时区处理帮助类
/// </summary>
public static class TimeZoneHelper
{
    /// <summary>
    /// 获取所有时区
    /// </summary>
    /// <returns>时区列表</returns>
    public static IEnumerable<TimeZoneInfo> GetAllTimeZones()
    {
        return TimeZoneInfo.GetSystemTimeZones();
    }

    /// <summary>
    /// 获取时区信息
    /// </summary>
    /// <param name="timeZoneId">时区ID</param>
    /// <returns>时区信息</returns>
    public static TimeZoneInfo GetTimeZone(string timeZoneId)
    {
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    /// <summary>
    /// 转换时间到指定时区
    /// </summary>
    /// <param name="dateTime">要转换的时间</param>
    /// <param name="sourceTimeZone">源时区</param>
    /// <param name="targetTimeZone">目标时区</param>
    /// <returns>转换后的时间</returns>
    public static DateTime ConvertTime(DateTime dateTime, TimeZoneInfo sourceTimeZone, TimeZoneInfo targetTimeZone)
    {
        return TimeZoneInfo.ConvertTime(dateTime, sourceTimeZone, targetTimeZone);
    }

    /// <summary>
    /// 转换时间到指定时区
    /// </summary>
    /// <param name="dateTime">要转换的时间</param>
    /// <param name="targetTimeZone">目标时区</param>
    /// <returns>转换后的时间</returns>
    public static DateTime ConvertTime(DateTime dateTime, TimeZoneInfo targetTimeZone)
    {
        return TimeZoneInfo.ConvertTime(dateTime, targetTimeZone);
    }

    /// <summary>
    /// 获取时区偏移量
    /// </summary>
    /// <param name="timeZone">时区</param>
    /// <returns>偏移量</returns>
    public static TimeSpan GetTimeZoneOffset(TimeZoneInfo timeZone)
    {
        return timeZone.GetUtcOffset(DateTime.Now);
    }

    /// <summary>
    /// 获取时区偏移量字符串
    /// </summary>
    /// <param name="timeZone">时区</param>
    /// <returns>偏移量字符串</returns>
    public static string GetTimeZoneOffsetString(TimeZoneInfo timeZone)
    {
        var offset = GetTimeZoneOffset(timeZone);
        return $"{(offset.Hours >= 0 ? "+" : "-")}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00}";
    }

    /// <summary>
    /// 检查时区是否存在
    /// </summary>
    /// <param name="timeZoneId">时区ID</param>
    /// <returns>是否存在</returns>
    public static bool IsTimeZoneExists(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取本地时区
    /// </summary>
    /// <returns>本地时区</returns>
    public static TimeZoneInfo GetLocalTimeZone()
    {
        return TimeZoneInfo.Local;
    }

    /// <summary>
    /// 获取UTC时区
    /// </summary>
    /// <returns>UTC时区</returns>
    public static TimeZoneInfo GetUtcTimeZone()
    {
        return TimeZoneInfo.Utc;
    }

    /// <summary>
    /// 获取时区显示名称
    /// </summary>
    /// <param name="timeZone">时区</param>
    /// <param name="_">文化（未使用）</param>
    /// <returns>显示名称</returns>
    public static string GetTimeZoneDisplayName(TimeZoneInfo timeZone, CultureInfo _)
    {
        return timeZone.DisplayName;
    }
}
