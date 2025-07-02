#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RunningTimeHelper
// Guid:722c8f90-08b3-4e0f-be12-c272fe3e6549
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/15 18:59:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Runtime.InteropServices;
using XiHan.Framework.Utils.Caching;
using XiHan.Framework.Utils.CommandLine;
using XiHan.Framework.Utils.Logging;
using XiHan.Framework.Utils.System;
using XiHan.Framework.Utils.Timing;

namespace XiHan.Framework.Utils.Runtime;

/// <summary>
/// 系统运行时间
/// </summary>
public static class RunningTimeHelper
{
    /// <summary>
    /// 系统运行时间
    /// </summary>
    /// <remarks>
    /// 推荐使用，默认有缓存
    /// </remarks>
    public static string RunningTime => CacheManager.Instance.DefaultCache.GetOrAdd("RunningTime", () => GetRunningTime(), TimeSpan.FromMinutes(1));

    /// <summary>
    /// 获取系统运行时间
    /// </summary>
    public static string GetRunningTime()
    {
        var runTime = string.Empty;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var output = ShellHelper.Bash("uptime -s").Trim();
                var timeSpan = DateTime.Now - output.Trim().ParseToDateTime();
                runTime = timeSpan.FormatTimeSpanToString();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var output = ShellHelper.Bash("uptime | tail -n -1").Trim();
                // 提取运行时间部分
                var startIndex = output.IndexOf("up ", StringComparison.Ordinal) + 3;
                var endIndex = output.IndexOf(" user", StringComparison.Ordinal);
                var uptime = output[startIndex..endIndex].Trim();
                // 解析运行时间并转换为标准格式
                var uptimeSpan = ParseUptime(uptime);
                runTime = uptimeSpan.FormatTimeSpanToString();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var output = ShellHelper.Cmd("powershell", @"-Command ""Get-CimInstance -ClassName Win32_OperatingSystem | Select-Object LastBootUpTime | Format-List""").Trim();
                var lines = output.Split(Environment.NewLine);
                var bootTimeLine = lines.FirstOrDefault(s => s.StartsWith("LastBootUpTime"));
                if (bootTimeLine != null)
                {
                    var bootTimeStr = bootTimeLine.Split(':', 2)[1].Trim();
                    if (DateTime.TryParse(bootTimeStr, out var bootTime))
                    {
                        var timeSpan = DateTime.Now - bootTime;
                        runTime = timeSpan.FormatTimeSpanToString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error("获取系统运行时间出错，" + ex.Message);
        }

        return runTime;
    }

    /// <summary>
    /// 解析运行时间
    /// </summary>
    /// <param name="uptime"></param>
    /// <returns></returns>
    private static TimeSpan ParseUptime(string uptime)
    {
        var parts = uptime.Split(',');
        int days = 0, hours = 0, minutes = 0;

        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();

            if (trimmedPart.Contains("day"))
            {
                days = int.Parse(trimmedPart.Split(' ')[0]);
            }
            else if (trimmedPart.Contains(':'))
            {
                var timeParts = trimmedPart.Split(':');
                hours = int.Parse(timeParts[0]);
                minutes = int.Parse(timeParts[1]);
            }
        }

        return new TimeSpan(days, hours, minutes, 0);
    }
}
