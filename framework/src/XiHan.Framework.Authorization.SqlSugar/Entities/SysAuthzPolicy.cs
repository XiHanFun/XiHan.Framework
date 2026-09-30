// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;

namespace XiHan.Framework.Authorization.SqlSugar.Entities;

/// <summary>
/// 授权策略实体
/// </summary>
[SugarTable("sys_authz_policy")]
[TableInitialization(Group = "Authorization")]
[SugarIndex("ux_authz_policy_name", nameof(SysAuthzPolicy.PolicyName), OrderByType.Asc, true)]
public class SysAuthzPolicy : SugarEntity<long>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysAuthzPolicy() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysAuthzPolicy(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 策略名称
    /// </summary>
    [SugarColumn(ColumnName = "Policy_Name", Length = 256, IsNullable = false, ColumnDescription = "策略名称")]
    public string PolicyName { get; set; } = string.Empty;

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
    /// 要求的角色名称的 JSON 数组
    /// </summary>
    [SugarColumn(ColumnName = "Required_Roles", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "要求的角色名称的 JSON 数组")]
    public string RequiredRoles { get; set; } = "[]";

    /// <summary>
    /// 要求的权限名称的 JSON 数组
    /// </summary>
    [SugarColumn(ColumnName = "Required_Permissions", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "要求的权限名称的 JSON 数组")]
    public string RequiredPermissions { get; set; } = "[]";

    /// <summary>
    /// 要求的声明的 JSON 对象
    /// </summary>
    [SugarColumn(ColumnName = "Required_Claims", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = false, ColumnDescription = "要求的声明的 JSON 对象")]
    public string RequiredClaims { get; set; } = "{}";

    /// <summary>
    /// 是否启用
    /// </summary>
    [SugarColumn(ColumnName = "Is_Enabled", IsNullable = false, ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 额外属性的 JSON
    /// </summary>
    [SugarColumn(ColumnName = "Properties", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "额外属性的 JSON")]
    public string? Properties { get; set; }
}
