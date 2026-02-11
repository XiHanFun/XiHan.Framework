#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DirectoryExistsValidator
// Guid:0eee7e34-53c3-42bf-829c-bd0b874e4c6b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:07:28
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Validators;

/// <summary>
/// 目录存在验证器
/// </summary>
public class DirectoryExistsValidator : IValidator
{
    /// <summary>
    /// 验证目录是否存在
    /// </summary>
    /// <param name="value">目录路径</param>
    /// <param name="parameters">验证参数（未使用）</param>
    /// <returns>验证结果</returns>
    public ValidationResult Validate(object? value, object[]? parameters = null)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        var directoryPath = value.ToString();
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return ValidationResult.Error("目录路径不能为空");
        }

        if (!Directory.Exists(directoryPath))
        {
            return ValidationResult.Error($"目录 '{directoryPath}' 不存在");
        }

        return ValidationResult.Success;
    }
}
