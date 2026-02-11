#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ValidationResult
// Guid:1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 15:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Shared.Paging.Validators;

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public ValidationResult(bool isValid, List<string> errors)
    {
        IsValid = isValid;
        Errors = errors ?? [];
    }

    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// 错误信息列表
    /// </summary>
    public List<string> Errors { get; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ValidationResult Success() => new(true, []);

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ValidationResult Failure(params string[] errors) => new(false, [.. errors]);

    /// <summary>
    /// 获取错误信息字符串
    /// </summary>
    public string GetErrorMessage() => string.Join("; ", Errors);
}
