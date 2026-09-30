// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;

namespace XiHan.Framework.Authorization.SqlSugar.Entities;

/// <summary>
/// 权限定义实体
/// </summary>
[SugarTable("sys_authz_permission")]
[TableInitialization(Group = "Authorization")]
[SugarIndex("ux_authz_perm_name", nameof(SysAuthzPermission.PermissionName), OrderByType.Asc, true)]
public class SysAuthzPermission : SugarEntity<long>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysAuthzPermission() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysAuthzPermission(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 权限名称
    /// </summary>
    [SugarColumn(ColumnName = "Permission_Name", Length = 256, IsNullable = false, ColumnDescription = "权限名称")]
    public string PermissionName { get; set; } = string.Empty;

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
    /// 父权限名称
    /// </summary>
    [SugarColumn(ColumnName = "Parent_Name", Length = 256, IsNullable = true, ColumnDescription = "父权限名称")]
    public string? ParentName { get; set; }

    /// <summary>
    /// 分组名称
    /// </summary>
    [SugarColumn(ColumnName = "Tag", Length = 128, IsNullable = true, ColumnDescription = "分组名称")]
    public string? Tag { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    [SugarColumn(ColumnName = "Is_Enabled", IsNullable = false, ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 排序
    /// </summary>
    [SugarColumn(ColumnName = "Sort_Order", IsNullable = false, ColumnDescription = "排序")]
    public int SortOrder { get; set; }

    /// <summary>
    /// 额外属性的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Properties", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "额外属性的 JSON")]
    public string? Properties { get; set; }
}
