#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:AuthenticationResult
// Guid:a3b4c5d6-e7f8-9012-6789-123456789022
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Authentication.Jwt;

namespace XiHan.Framework.Authentication;

/// <summary>
/// 认证结果
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// 是否认证成功
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// JWT Token 信息
    /// </summary>
    public JwtTokenResult? TokenResult { get; set; }

    /// <summary>
    /// 是否需要双因素认证
    /// </summary>
    public bool RequiresTwoFactor { get; set; }

    /// <summary>
    /// 是否被锁定
    /// </summary>
    public bool IsLockedOut { get; set; }

    /// <summary>
    /// 锁定结束时间
    /// </summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 额外数据
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static AuthenticationResult Success(string userId, string username, JwtTokenResult tokenResult)
    {
        return new AuthenticationResult
        {
            Succeeded = true,
            UserId = userId,
            Username = username,
            TokenResult = tokenResult
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static AuthenticationResult Failure(string errorMessage)
    {
        return new AuthenticationResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// 创建需要双因素认证的结果
    /// </summary>
    public static AuthenticationResult RequiresTwoFactorAuthentication(string userId, string username)
    {
        return new AuthenticationResult
        {
            Succeeded = false,
            UserId = userId,
            Username = username,
            RequiresTwoFactor = true
        };
    }

    /// <summary>
    /// 创建账户锁定结果
    /// </summary>
    public static AuthenticationResult LockedOut(DateTime? lockoutEnd)
    {
        return new AuthenticationResult
        {
            Succeeded = false,
            IsLockedOut = true,
            LockoutEnd = lockoutEnd,
            ErrorMessage = "账户已被锁定"
        };
    }
}
