// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;
using XiHan.Framework.Data.SqlSugar.Initializers;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Authorization.SqlSugar.Entities;

/// <summary>
/// 用户直接权限授予实体
/// </summary>
[SugarTable("sys_authz_user_permission")]
[TableInitialization(Group = "Authorization")]
[SugarIndex("ux_authz_up_tenant_user_perm",
    nameof(SysAuthzUserPermission.TenantId), OrderByType.Asc,
    nameof(SysAuthzUserPermission.UserId), OrderByType.Asc,
    nameof(SysAuthzUserPermission.PermissionName), OrderByType.Asc,
    true)]
public class SysAuthzUserPermission : SugarMultiTenantEntity<long>, IStrictMultiTenantEntity
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysAuthzUserPermission() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysAuthzUserPermission(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 用户标识
    /// </summary>
    [SugarColumn(ColumnName = "User_Id", Length = 128, IsNullable = false, ColumnDescription = "用户标识")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 权限名称
    /// </summary>
    [SugarColumn(ColumnName = "Permission_Name", Length = 256, IsNullable = false, ColumnDescription = "权限名称")]
    public string PermissionName { get; set; } = string.Empty;
}
