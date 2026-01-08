#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DefaultAuthenticationService
// Guid:f8a9b0c1-d2e3-4567-1234-123456789027
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/09 10:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Security.Claims;
using System.Text.RegularExpressions;
using XiHan.Framework.Authentication.Jwt;
using XiHan.Framework.Authentication.Otp;
using XiHan.Framework.Authentication.Password;

namespace XiHan.Framework.Authentication;

/// <summary>
/// 默认认证服务实现
/// </summary>
public partial class DefaultAuthenticationService : IAuthenticationService
{
    private readonly IUserStore _userStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IOtpService _otpService;
    private readonly PasswordPolicy _passwordPolicy;

    /// <summary>
    /// 常用弱密码黑名单
    /// </summary>
    private static readonly HashSet<string> CommonWeakPasswords =
    [
        "password", "123456", "12345678", "qwerty", "abc123", "monkey",
        "1234567", "letmein", "trustno1", "dragon", "baseball", "111111",
        "iloveyou", "master", "sunshine", "ashley", "bailey", "passw0rd",
        "shadow", "123123", "654321", "superman", "qazwsx", "michael",
        "football", "admin", "admin123", "root", "test", "guest"
    ];

    /// <summary>
    /// 构造函数
    /// </summary>
    public DefaultAuthenticationService(
        IUserStore userStore,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IOtpService otpService,
        PasswordPolicy passwordPolicy)
    {
        _userStore = userStore;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _otpService = otpService;
        _passwordPolicy = passwordPolicy;
    }

