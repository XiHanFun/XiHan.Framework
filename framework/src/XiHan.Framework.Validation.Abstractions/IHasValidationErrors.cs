#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IHasValidationErrors
// Guid:6c9b789e-6594-4914-ba6b-2292770ce735
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 06:48:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
