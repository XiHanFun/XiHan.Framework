#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:RangeValidator
// Guid:6220e6a5-9896-41bf-a42a-676a0546c6f4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/10/17 05:06:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.DevTools.CommandLine.Validators;

/// <summary>
/// 范围验证器
/// </summary>
public class RangeValidator : IValidator
{
    /// <summary>
    /// 验证值是否在指定范围内
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="parameters">验证参数 [minimum, maximum]</param>
    /// <returns>验证结果</returns>
    public ValidationResult Validate(object? value, object[]? parameters = null)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (parameters == null || parameters.Length < 2)
        {
            return ValidationResult.Error("范围验证需要最小值和最大值参数");
        }

        try
        {
            if (value is not IComparable comparable)
            {
                return ValidationResult.Error("值不支持比较操作");
            }

            if (parameters[0] is IComparable minimum && comparable.CompareTo(minimum) < 0)
            {
                return ValidationResult.Error($"值 {value} 小于最小值 {minimum}");
            }

            if (parameters[1] is IComparable maximum && comparable.CompareTo(maximum) > 0)
            {
                return ValidationResult.Error($"值 {value} 大于最大值 {maximum}");
            }

            return ValidationResult.Success;
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"范围验证失败: {ex.Message}");
        }
    }
}