    /// <summary>
    /// 用户名密码认证
    /// </summary>
    public async Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return AuthenticationResult.Failure("用户名或密码不能为空");
        }

        // 检查账户是否被锁定
        if (await IsAccountLockedAsync(username, cancellationToken))
        {
            var lockoutEnd = await _userStore.GetLockoutEndAsync(username, cancellationToken);
            return AuthenticationResult.LockedOut(lockoutEnd);
        }

        // 获取用户信息
        var user = await _userStore.GetUserByUsernameAsync(username, cancellationToken);
        if (user == null)
        {
            // 记录失败尝试（防止用户枚举攻击，即使用户不存在也记录）
            await RecordFailedLoginAttemptAsync(username, cancellationToken);
            return AuthenticationResult.Failure("用户名或密码错误");
        }

        // 检查用户是否激活
        if (!user.IsActive)
        {
            return AuthenticationResult.Failure("账户未激活");
        }

        // 验证密码
        if (!_passwordHasher.VerifyPassword(user.PasswordHash, password))
        {
            await RecordFailedLoginAttemptAsync(username, cancellationToken);
            return AuthenticationResult.Failure("用户名或密码错误");
        }

        // 检查密码是否需要重新哈希
        if (_passwordHasher.NeedsRehash(user.PasswordHash))
        {
            var newHash = _passwordHasher.HashPassword(password);
            await _userStore.UpdatePasswordAsync(user.UserId, newHash, cancellationToken);
        }

        // 检查是否需要双因素认证
        if (user.TwoFactorEnabled)
        {
            return AuthenticationResult.RequiresTwoFactorAuthentication(user.UserId, user.Username);
        }

        // 重置失败登录次数
        await ResetFailedLoginAttemptsAsync(username, cancellationToken);

        // 更新最后登录时间
        user.LastLoginTime = DateTime.UtcNow;
        await _userStore.UpdateUserAsync(user, cancellationToken);

        // 生成 JWT Token
        var claims = GenerateUserClaims(user);
        var tokenResult = _jwtTokenService.GenerateAccessToken(claims);

        return AuthenticationResult.Success(user.UserId, user.Username, tokenResult);
    }

    /// <summary>
    /// 验证密码强度
    /// </summary>
    public Task<PasswordValidationResult> ValidatePasswordStrengthAsync(string password, List<string>? customBlacklist = null)
    {
        var errors = new List<string>();
        var score = 0;

        // 检查密码是否为空
        if (string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(PasswordValidationResult.Failure("密码不能为空", 0, ["密码不能为空"]));
        }

        // 检查长度
        if (password.Length < _passwordPolicy.MinimumLength)
        {
            errors.Add($"密码长度至少需要 {_passwordPolicy.MinimumLength} 个字符");
        }
        else
        {
            score += Math.Min(password.Length - _passwordPolicy.MinimumLength, 10);
        }

        if (password.Length > _passwordPolicy.MaximumLength)
        {
            errors.Add($"密码长度不能超过 {_passwordPolicy.MaximumLength} 个字符");
        }

        // 检查大写字母
        if (_passwordPolicy.RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("密码必须包含至少一个大写字母");
        }
        else if (password.Any(char.IsUpper))
        {
            score += 10;
        }

        // 检查小写字母
        if (_passwordPolicy.RequireLowercase && !password.Any(char.IsLower))
        {
            errors.Add("密码必须包含至少一个小写字母");
        }
        else if (password.Any(char.IsLower))
        {
            score += 10;
        }

        // 检查数字
        if (_passwordPolicy.RequireDigit && !password.Any(char.IsDigit))
        {
            errors.Add("密码必须包含至少一个数字");
        }
        else if (password.Any(char.IsDigit))
        {
            score += 10;
        }

        // 检查特殊字符
        if (_passwordPolicy.RequireSpecialCharacter && !SpecialCharacterRegex().IsMatch(password))
        {
            errors.Add("密码必须包含至少一个特殊字符");
        }
        else if (SpecialCharacterRegex().IsMatch(password))
        {
            score += 15;
        }

        // 检查是否在黑名单中
        var lowerPassword = password.ToLower();
        if (CommonWeakPasswords.Contains(lowerPassword))
        {
            errors.Add("密码过于常见，请使用更安全的密码");
            score = Math.Max(0, score - 30);
        }

        // 检查自定义黑名单
        if (customBlacklist != null && customBlacklist.Any(weak => lowerPassword.Contains(weak.ToLower())))
        {
            errors.Add("密码包含不允许的词汇");
            score = Math.Max(0, score - 20);
        }

        // 检查重复字符
        if (HasRepeatingCharacters(password))
        {
            errors.Add("密码包含过多重复字符");
            score = Math.Max(0, score - 10);
        }

        // 检查连续字符
        if (HasSequentialCharacters(password))
        {
            errors.Add("密码包含连续字符序列");
            score = Math.Max(0, score - 10);
        }

        // 确保分数在 0-100 之间
        score = Math.Min(100, Math.Max(0, score));

        if (errors.Count > 0)
        {
            return Task.FromResult(PasswordValidationResult.Failure("密码不符合安全要求", score, errors));
        }

        return Task.FromResult(PasswordValidationResult.Success(score));
    }

    /// <summary>
    /// 更改密码
    /// </summary>
    public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        // 获取用户信息
        var user = await _userStore.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        // 验证当前密码
        if (!_passwordHasher.VerifyPassword(user.PasswordHash, currentPassword))
        {
            return false;
        }

        // 验证新密码强度
        var validationResult = await ValidatePasswordStrengthAsync(newPassword);
        if (!validationResult.IsValid)
        {
            return false;
        }

        // 哈希新密码
        var newPasswordHash = _passwordHasher.HashPassword(newPassword);

        // 更新密码
        await _userStore.UpdatePasswordAsync(userId, newPasswordHash, cancellationToken);

        // 更新密码修改时间
        user.PasswordChangedTime = DateTime.UtcNow;
        await _userStore.UpdateUserAsync(user, cancellationToken);

        return true;
    }

    /// <summary>
    /// 重置密码
    /// </summary>
    public async Task<bool> ResetPasswordAsync(string userId, string newPassword, string resetToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(resetToken))
        {
            return false;
        }

        // 注意：这里需要验证 resetToken 的有效性
        // 实际实现中应该有一个 token 存储和验证机制
        // 这里仅作示例，实际使用时需要补充完整的 token 验证逻辑

        // 获取用户信息
        var user = await _userStore.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        // 验证新密码强度
        var validationResult = await ValidatePasswordStrengthAsync(newPassword);
        if (!validationResult.IsValid)
        {
            return false;
        }

        // 哈希新密码
        var newPasswordHash = _passwordHasher.HashPassword(newPassword);

        // 更新密码
        await _userStore.UpdatePasswordAsync(userId, newPasswordHash, cancellationToken);

        // 更新密码修改时间
        user.PasswordChangedTime = DateTime.UtcNow;
        await _userStore.UpdateUserAsync(user, cancellationToken);

        return true;
    }

    /// <summary>
    /// 启用双因素认证
    /// </summary>
    public async Task<TwoFactorSetupResult> EnableTwoFactorAuthenticationAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("用户ID不能为空", nameof(userId));
        }

        // 获取用户信息
        var user = await _userStore.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"用户 {userId} 不存在");
        }

        // 生成 TOTP 密钥
        var secret = _otpService.GenerateTotpSecret();

        // 生成二维码 URI
        var qrCodeUri = _otpService.GenerateTotpUri(secret, "XiHan", user.Username);

        // 生成恢复码
        var recoveryCodes = await GenerateRecoveryCodesAsync(userId, cancellationToken);

        // 更新用户信息
        user.TwoFactorSecret = secret;
        user.TwoFactorEnabled = true;
        await _userStore.UpdateUserAsync(user, cancellationToken);

        return new TwoFactorSetupResult
        {
            Secret = secret,
            QrCodeUri = qrCodeUri,
            ManualEntryKey = secret.Replace(" ", ""),
            RecoveryCodes = recoveryCodes
        };
    }

    /// <summary>
    /// 验证双因素认证代码
    /// </summary>
    public async Task<bool> VerifyTwoFactorCodeAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        // 获取用户信息
        var user = await _userStore.GetUserByIdAsync(userId, cancellationToken);
        if (user == null || !user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            return false;
        }

        // 验证 TOTP 代码
        return _otpService.VerifyTotpCode(user.TwoFactorSecret, code);
    }

    /// <summary>
    /// 禁用双因素认证
    /// </summary>
    public async Task<bool> DisableTwoFactorAuthenticationAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        // 获取用户信息
        var user = await _userStore.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        // 禁用双因素认证
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.RecoveryCodes.Clear();
        await _userStore.UpdateUserAsync(user, cancellationToken);

        return true;
    }

    /// <summary>
    /// 生成备用恢复码
    /// </summary>
    public async Task<List<string>> GenerateRecoveryCodesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("用户ID不能为空", nameof(userId));
        }

        // 获取用户信息
        var user = await _userStore.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"用户 {userId} 不存在");
        }

        // 生成恢复码
        var recoveryCodes = _otpService.GenerateRecoveryCodes(10);

        // 哈希恢复码后存储
        user.RecoveryCodes = recoveryCodes.Select(code => _passwordHasher.HashPassword(code)).ToList();
        await _userStore.UpdateUserAsync(user, cancellationToken);

        return recoveryCodes;
    }

    /// <summary>
    /// 验证恢复码
    /// </summary>
    public async Task<bool> VerifyRecoveryCodeAsync(string userId, string recoveryCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(recoveryCode))
        {
            return false;
        }

        // 获取用户信息
        var user = await _userStore.GetUserByIdAsync(userId, cancellationToken);
        if (user == null || user.RecoveryCodes.Count == 0)
        {
            return false;
        }

        // 验证恢复码
        foreach (var hashedCode in user.RecoveryCodes.ToList())
        {
            if (_passwordHasher.VerifyPassword(hashedCode, recoveryCode))
            {
                // 使用后删除该恢复码
                user.RecoveryCodes.Remove(hashedCode);
                await _userStore.UpdateUserAsync(user, cancellationToken);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 记录登录失败
    /// </summary>
    public async Task<bool> RecordFailedLoginAttemptAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        await _userStore.IncrementFailedLoginAttemptsAsync(username, cancellationToken);
        var failedAttempts = await _userStore.GetFailedLoginAttemptsAsync(username, cancellationToken);

        // 检查是否达到锁定阈值
        if (failedAttempts >= _passwordPolicy.MaxFailedAccessAttempts)
        {
            var lockoutEnd = DateTime.UtcNow.AddMinutes(_passwordPolicy.LockoutDurationMinutes);
            await _userStore.SetLockoutEndAsync(username, lockoutEnd, cancellationToken);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 重置登录失败次数
    /// </summary>
    public async Task ResetFailedLoginAttemptsAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        await _userStore.ResetFailedLoginAttemptsAsync(username, cancellationToken);
        await _userStore.SetLockoutEndAsync(username, null, cancellationToken);
    }

    /// <summary>
    /// 检查账户是否被锁定
    /// </summary>
    public async Task<bool> IsAccountLockedAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var lockoutEnd = await _userStore.GetLockoutEndAsync(username, cancellationToken);
        if (lockoutEnd == null)
        {
            return false;
        }

        // 检查锁定是否已过期
        if (lockoutEnd.Value <= DateTime.UtcNow)
        {
            // 锁定已过期，重置状态
            await ResetFailedLoginAttemptsAsync(username, cancellationToken);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 生成用户声明
    /// </summary>
    private static List<Claim> GenerateUserClaims(UserInfo user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.Username)
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        if (!string.IsNullOrEmpty(user.PhoneNumber))
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, user.PhoneNumber));
        }

        return claims;
    }

    /// <summary>
    /// 检查是否有重复字符
    /// </summary>
    private static bool HasRepeatingCharacters(string password)
    {
        var repeatCount = 0;
        for (var i = 1; i < password.Length; i++)
        {
            if (password[i] == password[i - 1])
            {
                repeatCount++;
                if (repeatCount >= 2) // 连续3个或更多相同字符
                {
                    return true;
                }
            }
            else
            {
                repeatCount = 0;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查是否有连续字符
    /// </summary>
    private static bool HasSequentialCharacters(string password)
    {
        const string sequences = "abcdefghijklmnopqrstuvwxyz0123456789";
        var lowerPassword = password.ToLower();

        for (var i = 0; i < lowerPassword.Length - 2; i++)
        {
            var substring = lowerPassword.Substring(i, 3);
            if (sequences.Contains(substring) || sequences.Contains(new string(substring.Reverse().ToArray())))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 特殊字符正则表达式
    /// </summary>
    [GeneratedRegex(@"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]")]
    private static partial Regex SpecialCharacterRegex();
}
