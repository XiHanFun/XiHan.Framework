#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PasswordPolicyOptions
// Guid:d4e5f6a7-b8c9-0123-def0-123456789013
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication.Password;

/// <summary>
/// 密码策略配置选项
/// </summary>
public class PasswordPolicyOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Authentication:PasswordPolicy";

    /// <summary>
    /// 密码最小长度
    /// </summary>
    public int MinimumLength { get; set; } = 8;

    /// <summary>
    /// 密码最大长度
    /// </summary>
    public int MaximumLength { get; set; } = 128;

    /// <summary>
    /// 是否需要大写字母
    /// </summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>
    /// 是否需要小写字母
    /// </summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>
    /// 是否需要数字
    /// </summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// 是否需要特殊字符
    /// </summary>
    public bool RequireSpecialCharacter { get; set; } = true;

    /// <summary>
    /// 密码过期天数（0表示不过期）
    /// </summary>
    public int PasswordExpirationDays { get; set; } = 0;

    /// <summary>
    /// 密码历史记录数（防止重复使用旧密码）
    /// </summary>
    public int PasswordHistoryCount { get; set; } = 5;

    /// <summary>
    /// 自定义黑名单
    /// </summary>
    public List<string> CustomBlacklist { get; set; } = [];

    /// <summary>
    /// 允许的最大连续失败尝试次数
    /// </summary>
    public int MaxFailedAccessAttempts { get; set; } = 5;

    /// <summary>
    /// 账户锁定时长（分钟）
    /// </summary>
    public int LockoutDurationMinutes { get; set; } = 30;
}
