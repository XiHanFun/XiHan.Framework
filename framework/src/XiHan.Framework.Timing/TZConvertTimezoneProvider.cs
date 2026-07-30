// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using TimeZoneConverter;
using XiHan.Framework.Core;

namespace XiHan.Framework.Timing;

/// <summary>
/// 时区提供器
/// </summary>
public class TZConvertTimezoneProvider : ITimezoneProvider
{
    /// <summary>
    /// 获取 Windows 时区
    /// </summary>
    /// <returns>Windows 时区列表</returns>
    public virtual List<NameValue> GetWindowsTimezones()
    {
        return [.. TZConvert.KnownWindowsTimeZoneIds.OrderBy(x => x).Select(x => new NameValue(x, x))];
    }

    /// <summary>
    /// 获取 IANA 时区
    /// </summary>
    /// <returns>IANA 时区列表</returns>
    public virtual List<NameValue> GetIanaTimezones()
    {
        return [.. TZConvert.KnownIanaTimeZoneNames.OrderBy(x => x)
            .Where(x => (x.Contains('/') && !x.Contains("Etc")) || x == "UTC")
            .Select(x => new NameValue(x, x))];
    }

    /// <summary>
    /// 将 Windows 时区转换为 IANA 时区
    /// </summary>
    /// <param name="windowsTimeZoneId">Windows 时区</param>
    /// <returns></returns>
    public virtual string WindowsToIana(string windowsTimeZoneId)
    {
        return TZConvert.WindowsToIana(windowsTimeZoneId);
    }

    /// <summary>
    /// 将 IANA 时区转换为 Windows 时区
    /// </summary>
    /// <param name="ianaTimeZoneName">IANA 时区</param>
    /// <returns></returns>
    public virtual string IanaToWindows(string ianaTimeZoneName)
    {
        return TZConvert.IanaToWindows(ianaTimeZoneName);
    }

    /// <summary>
    /// 获取时区信息
    /// </summary>
    /// <param name="windowsOrIanaTimeZoneId">Windows 或 IANA 时区</param>
    /// <returns></returns>
    public virtual TimeZoneInfo GetTimeZoneInfo(string windowsOrIanaTimeZoneId)
    {
        return TZConvert.GetTimeZoneInfo(windowsOrIanaTimeZoneId);
    }
}
