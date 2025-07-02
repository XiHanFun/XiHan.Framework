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
using XiHan.Framework.Utils.Constants;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Utils.Security;

/// <summary>
/// 密码强度检测器
/// </summary>
public class PasswordStrengthChecker
{
    /// <summary>
    /// 默认的弱密码列表
    /// </summary>
    private static readonly List<string> WeakPasswords =
        [
            "123456", "password", "123456789", "12345678", "111111", "123123"
        ];

    /// <summary>
    /// 检查密码强度
    /// </summary>
    /// <param name="password">密码</param>
    /// <param name="customBlacklist">自定义黑名单</param>
    /// <returns></returns>
    public static PasswordStrengthResult CheckPasswordStrength(string password, IEnumerable<string>? customBlacklist = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordStrengthResult(false, "密码不能为空", 0);
        }

        // 初始化评分
        var score = 0;

        switch (password.Length)
        {
            // 检查长度
            case < 8:
                return new PasswordStrengthResult(false, "密码长度不足8位", score);

            case >= 12:
                score += 20;
                break;
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
        if (password.Any(DefaultConsts.SpecialCharacters.Contains))
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
    /// <param name="length">密码长度</param>
    /// <param name="includeSpecialChars">是否包含特殊字符</param>
    /// <returns>随机密码</returns>
    /// <exception cref="ArgumentException">密码长度必须大于或等于8位</exception>
    public static string GeneratePassword(int length = 12, bool includeSpecialChars = true)
    {
        if (length < 8)
        {
            throw new ArgumentException("密码长度必须大于或等于8位", nameof(length));
        }

        var characterPool = new StringBuilder(DefaultConsts.UppercaseLetters + DefaultConsts.LowercaseLetters + DefaultConsts.Digits);

        if (includeSpecialChars)
        {
            _ = characterPool.Append(DefaultConsts.SpecialCharacters);
        }

        return new string([.. Enumerable.Range(0, length).Select(_ => characterPool[RandomHelper.GetRandom(characterPool.Length)])]);
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
    /// <param name="isStrong">是否强密码</param>
    /// <param name="message">检查结果消息</param>
    /// <param name="score">评分</param>
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
    /// <returns>字符串表示</returns>
    public override string ToString()
    {
        return $"Strong: {IsStrong}, Message: {Message}, Score: {Score}";
    }
}
