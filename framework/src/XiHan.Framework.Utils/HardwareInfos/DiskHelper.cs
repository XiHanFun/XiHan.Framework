#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DiskHelper
// Guid:4e1014f7-200b-42f3-a1bf-cde1c500054a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-06-03 下午 08:48:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Runtime.InteropServices;
using XiHan.Framework.Utils.Caching;
using XiHan.Framework.Utils.CommandLine;
using XiHan.Framework.Utils.Logging;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Utils.HardwareInfos;

/// <summary>
/// 磁盘帮助类
/// </summary>
public static class DiskHelper
{
    /// <summary>
    /// 磁盘信息
    /// </summary>
    /// <remarks>
    /// 推荐使用，默认有缓存
    /// </remarks>
    public static List<DiskInfo> DiskInfos => CacheManager.Instance.DefaultCache.GetOrAdd("DiskInfos", () => GetDiskInfos(), TimeSpan.FromMinutes(1));

    /// <summary>
    /// 获取磁盘信息
    /// </summary>
    /// <returns></returns>
    public static List<DiskInfo> GetDiskInfos()
    {
        List<DiskInfo> diskInfos = [];

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var output = ShellHelper.Bash(@"df -mT | awk '/^\/dev\/(sd|vd|xvd|nvme|sda|vda|mapper)/ {print $1,$2,$3,$4,$5,$6}'").Trim();
                var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (lines.Count != 0)
                {
                    diskInfos.AddRange(from line in lines
                                       select line.Split(' ', (char)StringSplitOptions.RemoveEmptyEntries)
                                       into rootDisk
                                       where rootDisk.Length >= 6
                                       select new DiskInfo
                                       {
                                           DiskName = rootDisk[0].Trim(),
                                           TypeName = rootDisk[1].Trim(),
                                           TotalSpace = rootDisk[2].ParseToLong() * 1024 * 1024, // MB转换为字节
                                           UsedSpace = rootDisk[3].ParseToLong() * 1024 * 1024,
                                           FreeSpace = rootDisk[4].ParseToLong() * 1024 * 1024,
                                           AvailableRate = rootDisk[2].ParseToLong() == 0
                                               ? 0
                                               : Math.Round((double)rootDisk[4].ParseToLong() / rootDisk[2].ParseToLong() * 100, 3)
                                       });
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var output = ShellHelper.Bash(@"df -k | awk '/^\/dev\/disk/ {print $1,$2,$3,$4,$6}' | tail -n +2").Trim();
                var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (lines.Count != 0)
                {
                    diskInfos.AddRange(from line in lines
                                       select line.Split(' ', (char)StringSplitOptions.RemoveEmptyEntries)
                                       into rootDisk
                                       where rootDisk.Length >= 5
                                       select new DiskInfo
                                       {
                                           TypeName = rootDisk[0].Trim(),
                                           TotalSpace = rootDisk[1].ParseToLong() * 1024,
                                           UsedSpace = rootDisk[2].ParseToLong() * 1024,
                                           DiskName = rootDisk[4].Trim(),
                                           FreeSpace = (rootDisk[1].ParseToLong() - rootDisk[2].ParseToLong()) * 1024,
                                           AvailableRate = rootDisk[1].ParseToLong() == 0
                                               ? 0
                                               : Math.Round((double)rootDisk[3].ParseToLong() / rootDisk[1].ParseToLong() * 100, 3)
                                       });
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                diskInfos.AddRange(drives.Select(item => new DiskInfo
                {
                    DiskName = item.Name,
                    TypeName = item.DriveType.ToString(),
                    TotalSpace = item.TotalSize,
                    FreeSpace = item.TotalFreeSpace,
                    UsedSpace = item.TotalSize - item.TotalFreeSpace,
                    AvailableRate = item.TotalSize == 0
                        ? 0
                        : Math.Round((double)item.TotalFreeSpace / item.TotalSize * 100, 3)
                }));
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error("获取磁盘信息出错，" + ex.Message);
        }

        return diskInfos;
    }
}

/// <summary>
/// 磁盘信息
/// </summary>
public record DiskInfo
{
    /// <summary>
    /// 磁盘名称
    /// </summary>
    public string DiskName { get; set; } = string.Empty;

    /// <summary>
    /// 磁盘类型
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// 总大小
    /// </summary>
    public long TotalSpace { get; set; }

    /// <summary>
    /// 空闲大小
    /// </summary>
    public long FreeSpace { get; set; }

    /// <summary>
    /// 已用大小
    /// </summary>
    public long UsedSpace { get; set; }

    /// <summary>
    /// 可用占比
    /// </summary>
    public double AvailableRate { get; set; }
}
