#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2023 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OsPlatformHelper
// Guid:d404f006-9a93-45b2-b33b-8ec201355621
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2023-04-09 上午 06:49:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Runtime.InteropServices;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Utils.Extensions.System;

namespace XiHan.Framework.Utils.System.Runtime;

/// <summary>
/// 操作系统帮助类
/// </summary>
public static class OsPlatformHelper
{
    /// <summary>
    /// 操作系统
    /// </summary>
    public static string OperatingSystem => GetOperatingSystem();

    /// <summary>
    /// 是否 Unix 系统
    /// </summary>
    public static bool OsIsUnix => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                                   RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// 系统描述
    /// </summary>
    public static string OsDescription => RuntimeInformation.OSDescription;

    /// <summary>
    /// 系统版本
    /// </summary>
    public static string OsVersion => Environment.OSVersion.Version.ToString();

    /// <summary>
    /// 系统平台
    /// </summary>
    public static string Platform => Environment.OSVersion.Platform.ToString();

    /// <summary>
    /// 系统架构
    /// </summary>
    public static string OsArchitecture => RuntimeInformation.OSArchitecture.ToString();

    /// <summary>
    /// 系统目录
    /// </summary>
    public static string SystemDirectory => Environment.SystemDirectory;

    /// <summary>
    /// 运行时间
    /// </summary>
    public static string RunningTime => GetRunningTime();

    /// <summary>
    /// 交互模式
    /// </summary>
    public static string InteractiveMode => Environment.UserInteractive ? "交互运行" : "非交互运行";

    /// <summary>
    /// 获取操作系统
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string GetOperatingSystem()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? OSPlatform.OSX.ToString()
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? OSPlatform.Linux.ToString()
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? OSPlatform.Windows.ToString()
                    : throw new Exception("Cannot determine operating system!");
    }

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
                var output = ShellHelper.Cmd("wmic", "OS get LastBootUpTime /Value").Trim();
                var outputArr = output.Split('=');
                if (outputArr.Length != 0)
                {
                    var timeSpan = DateTime.Now - outputArr[1].Split('.')[0].FormatStringToDate();
                    runTime = timeSpan.FormatTimeSpanToString();
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error("获取系统运行时间出错，" + ex.Message);
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