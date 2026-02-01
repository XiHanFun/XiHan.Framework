#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAuthenticationService
// Guid:f2a3b4c5-d6e7-8901-5678-123456789021
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/06 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Authentication;

/// <summary>
/// 认证服务接口
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// 用户名密码认证
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>认证结果</returns>
    Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证密码强度
    /// </summary>
    /// <param name="password">密码</param>
    /// <param name="customBlacklist">自定义黑名单</param>
    /// <returns>验证结果</returns>
    Task<PasswordValidationResult> ValidatePasswordStrengthAsync(string password, List<string>? customBlacklist = null);

    /// <summary>
    /// 更改密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="currentPassword">当前密码</param>
    /// <param name="newPassword">新密码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="newPassword">新密码</param>
    /// <param name="resetToken">重置令牌</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> ResetPasswordAsync(string userId, string newPassword, string resetToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启用双因素认证
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>TOTP 设置信息</returns>
    Task<TwoFactorSetupResult> EnableTwoFactorAuthenticationAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证双因素认证代码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="code">验证码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否验证成功</returns>
    Task<bool> VerifyTwoFactorCodeAsync(string userId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// 禁用双因素认证
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> DisableTwoFactorAuthenticationAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成备用恢复码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>恢复码列表</returns>
    Task<List<string>> GenerateRecoveryCodesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证恢复码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="recoveryCode">恢复码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否验证成功</returns>
    Task<bool> VerifyRecoveryCodeAsync(string userId, string recoveryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录登录失败
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否被锁定</returns>
    Task<bool> RecordFailedLoginAttemptAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重置登录失败次数
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ResetFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查账户是否被锁定
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否被锁定</returns>
    Task<bool> IsAccountLockedAsync(string username, CancellationToken cancellationToken = default);
}
