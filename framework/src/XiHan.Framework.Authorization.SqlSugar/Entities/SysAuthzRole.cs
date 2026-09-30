// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Authorization.SqlSugar.Entities;

/// <summary>
/// 角色实体
/// </summary>
[SugarTable("sys_authz_role")]
[TableInitialization(Group = "Authorization")]
[SugarIndex("ux_authz_role_tenant_id",
    nameof(SysAuthzRole.TenantId), OrderByType.Asc,
    nameof(SysAuthzRole.RoleId), OrderByType.Asc,
    true)]
[SugarIndex("ux_authz_role_tenant_name",
    nameof(SysAuthzRole.TenantId), OrderByType.Asc,
    nameof(SysAuthzRole.RoleName), OrderByType.Asc,
    true)]
public class SysAuthzRole : SugarMultiTenantEntity<long>, IStrictMultiTenantEntity
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysAuthzRole() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysAuthzRole(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 角色标识
    /// </summary>
    [SugarColumn(ColumnName = "Role_Id", Length = 128, IsNullable = false, ColumnDescription = "角色标识")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// 角色名称
    /// </summary>
    [SugarColumn(ColumnName = "Role_Name", Length = 128, IsNullable = false, ColumnDescription = "角色名称")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [SugarColumn(ColumnName = "Display_Name", Length = 256, IsNullable = false, ColumnDescription = "显示名称")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    [SugarColumn(ColumnName = "Description", Length = 1024, IsNullable = true, ColumnDescription = "描述")]
    public string? Description { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [SugarColumn(ColumnName = "Is_Enabled", IsNullable = false, ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 是否为默认角色
    /// </summary>
    [SugarColumn(ColumnName = "Is_Default", IsNullable = false, ColumnDescription = "是否为默认角色")]
    public bool IsDefault { get; set; }

    /// <summary>
    /// 是否为静态角色
    /// </summary>
    [SugarColumn(ColumnName = "Is_Static", IsNullable = false, ColumnDescription = "是否为静态角色")]
    public bool IsStatic { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    [SugarColumn(ColumnName = "Sort_Order", IsNullable = false, ColumnDescription = "排序")]
    public int SortOrder { get; set; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    [SugarColumn(ColumnName = "Created_Time", IsNullable = false, ColumnDescription = "创建时间（UTC）")]
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// 最后修改时间（UTC）
    /// </summary>
    [SugarColumn(ColumnName = "Last_Modified_Time", IsNullable = true, ColumnDescription = "最后修改时间（UTC）")]
    public DateTime? LastModifiedTime { get; set; }

    /// <summary>
    /// 额外属性的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Properties", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "额外属性的 JSON")]
    public string? Properties { get; set; }
}
