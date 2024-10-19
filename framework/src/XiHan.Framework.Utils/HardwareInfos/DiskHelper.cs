#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2022 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DiskHelper
// Guid:4e1014f7-200b-42f3-a1bf-cde1c500054a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2022-06-03 下午 08:48:25
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Runtime.InteropServices;
using XiHan.Framework.Utils.Diagnostics;
using XiHan.Framework.Utils.Extensions.System;
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
    public static List<DiskInfo> DiskInfos => GetDiskInfos();

    /// <summary>
    /// 获取磁盘信息
    /// </summary>
    /// <returns></returns>
    public static List<DiskInfo> GetDiskInfos()
    {
        List<DiskInfo> diskInfos = [];

        try
        {
            if (OsPlatformHelper.OsIsUnix)
            {
                var output = ShellHelper.Bash("df -k | awk '{print $1,$2,$3,$4,$6}' | tail -n +2").Trim();
                var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
                if (lines.Count != 0)
                    diskInfos.AddRange(from line in lines
                                       select line.Split(' ', (char)StringSplitOptions.RemoveEmptyEntries)
                        into rootDisk
                                       where rootDisk.Length >= 5
                                       select new DiskInfo
                                       {
                                           DiskName = rootDisk[4].Trim(),
                                           TypeName = rootDisk[0].Trim(),
                                           TotalSpace = (rootDisk[1].ParseToLong() * 1024).FormatFileSizeToString(),
                                           UsedSpace = (rootDisk[2].ParseToLong() * 1024).FormatFileSizeToString(),
                                           FreeSpace = ((rootDisk[1].ParseToLong() - rootDisk[2].ParseToLong()) * 1024).FormatFileSizeToString(),
                                           AvailableRate = rootDisk[1].ParseToLong() == 0
                                               ? "0%"
                                               : Math.Round((decimal)rootDisk[3].ParseToLong() / rootDisk[1].ParseToLong() * 100, 3) + "%"
                                       });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                diskInfos.AddRange(drives.Select(item => new DiskInfo
                {
                    DiskName = item.Name,
                    TypeName = item.DriveType.ToString(),
                    TotalSpace = GetHardDiskTotalSpace(item.Name).FormatFileSizeToString(),
                    FreeSpace = GetHardDiskFreeSpace(item.Name).FormatFileSizeToString(),
                    UsedSpace = (GetHardDiskTotalSpace(item.Name) - GetHardDiskFreeSpace(item.Name)).FormatFileSizeToString(),
                    AvailableRate = ProportionOfHardDiskFreeSpace(item.Name)
                }));
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error("获取处理器信息出错，" + ex.Message);
        }

        return diskInfos;
    }

    /// <summary>
    /// 指定驱动器剩余空间大小与总空间大小占比
    /// </summary>
    /// <param name="hardDiskName"></param>
    /// <returns></returns>
    public static string ProportionOfHardDiskFreeSpace(string hardDiskName)
    {
        return GetHardDiskTotalSpace(hardDiskName) == 0
            ? "0%"
            : Math.Round((decimal)GetHardDiskFreeSpace(hardDiskName) / GetHardDiskTotalSpace(hardDiskName) * 100, 3) + "%";
    }

    /// <summary>
    /// 获取指定驱动器剩余空间大小
    /// </summary>
    /// <param name="hardDiskName"></param>
    /// <returns></returns>
    public static long GetHardDiskFreeSpace(string hardDiskName)
    {
        return new DriveInfo(hardDiskName).TotalFreeSpace;
    }

    /// <summary>
    /// 获取指定驱动器总空间大小
    /// </summary>
    /// <param name="hardDiskName"></param>
    /// <returns></returns>
    public static long GetHardDiskTotalSpace(string hardDiskName)
    {
        return new DriveInfo(hardDiskName).TotalSize;
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
    public string TotalSpace { get; set; } = string.Empty;

    /// <summary>
    /// 空闲大小
    /// </summary>
    public string FreeSpace { get; set; } = string.Empty;

    /// <summary>
    /// 已用大小
    /// </summary>
    public string UsedSpace { get; set; } = string.Empty;

    /// <summary>
    /// 可用占比
    /// </summary>
    public string AvailableRate { get; set; } = string.Empty;
}