#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RamHelper
// Guid:93baae04-c99a-4095-b5ab-9f14e2a64c97
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2023-04-09 上午 06:09:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Runtime.InteropServices;
using XiHan.Framework.Utils.Caching;
using XiHan.Framework.Utils.CommandLine;
using XiHan.Framework.Utils.Logging;
using XiHan.Framework.Utils.System;
using XiHan.Framework.Utils.Verifications;

namespace XiHan.Framework.Utils.HardwareInfos;

/// <summary>
/// 内存帮助类
/// </summary>
public static class RamHelper
{
    /// <summary>
    /// 内存信息
    /// </summary>
    /// <remarks>
    /// 推荐使用，默认有缓存
    /// </remarks>
    public static RamInfo RamInfos => CacheManager.Instance.DefaultCache.GetOrAdd("RamInfos", () => GetRamInfos(), TimeSpan.FromSeconds(5));

    /// <summary>
    /// 获取内存信息
    /// </summary>
    /// <returns></returns>
    public static RamInfo GetRamInfos()
    {
        var ramInfo = new RamInfo();

        try
        {
            // 单位是 Byte
            var totalMemoryParts = 0L;
            var usedMemoryParts = 0L;
            var freeMemoryParts = 0L;
            var availableMemoryParts = 0L;
            var buffersCached = 0L;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // 使用更详细的内存信息获取
                var output = ShellHelper.Bash("cat /proc/meminfo").Trim();
                var lines = output.Split('\n');

                foreach (var line in lines)
                {
                    var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var key = parts[0].Trim();
                        var valueStr = parts[1].Trim().Replace(" kB", "");
                        if (long.TryParse(valueStr, out var value))
                        {
                            var bytes = value * 1024;
                            switch (key)
                            {
                                case "MemTotal":
                                    totalMemoryParts = bytes;
                                    break;

                                case "MemFree":
                                    freeMemoryParts = bytes;
                                    break;

                                case "MemAvailable":
                                    availableMemoryParts = bytes;
                                    break;

                                case "Buffers":
                                    buffersCached += bytes;
                                    break;

                                case "Cached":
                                    buffersCached += bytes;
                                    break;
                            }
                        }
                    }
                }

                usedMemoryParts = totalMemoryParts - freeMemoryParts - buffersCached;
                if (availableMemoryParts == 0)
                {
                    availableMemoryParts = freeMemoryParts;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // 获取总内存
                var totalOutput = ShellHelper.Bash("sysctl -n hw.memsize").Trim();
                if (long.TryParse(totalOutput, out totalMemoryParts))
                {
                    // 获取内存压力信息
                    var vmStatOutput = ShellHelper.Bash("vm_stat").Trim();
                    var lines = vmStatOutput.Split('\n');

                    long pageSize = 4096; // 默认页面大小
                    var pageSizeOutput = ShellHelper.Bash("sysctl -n hw.pagesize").Trim();
                    if (long.TryParse(pageSizeOutput, out var ps))
                    {
                        pageSize = ps;
                    }

                    long freePages = 0, wiredPages = 0, activePages = 0, inactivePages = 0;

                    foreach (var line in lines)
                    {
                        if (line.Contains("Pages free:"))
                        {
                            var match = RegexHelper.OneOrMoreNumbersRegex().Match(line);
                            if (match.Success && long.TryParse(match.Value, out freePages)) { }
                        }
                        else if (line.Contains("Pages wired down:"))
                        {
                            var match = RegexHelper.OneOrMoreNumbersRegex().Match(line);
                            if (match.Success && long.TryParse(match.Value, out wiredPages)) { }
                        }
                        else if (line.Contains("Pages active:"))
                        {
                            var match = RegexHelper.OneOrMoreNumbersRegex().Match(line);
                            if (match.Success && long.TryParse(match.Value, out activePages)) { }
                        }
                        else if (line.Contains("Pages inactive:"))
                        {
                            var match = RegexHelper.OneOrMoreNumbersRegex().Match(line);
                            if (match.Success && long.TryParse(match.Value, out inactivePages)) { }
                        }
                    }

                    freeMemoryParts = freePages * pageSize;
                    usedMemoryParts = (wiredPages + activePages) * pageSize;
                    availableMemoryParts = (freePages + inactivePages) * pageSize;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var output = ShellHelper.Cmd("powershell", @"-Command ""Get-CimInstance -ClassName Win32_OperatingSystem | Select-Object FreePhysicalMemory, TotalVisibleMemorySize | Format-List""").Trim();
                var lines = output.Split(Environment.NewLine);
                if (lines.Length != 0)
                {
                    totalMemoryParts = lines.First(s => s.StartsWith("TotalVisibleMemorySize")).Split(':', 2)[1].ParseToLong() * 1024;
                    freeMemoryParts = lines.First(s => s.StartsWith("FreePhysicalMemory")).Split(':', 2)[1].ParseToLong() * 1024;
                    usedMemoryParts = totalMemoryParts - freeMemoryParts;
                    availableMemoryParts = freeMemoryParts;
                }
            }

            // 设置内存信息
            ramInfo.TotalBytes = totalMemoryParts;
            ramInfo.UsedBytes = usedMemoryParts;
            ramInfo.FreeBytes = freeMemoryParts;
            ramInfo.AvailableBytes = availableMemoryParts;
            ramInfo.BuffersCachedBytes = buffersCached;

            ramInfo.UsagePercentage = totalMemoryParts > 0
                ? Math.Round((double)usedMemoryParts / totalMemoryParts * 100, 2)
                : 0;

            ramInfo.AvailablePercentage = totalMemoryParts > 0
                ? Math.Round((double)availableMemoryParts / totalMemoryParts * 100, 2)
                : 0;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error("获取内存信息出错，" + ex.Message);
        }

        return ramInfo;
    }
}

/// <summary>
/// 内存信息
/// </summary>
public record RamInfo
{
    /// <summary>
    /// 总内存大小（字节）
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// 已用内存大小（字节）
    /// </summary>
    public long UsedBytes { get; set; }

    /// <summary>
    /// 空闲内存大小（字节）
    /// </summary>
    public long FreeBytes { get; set; }

    /// <summary>
    /// 可用内存大小（字节）
    /// </summary>
    public long AvailableBytes { get; set; }

    /// <summary>
    /// 缓冲区和缓存大小（字节）
    /// </summary>
    public long BuffersCachedBytes { get; set; }

    /// <summary>
    /// 内存使用率（%）
    /// </summary>
    public double UsagePercentage { get; set; }

    /// <summary>
    /// 可用内存占比（%）
    /// </summary>
    public double AvailablePercentage { get; set; }
}
