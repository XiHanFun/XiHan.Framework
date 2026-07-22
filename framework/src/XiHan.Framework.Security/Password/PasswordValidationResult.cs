// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Security.Password;

/// <summary>
/// 密码验证结果
/// </summary>
public class PasswordValidationResult
{
    /// <summary>
    /// 是否通过验证
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 密码强度评分
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// 验证消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 错误详情
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static PasswordValidationResult Success(int score)
    {
        return new PasswordValidationResult
        {
            IsValid = true,
            Score = score,
            Message = "密码强度良好"
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static PasswordValidationResult Failure(string message, int score, List<string>? errors = null)
    {
        return new PasswordValidationResult
        {
            IsValid = false,
            Score = score,
            Message = message,
            Errors = errors ?? []
        };
    }
}
