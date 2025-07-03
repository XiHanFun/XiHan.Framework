#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumValidationAttribute
// Guid:bff9b62b-113f-462b-8c45-627cf0b93887
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:21:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;

namespace XiHan.Framework.Utils.Enums.Attributes;

/// <summary>
/// 枚举验证特性
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class EnumValidationAttribute<TEnum> : ValidationAttribute where TEnum : struct, Enum
{
    /// <summary>
    /// 是否允许空值
    /// </summary>
    public bool AllowNull { get; set; } = false;

    /// <summary>
    /// 验证方法（返回布尔值）
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <returns>是否有效</returns>
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return AllowNull;
        }

        if (value is TEnum enumValue)
        {
            return Enum.IsDefined(enumValue);
        }

        if (value is int intValue)
        {
            return Enum.IsDefined(typeof(TEnum), intValue);
        }

        return value is string stringValue ? Enum.TryParse<TEnum>(stringValue, out _) : false;
    }

    /// <summary>
    /// 验证方法（返回验证结果）
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <param name="validationContext">验证上下文</param>
    /// <returns>验证结果</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return AllowNull ? ValidationResult.Success : new ValidationResult("枚举值不能为空");
        }

        var validationResult = EnumValidator.IsValid<TEnum>(value);
        return validationResult == ValidationResult.Success ? ValidationResult.Success : validationResult;
    }
}
