// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Authorization.SqlSugar.Entities;

/// <summary>
/// 角色权限授予实体
/// </summary>
[SugarTable("sys_authz_role_permission")]
[TableInitialization(Group = "Authorization")]
[SugarIndex("ux_authz_rp_tenant_role_perm",
    nameof(SysAuthzRolePermission.TenantId), OrderByType.Asc,
    nameof(SysAuthzRolePermission.RoleId), OrderByType.Asc,
    nameof(SysAuthzRolePermission.PermissionName), OrderByType.Asc,
    true)]
public class SysAuthzRolePermission : SugarMultiTenantEntity<long>, IStrictMultiTenantEntity
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysAuthzRolePermission() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysAuthzRolePermission(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 角色标识
    /// </summary>
    [SugarColumn(ColumnName = "Role_Id", Length = 128, IsNullable = false, ColumnDescription = "角色标识")]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// 权限名称
    /// </summary>
    [SugarColumn(ColumnName = "Permission_Name", Length = 256, IsNullable = false, ColumnDescription = "权限名称")]
    public string PermissionName { get; set; } = string.Empty;
}
