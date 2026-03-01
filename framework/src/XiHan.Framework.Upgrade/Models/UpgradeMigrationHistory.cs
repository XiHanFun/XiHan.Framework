#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UpgradeMigrationHistory
// Guid:5a3c3e1d-5af1-4a0d-9d78-9fe26d95b0fd
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/03/01 17:10:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Upgrade.Models;

/// <summary>
/// 升级迁移历史
/// </summary>
public sealed class UpgradeMigrationHistory
{
    /// <summary>
    /// 租户ID
    /// </summary>
    public long? TenantId { get; set; }

    /// <summary>
    /// 版本
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 脚本名称
    /// </summary>
    public string ScriptName { get; set; } = string.Empty;

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecutedTime { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 节点名称
    /// </summary>
    public string? NodeName { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }
}
