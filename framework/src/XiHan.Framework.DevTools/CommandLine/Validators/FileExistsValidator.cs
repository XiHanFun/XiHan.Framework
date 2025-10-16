#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FileExistsValidator
// Guid:0b52408d-2986-4765-8cdf-3f432dbcb9b2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 5:07:14
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
