#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PasswordStrengthChecker
// Guid:779986e5-8d07-4767-879b-83b78f6ed821
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/9 5:26:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Text;

namespace XiHan.Framework.Utils.Verifications;

/// <summary>
/// 密码强度检测器
/// </summary>
public class PasswordStrengthChecker
{
    private const string SpecialCharacters = "!@#$%^&*()-_=+[]{}|;:'\",.<>?/";

    private const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";

    private const string Digits = "0123456789";

    private static readonly List<string> WeakPasswords =
                    [
        "123456", "password", "123456789", "12345678", "111111", "123123"
    ];

    /// <summary>
    /// 检查密码强度
    /// </summary>
    /// <param name="password"></param>
    /// <param name="customBlacklist"></param>
    /// <returns></returns>
    public static PasswordStrengthResult CheckPasswordStrength(string password, IEnumerable<string>? customBlacklist = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordStrengthResult(false, "密码不能为空", 0);
        }

        // 初始化评分
        var score = 0;

        // 检查长度
        if (password.Length < 8)
        {
            return new PasswordStrengthResult(false, "密码长度不足8位", score);
        }
        else if (password.Length >= 12)
        {
            score += 20;
        }

        // 检查是否包含大写字母
        if (password.Any(char.IsUpper))
        {
            score += 20;
        }
        else
        {
            return new PasswordStrengthResult(false, "密码必须包含至少一个大写字母", score);
        }

        // 检查是否包含小写字母
        if (password.Any(char.IsLower))
        {
            score += 20;
        }
        else
        {
            return new PasswordStrengthResult(false, "密码必须包含至少一个小写字母", score);
        }

        // 检查是否包含数字
        if (password.Any(char.IsDigit))
        {
            score += 20;
        }
        else
        {
            return new PasswordStrengthResult(false, "密码必须包含至少一个数字", score);
        }

        // 检查是否包含特殊字符
        if (password.Any(SpecialCharacters.Contains))
        {
            score += 20;
        }
        else
        {
            return new PasswordStrengthResult(false, "密码必须包含至少一个特殊字符", score);
        }

        // 检查是否包含弱密码模式
        if (WeakPasswords.Any(password.Contains))
        {
            return new PasswordStrengthResult(false, "密码过于简单，包含弱密码模式", score - 30);
        }

        // 检查是否包含自定义黑名单
        if (customBlacklist is not null && customBlacklist.Any(password.Contains))
        {
            return new PasswordStrengthResult(false, "密码包含禁止使用的词汇", score - 30);
        }

        // 最终结果
        return new PasswordStrengthResult(true, "密码强度良好", score);
    }

    /// <summary>
    /// 生成随机密码
    /// </summary>
    /// <param name="length"></param>
    /// <param name="includeSpecialChars"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string GeneratePassword(int length = 12, bool includeSpecialChars = true)
    {
        if (length < 8)
        {
            throw new ArgumentException("密码长度必须大于或等于8位", nameof(length));
        }

        var characterPool = new StringBuilder(UppercaseLetters + LowercaseLetters + Digits);

        if (includeSpecialChars)
        {
            _ = characterPool.Append(SpecialCharacters);
        }

        var random = new Random();
        return new string(Enumerable.Range(0, length)
            .Select(_ => characterPool[random.Next(characterPool.Length)])
            .ToArray());
    }
}

/// <summary>
/// 密码强度检查结果类
/// </summary>
public class PasswordStrengthResult
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isStrong"></param>
    /// <param name="message"></param>
    /// <param name="score"></param>
    public PasswordStrengthResult(bool isStrong, string message, int score)
    {
        IsStrong = isStrong;
        Message = message;
        Score = score;
    }

    /// <summary>
    /// 是否强密码
    /// </summary>
    public bool IsStrong { get; }

    /// <summary>
    /// 检查结果消息
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 评分
    /// </summary>
    public int Score { get; }

    /// <summary>
    /// ToString
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return $"Strong: {IsStrong}, Message: {Message}, Score: {Score}";
    }
}
