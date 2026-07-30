// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace XiHan.Framework.Validation.Abstractions;

/// <summary>
/// 存在验证错误的接口
/// </summary>
public interface IHasValidationErrors
{
    /// <summary>
    /// 验证错误列表
    /// </summary>
    IList<ValidationResult> ValidationErrors { get; }
}
