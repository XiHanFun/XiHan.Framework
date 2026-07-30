// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Upgrade.Models;

/// <summary>
/// 升级版本状态
/// </summary>
public sealed class UpgradeVersionState
{
    /// <summary>
    /// 记录标识
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 租户ID
    /// </summary>
    public long? TenantId { get; set; }

    /// <summary>
    /// 应用版本
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// 数据库版本
    /// </summary>
    public string DbVersion { get; set; } = "0.0.0";

    /// <summary>
    /// 最小支持版本
    /// </summary>
    public string? MinSupportVersion { get; set; }

    /// <summary>
    /// 是否升级中
    /// </summary>
    public bool IsUpgrading { get; set; }

    /// <summary>
    /// 升级节点
    /// </summary>
    public string? UpgradeNode { get; set; }

    /// <summary>
    /// 升级开始时间
    /// </summary>
    public DateTimeOffset? UpgradeStartTime { get; set; }
}
