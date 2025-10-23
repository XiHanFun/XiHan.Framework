#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TimezoneProvider
// Guid:b8f5c142-9d3e-4a89-9c2f-51ebbcf6ce9f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 5:25:30
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using TimeZoneConverter;
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

namespace XiHan.Framework.Core.Timing;

/// <summary>
/// 时区提供器
/// </summary>
public class TZConvertTimezoneProvider : ITimezoneProvider, ISingletonDependency
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
