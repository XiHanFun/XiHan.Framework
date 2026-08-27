// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core;

namespace XiHan.Framework.Timing.Tests;

/// <summary>
/// 时区提供器的手写替身
/// </summary>
/// <remarks>
/// 固定返回一个 UTC+08:00 的自定义时区，并记录时区解析次数。
/// 自定义时区不依赖运行机器的操作系统时区库，因此偏移量断言在任何平台上都是确定的；
/// 调用次数则用来证明时钟在短路分支上压根没有去解析时区。
/// </remarks>
public sealed class RecordingTimezoneProvider : ITimezoneProvider
{
    /// <summary>
    /// 固定的 UTC+08:00 自定义时区标识
    /// </summary>
    public const string PlusEightTimeZoneId = "XiHan/Plus08";

    /// <summary>
    /// 固定的 UTC+08:00 自定义时区
    /// </summary>
    public static readonly TimeZoneInfo PlusEightTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        PlusEightTimeZoneId,
        TimeSpan.FromHours(8),
        "XiHan Plus 08",
        "XiHan Plus 08");

    /// <summary>
    /// 时区解析被调用的次数
    /// </summary>
    public int GetTimeZoneInfoCallCount { get; private set; }

    /// <summary>
    /// 最近一次被请求解析的时区标识
    /// </summary>
    public string? LastRequestedTimeZoneId { get; private set; }

    /// <summary>
    /// 获取 Windows 时区
    /// </summary>
    /// <returns>Windows 时区列表</returns>
    public List<NameValue> GetWindowsTimezones()
    {
        return [];
    }

    /// <summary>
    /// 获取 IANA 时区
    /// </summary>
    /// <returns>IANA 时区列表</returns>
    public List<NameValue> GetIanaTimezones()
    {
        return [];
    }

    /// <summary>
    /// 将 Windows 时区转换为 IANA 时区
    /// </summary>
    /// <param name="windowsTimeZoneId">Windows 时区</param>
    /// <returns>IANA 时区</returns>
    public string WindowsToIana(string windowsTimeZoneId)
    {
        return windowsTimeZoneId;
    }

    /// <summary>
    /// 将 IANA 时区转换为 Windows 时区
    /// </summary>
    /// <param name="ianaTimeZoneName">IANA 时区</param>
    /// <returns>Windows 时区</returns>
    public string IanaToWindows(string ianaTimeZoneName)
    {
        return ianaTimeZoneName;
    }

    /// <summary>
    /// 获取时区信息
    /// </summary>
    /// <param name="windowsOrIanaTimeZoneId">Windows 或 IANA 时区</param>
    /// <returns>时区信息</returns>
    public TimeZoneInfo GetTimeZoneInfo(string windowsOrIanaTimeZoneId)
    {
        GetTimeZoneInfoCallCount++;
        LastRequestedTimeZoneId = windowsOrIanaTimeZoneId;
        return PlusEightTimeZone;
    }
}
