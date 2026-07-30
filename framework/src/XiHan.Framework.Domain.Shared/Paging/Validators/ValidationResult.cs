// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
