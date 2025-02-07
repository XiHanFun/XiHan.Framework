#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CpuHelper
// Guid:2e1f186b-92ad-4e02-9e15-d373684b181e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2023-04-09 上午 06:41:41
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Runtime.InteropServices;
using XiHan.Framework.Utils.CommandLine;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Utils.HardwareInfos;

/// <summary>
/// 处理器帮助类
/// </summary>
public static class CpuHelper
{
    /// <summary>
    /// 处理器信息
    /// </summary>
    public static CpuInfo CpuInfos => GetCpuInfos();

    /// <summary>
    /// 获取处理器信息
    /// </summary>
    /// <returns></returns>
    public static CpuInfo GetCpuInfos()
    {
        CpuInfo cpuInfo = new()
        {
            CpuCount = Environment.ProcessorCount.ToString(),
            CpuRate = "0%"
        };

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var output = ShellHelper.Bash(@"top -b -n1 | grep ""Cpu(s)""").Trim();
                var lines = output.Split(',');
                if (lines.Length != 0)
                {
                    var loadPercentage = lines[3].Trim().Split(' ')[0];
                    cpuInfo.CpuRate = loadPercentage.ParseToLong() + "%";
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var output = ShellHelper.Bash(@"top -l 1 -F | awk '/CPU usage/ {gsub(""%"", """"); print $7}'").Trim();
                cpuInfo.CpuRate = output.ParseToLong() + "%";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var output = ShellHelper.Cmd("wmic", "cpu get LoadPercentage /Value").Trim();
                var lines = output.Split(Environment.NewLine);
                if (lines.Length != 0)
                {
                    var loadPercentage = lines.First(s => s.StartsWith("LoadPercentage")).Split('=')[1];
                    cpuInfo.CpuRate = loadPercentage.ParseToLong() + "%";
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error("获取处理器信息出错，" + ex.Message);
        }

        return cpuInfo;
    }
}

/// <summary>
/// 处理器信息
/// </summary>
public record CpuInfo
{
    /// <summary>
    /// 处理器个数
    /// </summary>
    public string CpuCount { get; set; } = string.Empty;

    /// <summary>
    /// 处理器使用占比
    /// </summary>
    public string CpuRate { get; set; } = string.Empty;
}
