#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SecurityConstants
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5c6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Constants;

/// <summary>
/// 安全相关常量
/// </summary>
public static class SecurityConstants
{
    /// <summary>
    /// 默认密码最小长度
    /// </summary>
    public const int DefaultPasswordMinLength = 6;

    /// <summary>
    /// 默认密码最大长度
    /// </summary>
    public const int DefaultPasswordMaxLength = 128;

    /// <summary>
    /// 默认用户名最小长度
    /// </summary>
    public const int DefaultUsernameMinLength = 6;

    /// <summary>
    /// 默认用户名最大长度
    /// </summary>
    public const int DefaultUsernameMaxLength = 50;

    /// <summary>
    /// 默认邮箱最大长度
    /// </summary>
    public const int DefaultEmailMaxLength = 256;

    /// <summary>
    /// 默认手机号长度
    /// </summary>
    public const int DefaultPhoneNumberLength = 11;

    /// <summary>
    /// 默认验证码长度
    /// </summary>
    public const int DefaultVerificationCodeLength = 6;
}
