// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core;

namespace XiHan.Framework.Timing;

/// <summary>
/// 时区提供器接口
/// </summary>
public interface ITimezoneProvider
{
    /// <summary>
    /// 获取 Windows 时区
    /// </summary>
    /// <returns>Windows 时区列表</returns>
    List<NameValue> GetWindowsTimezones();

    /// <summary>
    /// 获取 IANA 时区
    /// </summary>
    /// <returns>IANA 时区列表</returns>
    List<NameValue> GetIanaTimezones();

    /// <summary>
    /// 将 Windows 时区转换为 IANA 时区
    /// </summary>
    /// <param name="windowsTimeZoneId">Windows 时区</param>
    /// <returns>IANA 时区</returns>
    string WindowsToIana(string windowsTimeZoneId);

    /// <summary>
    /// 将 IANA 时区转换为 Windows 时区
    /// </summary>
    /// <param name="ianaTimeZoneName">IANA 时区</param>
    /// <returns>Windows 时区</returns>
    string IanaToWindows(string ianaTimeZoneName);

    /// <summary>
    /// 获取时区信息
    /// </summary>
    /// <param name="windowsOrIanaTimeZoneId">Windows 或 IANA 时区</param>
    /// <returns>时区信息</returns>
    TimeZoneInfo GetTimeZoneInfo(string windowsOrIanaTimeZoneId);
}
