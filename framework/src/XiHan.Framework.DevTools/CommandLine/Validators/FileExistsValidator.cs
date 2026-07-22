// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.DevTools.CommandLine.Validators;

/// <summary>
/// 文件存在验证器
/// </summary>
public class FileExistsValidator : IValidator
{
    /// <summary>
    /// 验证文件是否存在
    /// </summary>
    /// <param name="value">文件路径</param>
    /// <param name="parameters">验证参数（未使用）</param>
    /// <returns>验证结果</returns>
    public ValidationResult Validate(object? value, object[]? parameters = null)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        var filePath = value.ToString();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ValidationResult.Error("文件路径不能为空");
        }

        if (!File.Exists(filePath))
        {
            return ValidationResult.Error($"文件 '{filePath}' 不存在");
        }

        return ValidationResult.Success;
    }
}
