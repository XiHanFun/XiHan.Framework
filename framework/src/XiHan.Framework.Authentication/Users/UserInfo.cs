// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authentication.Users;

/// <summary>
/// 用户信息
/// </summary>
public class UserInfo
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 邮箱
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 手机号
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// 是否启用双因素认证
    /// </summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// 双因素认证密钥
    /// </summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>
    /// 恢复码（哈希值）
    /// </summary>
    public List<string> RecoveryCodes { get; set; } = [];

    /// <summary>
    /// 是否锁定
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// 锁定结束时间
    /// </summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// 登录失败次数
    /// </summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginTime { get; set; }

    /// <summary>
    /// 密码修改时间
    /// </summary>
    public DateTime? PasswordChangedTime { get; set; }

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 额外数据
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; set; }
}
