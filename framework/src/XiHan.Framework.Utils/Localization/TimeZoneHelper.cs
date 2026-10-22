// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
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
    /// <param name="timeZoneId">时区唯一标识：IANA ID（含旧别名）或 Windows ID，解析规则见 <see cref="TryGetTimeZone"/></param>
    /// <returns>时区信息，其 ID 可能与传入的标识不同</returns>
    /// <exception cref="ArgumentNullException"><paramref name="timeZoneId"/> 为 null</exception>
    /// <exception cref="TimeZoneNotFoundException">当前系统找不到该时区，也找不到与它等价的时区</exception>
    public static TimeZoneInfo GetTimeZone(string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(timeZoneId);

        return TryGetTimeZone(timeZoneId, out var timeZone)
            ? timeZone
            : throw new TimeZoneNotFoundException($"当前系统找不到时区“{timeZoneId}”，也找不到与它等价的时区。");
    }

    /// <summary>
    /// 尝试获取时区信息
    /// </summary>
    /// <param name="timeZoneId">时区唯一标识：IANA ID（含旧别名）或 Windows ID；前后空白会被忽略</param>
    /// <param name="timeZone">成功时返回当前系统已安装的时区，其 ID 可能与传入的标识不同</param>
    /// <returns>找到该时区或与它等价的时区时返回 true；标识为空或找不到时返回 false</returns>
    /// <remarks>
    /// 依次尝试：按标识直接查找；IANA ID 与 Windows ID 互转后再查找；
    /// 非 Windows 系统上仍找不到时，在系统时区目录里取与它属于同一个 Windows 时区、ID 按序数排在最前的时区。
    /// 最后一步针对新版 tzdata 不再带 Asia/Calcutta、America/Indianapolis 这类旧别名的系统；
    /// 同一个 Windows 时区下的时区现行规则一致，当前时间的换算结果与 Windows 节点相同。
    /// </remarks>
    public static bool TryGetTimeZone(string? timeZoneId, [NotNullWhen(true)] out TimeZoneInfo? timeZone)
    {
        return TimeZoneIdResolver.Shared.TryResolve(timeZoneId, out timeZone);
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
    /// <param name="timeZoneId">时区唯一标识：IANA ID（含旧别名）或 Windows ID，解析规则见 <see cref="TryGetTimeZone"/></param>
    /// <returns>当前系统有该时区或与它等价的时区时返回 true</returns>
    public static bool IsTimeZoneExists(string timeZoneId)
    {
        return TryGetTimeZone(timeZoneId, out _);
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
    /// <param name="_">文化(未使用)</param>
    /// <returns>显示名称</returns>
    public static string GetTimeZoneDisplayName(TimeZoneInfo timeZone, CultureInfo _)
    {
        return timeZone.DisplayName;
    }
}
