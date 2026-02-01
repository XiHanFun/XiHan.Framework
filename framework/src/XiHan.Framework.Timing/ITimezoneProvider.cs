#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITimezoneProvider
// Guid:a4a66852-8c00-4c89-8ca1-32ebbcf5be8e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/24 05:25:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
