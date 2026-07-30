// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using XiHan.Framework.Security.Password;

namespace XiHan.Framework.Security.Services;

/// <summary>
/// 密码策略服务默认实现
/// </summary>
public partial class PasswordPolicyService : IPasswordPolicyService
{
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

    private readonly PasswordPolicyOptions _options;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordHistoryStore _passwordHistoryStore;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">密码策略配置选项</param>
    /// <param name="passwordHasher">密码哈希服务</param>
    /// <param name="passwordHistoryStore">密码历史记录存储</param>
    public PasswordPolicyService(
        IOptions<PasswordPolicyOptions> options,
        IPasswordHasher passwordHasher,
        IPasswordHistoryStore passwordHistoryStore)
    {
        _options = options.Value;
        _passwordHasher = passwordHasher;
        _passwordHistoryStore = passwordHistoryStore;
    }

    /// <summary>
    /// 验证密码是否符合策略
    /// </summary>
    /// <param name="password">待验证的密码</param>
    /// <returns>密码验证结果</returns>
    public PasswordValidationResult Validate(string password)
    {
        var errors = new List<string>();
        var score = 0;

        // 检查密码是否为空
        if (string.IsNullOrWhiteSpace(password))
        {
            return PasswordValidationResult.Failure("密码不能为空", 0, ["密码不能为空"]);
        }

        // 检查最小长度
        if (password.Length < _options.MinimumLength)
        {
            errors.Add($"密码长度至少需要 {_options.MinimumLength} 个字符");
        }
        else
        {
            score += Math.Min(password.Length - _options.MinimumLength, 10);
        }

        // 检查最大长度
        if (password.Length > _options.MaximumLength)
        {
            errors.Add($"密码长度不能超过 {_options.MaximumLength} 个字符");
        }

        // 检查大写字母
        if (_options.RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("密码必须包含至少一个大写字母");
        }
        else if (password.Any(char.IsUpper))
        {
            score += 10;
        }

        // 检查小写字母
        if (_options.RequireLowercase && !password.Any(char.IsLower))
        {
            errors.Add("密码必须包含至少一个小写字母");
        }
        else if (password.Any(char.IsLower))
        {
            score += 10;
        }

        // 检查数字
        if (_options.RequireDigit && !password.Any(char.IsDigit))
        {
            errors.Add("密码必须包含至少一个数字");
        }
        else if (password.Any(char.IsDigit))
        {
            score += 10;
        }

        // 检查特殊字符
        if (_options.RequireSpecialCharacter && !SpecialCharacterRegex().IsMatch(password))
        {
            errors.Add("密码必须包含至少一个特殊字符");
        }
        else if (SpecialCharacterRegex().IsMatch(password))
        {
            score += 15;
        }

        // 检查是否在常用弱密码黑名单中
        var lowerPassword = password.ToLower();
        if (CommonWeakPasswords.Contains(lowerPassword))
        {
            errors.Add("密码过于常见，请使用更安全的密码");
            score = Math.Max(0, score - 30);
        }

        // 检查自定义黑名单
        if (_options.CustomBlacklist is { Count: > 0 } &&
            _options.CustomBlacklist.Any(blacklistItem =>
                lowerPassword.Contains(blacklistItem.ToLower())))
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
            return PasswordValidationResult.Failure("密码不符合安全要求", score, errors);
        }

        return PasswordValidationResult.Success(score);
    }

    /// <summary>
    /// 检查新密码是否与历史密码重复
    /// </summary>
    /// <remarks>
    /// 入参为新密码<b>明文</b>：历史存储的是 PBKDF2 加盐哈希，必须用 VerifyPassword(历史哈希, 明文) 逐条比对，
    /// 不能直接比较哈希字符串（同一明文每次哈希结果不同）。
    /// </remarks>
    /// <param name="newPassword">新密码明文</param>
    /// <param name="userId">用户标识</param>
    /// <param name="historyCount">历史记录数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否重复使用旧密码</returns>
    public async Task<bool> IsPasswordReusedAsync(string newPassword, long userId, int historyCount, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(newPassword) || historyCount <= 0)
        {
            return false;
        }

        var recentHashes = await _passwordHistoryStore.GetRecentPasswordHashesAsync(userId, historyCount, ct);

        foreach (var historicalHash in recentHashes)
        {
            if (_passwordHasher.VerifyPassword(historicalHash, newPassword))
            {
                return true;
            }
        }

        return false;
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
            if (sequences.Contains(substring) || sequences.Contains(new string([.. substring.Reverse()])))
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
