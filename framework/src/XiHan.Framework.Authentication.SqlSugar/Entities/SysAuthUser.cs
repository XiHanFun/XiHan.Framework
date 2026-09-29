// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Data.SqlSugar.Entities;

namespace XiHan.Framework.Authentication.SqlSugar.Entities;

/// <summary>
/// 认证用户实体
/// </summary>
[SugarTable("sys_auth_user")]
[SugarIndex("ux_{table}_tenant_normalized_user_name", nameof(TenantId), OrderByType.Asc, nameof(NormalizedUserName), OrderByType.Asc, true)]
public class SysAuthUser : SugarEntity<long>
{
    /// <summary>
    /// 构造函数，供 SqlSugar 物化实体使用
    /// </summary>
    public SysAuthUser() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">用户标识</param>
    public SysAuthUser(long basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 租户标识，0 表示平台
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", IsNullable = false, ColumnDescription = "租户标识，0 表示平台")]
    public long TenantId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [SugarColumn(ColumnName = "User_Name", Length = 128, IsNullable = false, ColumnDescription = "用户名")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 规范化用户名
    /// </summary>
    [SugarColumn(ColumnName = "Normalized_User_Name", Length = 128, IsNullable = false, ColumnDescription = "规范化用户名")]
    public string NormalizedUserName { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希
    /// </summary>
    [SugarColumn(ColumnName = "Password_Hash", Length = 512, IsNullable = false, ColumnDescription = "密码哈希")]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 邮箱
    /// </summary>
    [SugarColumn(ColumnName = "Email", Length = 256, IsNullable = true, ColumnDescription = "邮箱")]
    public string? Email { get; set; }

    /// <summary>
    /// 手机号
    /// </summary>
    [SugarColumn(ColumnName = "Phone_Number", Length = 32, IsNullable = true, ColumnDescription = "手机号")]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// 是否启用双因素认证
    /// </summary>
    [SugarColumn(ColumnName = "Two_Factor_Enabled", IsNullable = false, ColumnDescription = "是否启用双因素认证")]
    public bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// 双因素认证密钥
    /// </summary>
    [SugarColumn(ColumnName = "Two_Factor_Secret", Length = 256, IsNullable = true, ColumnDescription = "双因素认证密钥")]
    public string? TwoFactorSecret { get; set; }

    /// <summary>
    /// 恢复码哈希的 JSON 数组
    /// </summary>
    [SugarColumn(ColumnName = "Recovery_Codes", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "恢复码哈希的 JSON 数组")]
    public string? RecoveryCodes { get; set; }

    /// <summary>
    /// 是否锁定
    /// </summary>
    [SugarColumn(ColumnName = "Is_Locked", IsNullable = false, ColumnDescription = "是否锁定")]
    public bool IsLocked { get; set; }

    /// <summary>
    /// 锁定结束时间（UTC）
    /// </summary>
    [SugarColumn(ColumnName = "Lockout_End", IsNullable = true, ColumnDescription = "锁定结束时间（UTC）")]
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// 登录失败次数
    /// </summary>
    [SugarColumn(ColumnName = "Failed_Login_Attempts", IsNullable = false, ColumnDescription = "登录失败次数")]
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// 最后登录时间（UTC）
    /// </summary>
    [SugarColumn(ColumnName = "Last_Login_Time", IsNullable = true, ColumnDescription = "最后登录时间（UTC）")]
    public DateTime? LastLoginTime { get; set; }

    /// <summary>
    /// 密码修改时间（UTC）
    /// </summary>
    [SugarColumn(ColumnName = "Password_Changed_Time", IsNullable = true, ColumnDescription = "密码修改时间（UTC）")]
    public DateTime? PasswordChangedTime { get; set; }

    /// <summary>
    /// 是否激活
    /// </summary>
    [SugarColumn(ColumnName = "Is_Active", IsNullable = false, ColumnDescription = "是否激活")]
    public bool IsActive { get; set; }

    /// <summary>
    /// 附加数据的 JSON 对象
    /// </summary>
    [SugarColumn(ColumnName = "Additional_Data", ColumnDataType = StaticConfig.CodeFirst_BigString, IsNullable = true, ColumnDescription = "附加数据的 JSON 对象")]
    public string? AdditionalData { get; set; }
}
