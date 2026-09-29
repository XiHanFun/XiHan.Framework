// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Upgrade.SqlSugar.Entities;

/// <summary>
/// 升级迁移历史实体，追加写入
/// </summary>
[SugarTable("sys_upgrade_migration_history")]
public class SysUpgradeMigrationHistory : SugarEntity<long>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysUpgradeMigrationHistory() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysUpgradeMigrationHistory(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 租户标识
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识")]
    public long? TenantId { get; set; }

    /// <summary>
    /// 租户键，host 或 tenant:{id}
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Key", Length = 64, IsNullable = false, ColumnDescription = "租户键")]
    public string TenantKey { get; set; } = string.Empty;

    /// <summary>
    /// 版本
    /// </summary>
    [SugarColumn(ColumnName = "Version", Length = 32, IsNullable = false, ColumnDescription = "版本")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 脚本名称
    /// </summary>
    [SugarColumn(ColumnName = "Script_Name", Length = 256, IsNullable = false, ColumnDescription = "脚本名称")]
    public string ScriptName { get; set; } = string.Empty;

    /// <summary>
    /// 执行时间
    /// </summary>
    [SugarColumn(ColumnName = "Executed_Time", IsNullable = false, ColumnDescription = "执行时间")]
    public DateTimeOffset ExecutedTime { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    [SugarColumn(ColumnName = "Success", IsNullable = false, ColumnDescription = "是否成功")]
    public bool Success { get; set; }

    /// <summary>
    /// 节点名称
    /// </summary>
    [SugarColumn(ColumnName = "Node_Name", Length = 128, IsNullable = true, ColumnDescription = "节点名称")]
    public string? NodeName { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [SugarColumn(ColumnName = "Error_Message", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "错误信息")]
    public string? ErrorMessage { get; set; }
}
