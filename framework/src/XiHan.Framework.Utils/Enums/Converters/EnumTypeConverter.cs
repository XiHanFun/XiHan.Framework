#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:EnumTypeConverter
// Guid:5bd78c69-d5bf-4ce2-a0cc-1b53f63989e0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/7/4 3:07:21
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Globalization;

namespace XiHan.Framework.Utils.Enums.Converters;

/// <summary>
/// 枚举类型转换器
/// </summary>
/// <typeparam name="TEnum">枚举类型</typeparam>
public class EnumTypeConverter<TEnum> : TypeConverter where TEnum : struct, Enum
{
    /// <summary>
    /// 是否可以从指定类型转换
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="sourceType">源类型</param>
    /// <returns>是否可以转换</returns>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || sourceType == typeof(int) || base.CanConvertFrom(context, sourceType);
    }

    /// <summary>
    /// 是否可以转换到指定类型
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="destinationType">目标类型</param>
    /// <returns>是否可以转换</returns>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || destinationType == typeof(int) || base.CanConvertTo(context, destinationType);
    }

    /// <summary>
    /// 从指定类型转换
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="culture">区域性</param>
    /// <param name="value">值</param>
    /// <returns>转换结果</returns>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value is string stringValue
            ? EnumHelper.TryGetEnum<TEnum>(stringValue, out var resultString) ? resultString : base.ConvertFrom(context, culture, value)
            : value is int intValue
            ? EnumHelper.TryGetEnum<TEnum>(intValue, out var resultInt) ? resultInt : base.ConvertFrom(context, culture, value)
            : base.ConvertFrom(context, culture, value);
    }

    /// <summary>
    /// 转换到指定类型
    /// </summary>
    /// <param name="context">上下文</param>
    /// <param name="culture">区域性</param>
    /// <param name="value">值</param>
    /// <param name="destinationType">目标类型</param>
    /// <returns>转换结果</returns>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is TEnum enumValue)
        {
            if (destinationType == typeof(string))
            {
                return enumValue.GetName();
            }

            if (destinationType == typeof(int))
            {
                return enumValue.GetValue();
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
