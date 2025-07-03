#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumValidator
// Guid:0a12b528-1de2-4e23-bf99-6b7e4428bd98
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/2 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace XiHan.Framework.Utils.Enums;

/// <summary>
/// 枚举验证器
/// </summary>
public static class EnumValidator
{
    #region 验证方法

    /// <summary>
    /// 验证枚举值是否有效
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>验证结果</returns>
    public static ValidationResult IsValid<TEnum>(object? value) where TEnum : struct, Enum
    {
        if (value is null)
        {
            return new ValidationResult("枚举值不能为空");
        }

        if (value is TEnum enumValue)
        {
            return Enum.IsDefined(enumValue)
                ? ValidationResult.Success!
                : new ValidationResult($"枚举值 {enumValue} 不在 {typeof(TEnum).Name} 中定义");
        }

        if (value is int intValue)
        {
            return Enum.IsDefined(typeof(TEnum), intValue)
                ? ValidationResult.Success!
                : new ValidationResult($"整数值 {intValue} 不在 {typeof(TEnum).Name} 中定义");
        }

        return value is string stringValue
            ? Enum.TryParse<TEnum>(stringValue, out _)
                ? ValidationResult.Success!
                : new ValidationResult($"字符串值 '{stringValue}' 不能转换为 {typeof(TEnum).Name}")
            : new ValidationResult($"无法将类型 {value.GetType().Name} 转换为 {typeof(TEnum).Name}");
    }

    /// <summary>
    /// 验证枚举值是否有效（简化版本）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>是否有效</returns>
    public static bool IsValid<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        return Enum.IsDefined(value);
    }

    /// <summary>
    /// 验证整数值是否为有效的枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">整数值</param>
    /// <returns>是否有效</returns>
    public static bool IsValid<TEnum>(int value) where TEnum : struct, Enum
    {
        return Enum.IsDefined(typeof(TEnum), value);
    }

    /// <summary>
    /// 验证字符串是否为有效的枚举名称
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="name">枚举名称</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>是否有效</returns>
    public static bool IsValid<TEnum>(string name, bool ignoreCase = false) where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(name, ignoreCase, out _);
    }

    /// <summary>
    /// 验证描述是否为有效的枚举描述
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="description">枚举描述</param>
    /// <returns>是否有效</returns>
    public static bool IsValidDescription<TEnum>(string description) where TEnum : struct, Enum
    {
        return EnumHelper.IsDescriptionDefined<TEnum>(description);
    }

    #endregion 验证方法

    #region 批量验证

    /// <summary>
    /// 批量验证枚举值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="values">枚举值集合</param>
    /// <returns>验证结果字典</returns>
    public static Dictionary<TEnum, bool> ValidateMany<TEnum>(IEnumerable<TEnum> values) where TEnum : struct, Enum
    {
        return values.ToDictionary(value => value, IsValid);
    }

    /// <summary>
    /// 批量验证整数值
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="values">整数值集合</param>
    /// <returns>验证结果字典</returns>
    public static Dictionary<int, bool> ValidateMany<TEnum>(IEnumerable<int> values) where TEnum : struct, Enum
    {
        return values.ToDictionary(value => value, value => IsValid<TEnum>(value));
    }

    /// <summary>
    /// 批量验证字符串名称
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="names">字符串名称集合</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>验证结果字典</returns>
    public static Dictionary<string, bool> ValidateMany<TEnum>(IEnumerable<string> names, bool ignoreCase = false) where TEnum : struct, Enum
    {
        return names.ToDictionary(name => name, name => IsValid<TEnum>(name, ignoreCase));
    }

    #endregion 批量验证

    #region 验证异常

    /// <summary>
    /// 验证枚举值，如果无效则抛出异常
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <param name="paramName">参数名称</param>
    /// <exception cref="ArgumentException">枚举值无效时抛出</exception>
    public static void ValidateOrThrow<TEnum>(TEnum value, string? paramName = null) where TEnum : struct, Enum
    {
        if (!IsValid(value))
        {
            throw new ArgumentException($"枚举值 {value} 不在 {typeof(TEnum).Name} 中定义", paramName);
        }
    }

    /// <summary>
    /// 验证整数值，如果无效则抛出异常
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">整数值</param>
    /// <param name="paramName">参数名称</param>
    /// <exception cref="ArgumentException">整数值无效时抛出</exception>
    public static void ValidateOrThrow<TEnum>(int value, string? paramName = null) where TEnum : struct, Enum
    {
        if (!IsValid<TEnum>(value))
        {
            throw new ArgumentException($"整数值 {value} 不在 {typeof(TEnum).Name} 中定义", paramName);
        }
    }

    /// <summary>
    /// 验证字符串名称，如果无效则抛出异常
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="name">字符串名称</param>
    /// <param name="paramName">参数名称</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <exception cref="ArgumentException">字符串名称无效时抛出</exception>
    public static void ValidateOrThrow<TEnum>(string name, string? paramName = null, bool ignoreCase = false) where TEnum : struct, Enum
    {
        if (!IsValid<TEnum>(name, ignoreCase))
        {
            throw new ArgumentException($"字符串值 '{name}' 不能转换为 {typeof(TEnum).Name}", paramName);
        }
    }

    #endregion 验证异常

    #region 范围验证

    /// <summary>
    /// 验证枚举值是否在指定范围内
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>是否在范围内</returns>
    public static bool IsInRange<TEnum>(TEnum value, TEnum min, TEnum max) where TEnum : struct, Enum
    {
        var intValue = value.GetValue();
        var minValue = min.GetValue();
        var maxValue = max.GetValue();

        return intValue >= minValue && intValue <= maxValue;
    }

    /// <summary>
    /// 验证枚举值是否在指定的有效值列表中
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <param name="validValues">有效值列表</param>
    /// <returns>是否在有效值列表中</returns>
    public static bool IsInValidValues<TEnum>(TEnum value, params TEnum[] validValues) where TEnum : struct, Enum
    {
        return validValues.Contains(value);
    }

    /// <summary>
    /// 验证枚举值是否不在指定的无效值列表中
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <param name="invalidValues">无效值列表</param>
    /// <returns>是否不在无效值列表中</returns>
    public static bool IsNotInInvalidValues<TEnum>(TEnum value, params TEnum[] invalidValues) where TEnum : struct, Enum
    {
        return !invalidValues.Contains(value);
    }

    #endregion 范围验证

    #region 标志枚举验证

    /// <summary>
    /// 验证标志枚举是否有效
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <param name="value">枚举值</param>
    /// <returns>是否有效</returns>
    public static bool IsValidFlagEnum<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var enumType = typeof(TEnum);
        if (!enumType.GetCustomAttributes<FlagsAttribute>().Any())
        {
            return false; // 不是标志枚举
        }

        var intValue = value.GetValue();
        var validValues = EnumHelper<TEnum>.GetValues().Select(v => v.GetValue()).ToArray();

        // 检查是否是有效的标志组合
        return IsValidFlagCombination(intValue, validValues);
    }

    /// <summary>
    /// 检查是否是有效的标志组合
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="validFlags">有效标志</param>
    /// <returns>是否有效</returns>
    private static bool IsValidFlagCombination(int value, int[] validFlags)
    {
        if (value == 0)
        {
            return true; // 0 通常是有效的标志值
        }

        var remainingValue = value;
        foreach (var flag in validFlags.Where(f => f != 0).OrderByDescending(f => f))
        {
            if ((remainingValue & flag) == flag)
            {
                remainingValue &= ~flag;
            }
        }

        return remainingValue == 0;
    }

    #endregion 标志枚举验证
}
