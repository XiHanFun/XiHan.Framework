#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IValidator
// Guid:4b594667-fe60-4dbf-bcca-3b3db1e92230
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 5:04:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
