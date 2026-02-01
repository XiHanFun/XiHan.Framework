#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SystemInfoManager
// Guid:d4e5f6a7-b8c9-0123-d4e5-f6a7b8c90123
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/11 13:47:16
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Diagnostics.HardwareInfos;
using XiHan.Framework.Utils.Runtime;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 系统信息管理器
/// </summary>
public static class SystemInfoManager
{
    /// <summary>
    /// 获取完整的系统硬件信息
    /// </summary>
    /// <remarks>
    /// 推荐使用，默认有缓存
    /// </remarks>
    /// <returns>系统硬件信息</returns>
    public static SystemInfo GetSystemInfo()
    {
        return new SystemInfo
        {
            CpuInfo = CpuHelper.CpuInfos,
            RamInfo = RamHelper.RamInfos,
            DiskInfos = DiskHelper.DiskInfos,
            NetworkInfos = NetworkHelper.NetworkInfos,
            GpuInfos = GpuHelper.GpuInfos,
            BoardInfo = BoardHelper.BoardInfos,
            RuntimeInfo = OsPlatformHelper.RuntimeInfos
        };
    }
}

/// <summary>
/// 系统运行信息
/// </summary>
public record SystemInfo
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

    /// <summary>
    /// 运行时信息
    /// </summary>
    public RuntimeInfo RuntimeInfo { get; set; } = new();
}
