// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.DevTools.CommandLine.Validators;

/// <summary>
/// 验证器接口
/// </summary>
public interface IValidator
{
    /// <summary>
    /// 验证值
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameters">验证参数</param>
    /// <returns>验证结果</returns>
    ValidationResult Validate(object? value, object[]? parameters = null);
}
