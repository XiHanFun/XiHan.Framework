#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanUpgradeOptions
// Guid:ac1bb8d4-66a7-4e19-9c58-0df9b0d5f4b1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 16:24:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Upgrade.Options;

/// <summary>
/// 曦寒升级选项
/// </summary>
public class XiHanUpgradeOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Upgrade";

    /// <summary>
    /// 最小支持版本（低于该版本强制升级）
    /// </summary>
    public string MinSupportVersion { get; set; } = "0.0.0";

    /// <summary>
    /// 当前应用版本（为空时从入口程序集获取）
    /// </summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// 迁移脚本根目录（相对路径将基于应用根目录）
    /// </summary>
    public string MigrationsRootPath { get; set; } = "migrations";

    /// <summary>
    /// 分布式锁资源键
    /// </summary>
    public string LockResourceKey { get; set; } = "SystemUpgrade";

    /// <summary>
    /// 分布式锁过期时间（秒）
    /// </summary>
    public int LockExpirySeconds { get; set; } = 600;

    /// <summary>
    /// 启动时自动检查升级
    /// </summary>
    public bool EnableAutoCheckOnStartup { get; set; } = true;

    /// <summary>
    /// 当前节点名称（为空时使用机器名）
    /// </summary>
    public string? NodeName { get; set; }

    /// <summary>
    /// 主节点名称（配置后仅主节点可执行升级）
    /// </summary>
    public string? PrimaryNodeName { get; set; }

    /// <summary>
    /// 是否启用多租户隔离升级（可选）
    /// </summary>
    public bool EnableMultiTenantIsolation { get; set; } = false;

    /// <summary>
    /// 升级使用的数据库配置Id（为空则使用默认连接）
    /// </summary>
    public string? ConnectionConfigId { get; set; }

    /// <summary>
    /// 是否启用维护模式
    /// </summary>
    public bool EnableMaintenanceMode { get; set; } = true;

    /// <summary>
    /// 是否启用程序文件替换
    /// </summary>
    public bool EnableFileUpdate { get; set; } = false;

    /// <summary>
    /// 是否启用滚动重启
    /// </summary>
    public bool EnableRollingRestart { get; set; } = false;
}
