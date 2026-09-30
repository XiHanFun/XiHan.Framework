// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Authorization.SqlSugar.Entities;

/// <summary>
/// 用户角色关联实体
/// </summary>
[SugarTable("sys_authz_user_role")]
[TableInitialization(Group = "Authorization")]
[SugarIndex("ux_authz_ur_tenant_user_role",
    nameof(SysAuthzUserRole.TenantId), OrderByType.Asc,
    nameof(SysAuthzUserRole.UserId), OrderByType.Asc,
    nameof(SysAuthzUserRole.RoleId), OrderByType.Asc,
    true)]
[SugarIndex("ix_authz_ur_tenant_role",
    nameof(SysAuthzUserRole.TenantId), OrderByType.Asc,
    nameof(SysAuthzUserRole.RoleId), OrderByType.Asc)]
public class SysAuthzUserRole : SugarMultiTenantEntity<long>, IStrictMultiTenantEntity
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysAuthzUserRole() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysAuthzUserRole(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 用户标识
    /// </summary>
    [SugarColumn(ColumnName = "User_Id", Length = 128, IsNullable = false, ColumnDescription = "用户标识")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 角色标识
    /// </summary>
    [SugarColumn(ColumnName = "Role_Id", Length = 128, IsNullable = false, ColumnDescription = "角色标识")]
    public string RoleId { get; set; } = string.Empty;
}
