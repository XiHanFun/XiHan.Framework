#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ValidationResult
// Guid:e4cfb7cd-426d-423d-85ee-53c659561b88
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:06:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Validators;

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 创建验证结果
    /// </summary>
    /// <param name="isValid">是否有效</param>
    /// <param name="errorMessage">错误消息</param>
    public ValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 成功结果
    /// </summary>
    public static ValidationResult Success => new(true);

    /// <summary>
    /// 是否验证成功
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// 创建错误结果
    /// </summary>
    /// <param name="errorMessage">错误消息</param>
    /// <returns>验证结果</returns>
    public static ValidationResult Error(string errorMessage)
    {
        return new ValidationResult(false, errorMessage);
    }
}
