// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Upgrade.SqlSugar.Entities;

/// <summary>
/// 升级版本状态实体，每个租户（或宿主）一行
/// </summary>
[SugarIndex("uq_sys_upgrade_version_tenant_key", nameof(TenantKey), OrderByType.Asc, isUnique: true)]
[SugarTable("sys_upgrade_version")]
public class SysUpgradeVersion : SugarEntity<long>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysUpgradeVersion() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysUpgradeVersion(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 租户标识，null 表示宿主
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识")]
    public long? TenantId { get; set; }

    /// <summary>
    /// 租户键，host 或 tenant:{id}
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Key", Length = 64, IsNullable = false, ColumnDescription = "租户键")]
    public string TenantKey { get; set; } = string.Empty;

    /// <summary>
    /// 应用版本
    /// </summary>
    [SugarColumn(ColumnName = "App_Version", Length = 32, IsNullable = false, ColumnDescription = "应用版本")]
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// 数据库版本
    /// </summary>
    [SugarColumn(ColumnName = "Db_Version", Length = 32, IsNullable = false, ColumnDescription = "数据库版本")]
    public string DbVersion { get; set; } = "0.0.0";

    /// <summary>
    /// 最小支持版本
    /// </summary>
    [SugarColumn(ColumnName = "Min_Support_Version", Length = 32, IsNullable = true, ColumnDescription = "最小支持版本")]
    public string? MinSupportVersion { get; set; }

    /// <summary>
    /// 是否升级中
    /// </summary>
    [SugarColumn(ColumnName = "Is_Upgrading", IsNullable = false, ColumnDescription = "是否升级中")]
    public bool IsUpgrading { get; set; }

    /// <summary>
    /// 升级节点
    /// </summary>
    [SugarColumn(ColumnName = "Upgrade_Node", Length = 128, IsNullable = true, ColumnDescription = "升级节点")]
    public string? UpgradeNode { get; set; }

    /// <summary>
    /// 升级开始时间
    /// </summary>
    [SugarColumn(ColumnName = "Upgrade_Start_Time", IsNullable = true, ColumnDescription = "升级开始时间")]
    public DateTimeOffset? UpgradeStartTime { get; set; }
}
