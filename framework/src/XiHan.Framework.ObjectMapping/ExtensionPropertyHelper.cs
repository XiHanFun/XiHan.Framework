#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExtensionPropertyHelper
// Guid:8554bb87-dafc-4619-8e5c-29ad18db8f7d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 7:25:32
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel.DataAnnotations;
using XiHan.Framework.Utils.Core;

namespace XiHan.Framework.ObjectMapping;

/// <summary>
/// 扩展属性帮助类
/// 提供扩展属性的默认特性和默认值处理的静态方法集合
/// </summary>
public static class ExtensionPropertyHelper
{
    /// <summary>
    /// 根据类型获取默认的验证特性集合
    /// 为非空基元类型和枚举类型自动添加相应的验证特性
    /// </summary>
    /// <param name="type">属性类型</param>
    /// <returns>默认特性的枚举集合</returns>
    /// <exception cref="ArgumentNullException">当 type 为 null 时</exception>
    public static IEnumerable<Attribute> GetDefaultAttributes(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (TypeHelper.IsNonNullablePrimitiveType(type) || type.IsEnum)
        {
            yield return new RequiredAttribute();
        }

        if (type.IsEnum)
        {
            yield return new EnumDataTypeAttribute(type);
        }
    }

    /// <summary>
    /// 获取属性的默认值
    /// 按优先级顺序：defaultValueFactory > defaultValue > 类型默认值
    /// </summary>
    /// <param name="propertyType">属性类型</param>
    /// <param name="defaultValueFactory">默认值工厂方法，优先使用</param>
    /// <param name="defaultValue">指定的默认值，次优先使用</param>
    /// <returns>计算得出的默认值</returns>
    /// <exception cref="ArgumentNullException">当 propertyType 为 null 时</exception>
    public static object? GetDefaultValue(Type propertyType, Func<object>? defaultValueFactory, object? defaultValue)
    {
        ArgumentNullException.ThrowIfNull(propertyType);

        if (defaultValueFactory != null)
        {
            return defaultValueFactory();
        }

        return defaultValue ?? TypeHelper.GetDefaultValue(propertyType);
    }
}
