#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CpuHelper
// Guid:2e1f186b-92ad-4e02-9e15-d373684b181e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/08/27 12:28:33
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Runtime.InteropServices;
using XiHan.Framework.Utils.Caching;
using XiHan.Framework.Utils.CommandLine;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.Diagnostics.HardwareInfos;

/// <summary>
/// 处理器帮助类
/// </summary>
public static class CpuHelper
{
    /// <summary>
    /// 处理器信息
    /// </summary>
    /// <remarks>
    /// 推荐使用，默认有缓存
    /// </remarks>
    public static CpuInfo CpuInfos => CacheHelper.GetOrAdd("CpuInfos", GetCpuInfos, TimeSpan.FromSeconds(5));

    /// <summary>
    /// 获取处理器信息
    /// </summary>
    /// <returns></returns>
    public static CpuInfo GetCpuInfos()
    {
        var cpuInfo = new CpuInfo
        {
            LogicalCoreCount = Environment.ProcessorCount,
            ProcessorArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
        };

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                GetLinuxCpuInfo(cpuInfo);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                GetMacOsCpuInfo(cpuInfo);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GetWindowsCpuInfo(cpuInfo);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error("获取处理器信息出错，" + ex.Message);
        }

        return cpuInfo;
    }

    /// <summary>
    /// 获取Windows CPU信息
    /// </summary>
    /// <param name="cpuInfo"></param>
    private static void GetWindowsCpuInfo(CpuInfo cpuInfo)
    {
        try
        {
            // 获取CPU使用率
            var usageOutput = ShellHelper.Cmd("powershell", @"-Command ""Get-CimInstance -ClassName Win32_Processor | Select-Object LoadPercentage | Format-List""").Trim();
            var usageLines = usageOutput.Split(Environment.NewLine);
            var loadLine = usageLines.FirstOrDefault(s => s.StartsWith("LoadPercentage"));
            if (loadLine != null)
            {
                var loadPercentage = loadLine.Split(':')[1].Trim();
                if (double.TryParse(loadPercentage, out var usage))
                {
                    cpuInfo.UsagePercentage = Math.Round(usage, 2);
                }
            }
        }
        catch
        {
            // 如果WMI失败，使用默认值
            cpuInfo.UsagePercentage = 0;
        }

        // 获取CPU详细信息
        var output = ShellHelper.Cmd("powershell", @"-Command ""Get-CimInstance -ClassName Win32_Processor | Select-Object Name, NumberOfCores, MaxClockSpeed, L3CacheSize | Format-List""").Trim();
        var lines = output.Split(Environment.NewLine);

        foreach (var line in lines)
        {
            if (line.StartsWith("Name"))
            {
                cpuInfo.ProcessorName = line.Split(':', 2)[1].Trim();
            }
            else if (line.StartsWith("NumberOfCores"))
            {
                if (int.TryParse(line.Split(':', 2)[1], out var cores))
                {
                    cpuInfo.PhysicalCoreCount = cores;
                }
            }
            else if (line.StartsWith("MaxClockSpeed"))
            {
                if (double.TryParse(line.Split(':', 2)[1], out var mhz))
                {
                    cpuInfo.BaseClockSpeed = Math.Round(mhz / 1000, 2);
                }
            }
            else if (line.StartsWith("L3CacheSize"))
            {
                var cache = line.Split(':', 2)[1].Trim();
                if (!string.IsNullOrEmpty(cache) && cache != "0")
                {
                    cpuInfo.CacheBytes = cache.ConvertToLong() * 1024;
                }
            }
        }
    }

    /// <summary>
    /// 获取Linux CPU信息
    /// </summary>
    /// <param name="cpuInfo"></param>
    private static void GetLinuxCpuInfo(CpuInfo cpuInfo)
    {
        // 获取CPU使用率
        var output = ShellHelper.Bash(@"top -b -n1 | grep ""Cpu(s)""").Trim();
        var lines = output.Split(',');
        if (lines.Length > 3)
        {
            var loadPercentage = lines[3].Trim().Split(' ')[0].Replace("%", "");
            if (double.TryParse(loadPercentage, out var usage))
            {
                cpuInfo.UsagePercentage = Math.Round(100 - usage, 2);
            }
        }

        // 获取CPU详细信息
        var cpuInfoOutput = ShellHelper.Bash("cat /proc/cpuinfo").Trim();
        var cpuInfoLines = cpuInfoOutput.Split('\n');

        foreach (var line in cpuInfoLines)
        {
            if (line.StartsWith("model name"))
            {
                cpuInfo.ProcessorName = line.Split(':')[1].Trim();
            }
            else if (line.StartsWith("cpu MHz"))
            {
                if (double.TryParse(line.Split(':')[1].Trim(), out var mhz))
                {
                    cpuInfo.BaseClockSpeed = Math.Round(mhz / 1000, 2);
                }
            }
            else if (line.StartsWith("cache size"))
            {
                cpuInfo.CacheBytes = line.Split(':')[1].Trim().ConvertToLong();
            }
            else if (line.StartsWith("cpu cores"))
            {
                if (int.TryParse(line.Split(':')[1].Trim(), out var cores))
                {
                    cpuInfo.PhysicalCoreCount = cores;
                }
            }
        }
    }

    /// <summary>
    /// 获取macOS CPU信息
    /// </summary>
    /// <param name="cpuInfo"></param>
    private static void GetMacOsCpuInfo(CpuInfo cpuInfo)
    {
        // 获取CPU使用率
        var output = ShellHelper.Bash(@"top -l 1 -F | awk '/CPU usage/ {gsub(""%"", """"); print $7}'").Trim();
        if (double.TryParse(output, out var usage))
        {
            cpuInfo.UsagePercentage = Math.Round(100 - usage, 2);
        }

        // 获取CPU详细信息
        var nameOutput = ShellHelper.Bash("sysctl -n machdep.cpu.brand_string").Trim();
        cpuInfo.ProcessorName = nameOutput;

        var coreOutput = ShellHelper.Bash("sysctl -n hw.physicalcpu").Trim();
        if (int.TryParse(coreOutput, out var cores))
        {
            cpuInfo.PhysicalCoreCount = cores;
        }
    }
}

/// <summary>
/// 处理器信息
/// </summary>
public record CpuInfo
{
    /// <summary>
    /// 处理器名称
    /// </summary>
    public string ProcessorName { get; set; } = string.Empty;

    /// <summary>
    /// 处理器架构
    /// </summary>
    public string ProcessorArchitecture { get; set; } = string.Empty;

    /// <summary>
    /// 物理核心数
    /// </summary>
    public int PhysicalCoreCount { get; set; }

    /// <summary>
    /// 逻辑核心数(超线程)
    /// </summary>
    public int LogicalCoreCount { get; set; }

    /// <summary>
    /// 基础时钟频率(GHz)
    /// </summary>
    public double BaseClockSpeed { get; set; }

    /// <summary>
    /// 缓存大小
    /// </summary>
    public long CacheBytes { get; set; }

    /// <summary>
    /// CPU使用率(%)
    /// </summary>
    public double UsagePercentage { get; set; }
}
