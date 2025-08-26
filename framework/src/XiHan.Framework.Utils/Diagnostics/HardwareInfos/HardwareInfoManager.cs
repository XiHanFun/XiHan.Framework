#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HardwareInfoManager
// Guid:d4e5f6a7-b8c9-0123-d4e5-f6a7b8c90123
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreatedTime:2025-01-01 上午 12:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.IO;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.Diagnostics.HardwareInfos;

/// <summary>
/// 硬件信息管理器
/// </summary>
public static class HardwareInfoManager
{
    /// <summary>
    /// 获取完整的系统硬件信息
    /// </summary>
    /// <remarks>
    /// 推荐使用，默认有缓存
    /// </remarks>
    /// <returns>系统硬件信息</returns>
    public static SystemHardwareInfo GetSystemHardwareInfo()
    {
        return new SystemHardwareInfo
        {
            CpuInfo = CpuHelper.CpuInfos,
            RamInfo = RamHelper.RamInfos,
            DiskInfos = DiskHelper.DiskInfos,
            NetworkInfos = NetworkHelper.NetworkInfos,
            GpuInfos = GpuHelper.GpuInfos,
            BoardInfo = BoardHelper.BoardInfos
        };
    }

    /// <summary>
    /// 获取系统硬件摘要信息
    /// </summary>
    /// <returns>系统硬件摘要</returns>
    public static SystemHardwareSummary GetSystemHardwareSummary()
    {
        try
        {
            var cpuInfo = CpuHelper.CpuInfos;
            var ramInfo = RamHelper.RamInfos;
            var diskInfos = DiskHelper.DiskInfos;
            var networkInfos = NetworkHelper.NetworkInfos;
            var gpuInfos = GpuHelper.GpuInfos;

            return new SystemHardwareSummary
            {
                CpuName = cpuInfo.ProcessorName,
                CpuCores = $"{cpuInfo.PhysicalCoreCount}C/{cpuInfo.LogicalCoreCount}T",
                CpuUsage = $"{cpuInfo.UsagePercentage:F1}%",
                TotalMemory = ramInfo.TotalBytes.FormatFileSizeToString(),
                MemoryUsage = $"{ramInfo.UsagePercentage:F1}%",
                TotalDiskSpace = diskInfos.Sum(d => d.TotalSpace).FormatFileSizeToString(),
                NetworkInterfaceCount = networkInfos.Count,
                GpuCount = gpuInfos.Count,
            };
        }
        catch (Exception ex)
        {
            LogHelper.Error($"获取系统硬件摘要失败: {ex.Message}");
            return new SystemHardwareSummary();
        }
    }
}

/// <summary>
/// 系统硬件信息
/// </summary>
public record SystemHardwareInfo
{
    /// <summary>
    /// CPU信息
    /// </summary>
    public CpuInfo CpuInfo { get; set; } = new();

    /// <summary>
    /// 内存信息
    /// </summary>
    public RamInfo RamInfo { get; set; } = new();

    /// <summary>
    /// 磁盘信息列表
    /// </summary>
    public List<DiskInfo> DiskInfos { get; set; } = [];

    /// <summary>
    /// 网络接口信息列表
    /// </summary>
    public List<NetworkInfo> NetworkInfos { get; set; } = [];

    /// <summary>
    /// GPU信息列表
    /// </summary>
    public List<GpuInfo> GpuInfos { get; set; } = [];

    /// <summary>
    /// 主板信息
    /// </summary>
    public BoardInfo BoardInfo { get; set; } = new();
}

/// <summary>
/// 系统硬件摘要信息
/// </summary>
public record SystemHardwareSummary
{
    /// <summary>
    /// CPU名称
    /// </summary>
    public string CpuName { get; set; } = string.Empty;

    /// <summary>
    /// CPU核心数
    /// </summary>
    public string CpuCores { get; set; } = string.Empty;

    /// <summary>
    /// CPU使用率
    /// </summary>
    public string CpuUsage { get; set; } = string.Empty;

    /// <summary>
    /// 总内存
    /// </summary>
    public string TotalMemory { get; set; } = string.Empty;

    /// <summary>
    /// 内存使用率
    /// </summary>
    public string MemoryUsage { get; set; } = string.Empty;

    /// <summary>
    /// 总磁盘空间
    /// </summary>
    public string TotalDiskSpace { get; set; } = string.Empty;

    /// <summary>
    /// 网络接口数量
    /// </summary>
    public int NetworkInterfaceCount { get; set; }

    /// <summary>
    /// GPU数量
    /// </summary>
    public int GpuCount { get; set; }

    /// <summary>
    /// 主要GPU
    /// </summary>
    public string PrimaryGpu { get; set; } = string.Empty;
}
