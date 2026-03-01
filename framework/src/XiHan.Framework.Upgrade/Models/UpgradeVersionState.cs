#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UpgradeVersionState
// Guid:2b7db1b5-97ee-47b4-8c6a-bc32c1d0b5f4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 17:10:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
    public DateTime? UpgradeStartTime { get; set; }
}
