// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Models;
using XiHan.Framework.Upgrade.SqlSugar.Entities;

namespace XiHan.Framework.Upgrade.SqlSugar.Mapping;

/// <summary>
/// 升级实体与模型的映射
/// </summary>
public static class UpgradeMapper
{
    /// <summary>
    /// 构建租户键
    /// </summary>
    /// <param name="tenantId">租户标识</param>
    /// <returns>host 或 tenant:{id}</returns>
    public static string BuildTenantKey(long? tenantId)
    {
        return tenantId.HasValue ? $"tenant:{tenantId.Value}" : "host";
    }

    /// <summary>
    /// 规范化版本值
    /// </summary>
    /// <param name="version">版本值</param>
    /// <returns>规范化后的版本，空白时返回 0.0.0</returns>
    public static string NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? "0.0.0" : version.Trim();
    }

    /// <summary>
    /// 把版本状态实体转换为模型
    /// </summary>
    /// <param name="entity">版本状态实体</param>
    /// <returns>版本状态模型</returns>
    public static UpgradeVersionState ToState(SysUpgradeVersion entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new UpgradeVersionState
        {
            Id = entity.BasicId,
            TenantId = entity.TenantId,
            AppVersion = entity.AppVersion,
            DbVersion = entity.DbVersion,
            MinSupportVersion = entity.MinSupportVersion,
            IsUpgrading = entity.IsUpgrading,
            UpgradeNode = entity.UpgradeNode,
            UpgradeStartTime = entity.UpgradeStartTime
        };
    }

    /// <summary>
    /// 把迁移历史实体转换为模型
    /// </summary>
    /// <param name="entity">迁移历史实体</param>
    /// <returns>迁移历史模型</returns>
    public static UpgradeMigrationHistory ToHistory(SysUpgradeMigrationHistory entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new UpgradeMigrationHistory
        {
            TenantId = entity.TenantId,
            Version = entity.Version,
            ScriptName = entity.ScriptName,
            ExecutedTime = entity.ExecutedTime,
            Success = entity.Success,
            NodeName = entity.NodeName,
            ErrorMessage = entity.ErrorMessage
        };
    }
}
