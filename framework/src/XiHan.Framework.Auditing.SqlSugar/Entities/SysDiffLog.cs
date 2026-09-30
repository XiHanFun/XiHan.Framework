// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Auditing.SqlSugar.Entities;

/// <summary>
/// 实体差异日志实体
/// </summary>
[SplitTable(SplitType.Month)]
[SugarTable("sys_diff_log_{year}{month}{day}")]
public class SysDiffLog : SugarCreationEntity<long>, ISplitTableEntity
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysDiffLog() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysDiffLog(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 创建时间，同时作为分表字段
    /// </summary>
    [SplitField]
    [SugarColumn(ColumnName = "Created_Time", IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    public override DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 审计类型
    /// </summary>
    [SugarColumn(ColumnName = "Audit_Type", Length = 32, IsNullable = false, ColumnDescription = "审计类型")]
    public string AuditType { get; set; } = "EntityChange";

    /// <summary>
    /// 操作类型（Create/Update/Delete/Restore）
    /// </summary>
    [SugarColumn(ColumnName = "Operation_Type", Length = 16, IsNullable = false, ColumnDescription = "操作类型")]
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// 实体类型
    /// </summary>
    [SugarColumn(ColumnName = "Entity_Type", Length = 256, IsNullable = false, ColumnDescription = "实体类型")]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// 实体标识
    /// </summary>
    [SugarColumn(ColumnName = "Entity_Id", Length = 256, IsNullable = true, ColumnDescription = "实体标识")]
    public string? EntityId { get; set; }

    /// <summary>
    /// 前值 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Before_Data", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "前值 JSON")]
    public string? BeforeData { get; set; }

    /// <summary>
    /// 后值 JSON
    /// </summary>
    [SugarColumn(ColumnName = "After_Data", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "后值 JSON")]
    public string? AfterData { get; set; }

    /// <summary>
    /// 变更字段 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Changed_Fields", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "变更字段 JSON")]
    public string? ChangedFields { get; set; }

    /// <summary>
    /// 请求路径
    /// </summary>
    [SugarColumn(ColumnName = "Request_Path", Length = 512, IsNullable = true, ColumnDescription = "请求路径")]
    public string? RequestPath { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    [SugarColumn(ColumnName = "Request_Method", Length = 16, IsNullable = true, ColumnDescription = "请求方法")]
    public string? RequestMethod { get; set; }

    /// <summary>
    /// 操作 IP
    /// </summary>
    [SugarColumn(ColumnName = "Operation_Ip", Length = 64, IsNullable = true, ColumnDescription = "操作 IP")]
    public string? OperationIp { get; set; }

    /// <summary>
    /// 请求标识
    /// </summary>
    [SugarColumn(ColumnName = "Request_Id", Length = 64, IsNullable = true, ColumnDescription = "请求标识")]
    public string? RequestId { get; set; }

    /// <summary>
    /// 用户标识
    /// </summary>
    [SugarColumn(ColumnName = "User_Id", IsNullable = true, ColumnDescription = "用户标识")]
    public long? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [SugarColumn(ColumnName = "User_Name", Length = 128, IsNullable = true, ColumnDescription = "用户名")]
    public string? UserName { get; set; }

    /// <summary>
    /// 租户标识
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = true, ColumnDescription = "租户标识")]
    public long? TenantId { get; set; }
}
