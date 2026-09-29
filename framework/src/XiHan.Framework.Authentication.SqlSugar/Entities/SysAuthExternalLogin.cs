// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Authentication.SqlSugar.Entities;

/// <summary>
/// 第三方登录绑定实体
/// </summary>
[SugarTable("sys_auth_external_login")]
[SugarIndex("ux_{table}_tenant_provider_key", nameof(TenantId), OrderByType.Asc, nameof(Provider), OrderByType.Asc, nameof(ProviderKey), OrderByType.Asc, true)]
[SugarIndex("ix_{table}_user_provider", nameof(UserId), OrderByType.Asc, nameof(Provider), OrderByType.Asc)]
public class SysAuthExternalLogin : SugarEntity<long>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysAuthExternalLogin() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    public SysAuthExternalLogin(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 租户标识，0 表示平台
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = false, ColumnDescription = "租户标识，0 表示平台")]
    public long TenantId { get; set; }

    /// <summary>
    /// 内部用户标识
    /// </summary>
    [SugarColumn(ColumnName = "User_Id", IsNullable = false, ColumnDescription = "内部用户标识")]
    public long UserId { get; set; }

    /// <summary>
    /// 提供商名称（小写）
    /// </summary>
    [SugarColumn(ColumnName = "Provider", Length = 64, IsNullable = false, ColumnDescription = "提供商名称（小写）")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 提供商用户标识
    /// </summary>
    [SugarColumn(ColumnName = "Provider_Key", Length = 256, IsNullable = false, ColumnDescription = "提供商用户标识")]
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>
    /// 提供商返回的显示名称
    /// </summary>
    [SugarColumn(ColumnName = "Display_Name", Length = 256, IsNullable = true, ColumnDescription = "提供商返回的显示名称")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 提供商返回的邮箱
    /// </summary>
    [SugarColumn(ColumnName = "Email", Length = 256, IsNullable = true, ColumnDescription = "提供商返回的邮箱")]
    public string? Email { get; set; }

    /// <summary>
    /// 提供商返回的头像地址
    /// </summary>
    [SugarColumn(ColumnName = "Avatar_Url", Length = 2048, IsNullable = true, ColumnDescription = "提供商返回的头像地址")]
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// 绑定时间（UTC）
    /// </summary>
    [SugarColumn(ColumnName = "Created_Time", IsNullable = false, ColumnDescription = "绑定时间（UTC）")]
    public DateTime CreatedTime { get; set; }
}
