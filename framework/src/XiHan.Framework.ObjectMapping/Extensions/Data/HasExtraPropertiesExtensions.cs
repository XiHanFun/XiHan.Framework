#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HasExtraPropertiesExtensions
// Guid:f70051c1-ed4d-42d3-ba6a-a6ac02313472
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 04:59:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.ComponentModel;
using System.Globalization;
using XiHan.Framework.Core.Exceptions;
using XiHan.Framework.ObjectMapping.Extensions;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Core;

namespace XiHan.Framework.ObjectMapping.Extensions.Data;

/// <summary>
/// IHasExtraProperties 接口扩展方法类
/// 提供额外属性的操作方法，包括获取、设置、删除和类型转换等功能
/// </summary>
public static class HasExtraPropertiesExtensions
{
    /// <summary>
    /// 检查是否存在指定名称的额外属性
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="name">属性名称</param>
    /// <returns>如果存在指定属性则返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当 source 或 name 为 null 时</exception>
    public static bool HasProperty(this IHasExtraProperties source, string name)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return source.ExtraProperties.ContainsKey(name);
    }

    /// <summary>
    /// 获取指定名称的额外属性值
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="name">属性名称</param>
    /// <param name="defaultValue">默认值，当属性不存在时返回</param>
    /// <returns>属性值或默认值</returns>
    /// <exception cref="ArgumentNullException">当 source 或 name 为 null 时</exception>
    public static object? GetProperty(this IHasExtraProperties source, string name, object? defaultValue = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return source.ExtraProperties.GetOrDefault(name)
               ?? defaultValue;
    }

    /// <summary>
    /// 获取指定名称和类型的额外属性值
    /// </summary>
    /// <typeparam name="TProperty">属性类型</typeparam>
    /// <param name="source">源对象</param>
    /// <param name="name">属性名称</param>
    /// <param name="defaultValue">默认值，当属性不存在时返回</param>
    /// <returns>转换为指定类型的属性值或默认值</returns>
    /// <exception cref="ArgumentNullException">当 source 或 name 为 null 时</exception>
    /// <exception cref="XiHanException">当类型不支持或转换失败时</exception>
    public static TProperty? GetProperty<TProperty>(this IHasExtraProperties source, string name, TProperty? defaultValue = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var value = source.GetProperty(name);
        if (value == null)
        {
            return defaultValue;
        }

        if (TypeHelper.IsPrimitiveExtended(typeof(TProperty), includeEnums: true))
        {
            var conversionType = typeof(TProperty);
            if (TypeHelper.IsNullable(conversionType))
            {
                conversionType = conversionType.GetFirstGenericArgumentIfNullable();
            }

            return conversionType == typeof(Guid)
                ? (TProperty)TypeDescriptor.GetConverter(conversionType).ConvertFromInvariantString(value.ToString()!)!
                : conversionType.IsEnum
                ? (TProperty)Enum.Parse(conversionType, value.ToString()!)
                : (TProperty)Convert.ChangeType(value, conversionType, CultureInfo.InvariantCulture);
        }

        throw new XiHanException("GetProperty<TProperty> 不支持非原始类型。请使用非泛型 GetProperty 方法并手动处理类型转换。");
    }

    /// <summary>
    /// 设置指定名称的额外属性值
    /// </summary>
    /// <typeparam name="TSource">源对象类型</typeparam>
    /// <param name="source">源对象</param>
    /// <param name="name">属性名称</param>
    /// <param name="value">属性值</param>
    /// <param name="validate">是否进行验证</param>
    /// <returns>源对象（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">当 source 或 name 为 null 时</exception>
    public static TSource SetProperty<TSource>(
        this TSource source,
        string name,
        object? value,
        bool validate = true)
        where TSource : IHasExtraProperties
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (validate)
        {
            ExtensibleObjectValidator.CheckValue(source, name, value);
        }

        source.ExtraProperties[name] = value;

        return source;
    }

    /// <summary>
    /// 移除指定名称的额外属性
    /// </summary>
    /// <typeparam name="TSource">源对象类型</typeparam>
    /// <param name="source">源对象</param>
    /// <param name="name">属性名称</param>
    /// <returns>源对象（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">当 source 或 name 为 null 时</exception>
    public static TSource RemoveProperty<TSource>(this TSource source, string name)
        where TSource : IHasExtraProperties
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        source.ExtraProperties.Remove(name);
        return source;
    }

    /// <summary>
    /// 为额外属性设置默认值
    /// </summary>
    /// <typeparam name="TSource">源对象类型</typeparam>
    /// <param name="source">源对象</param>
    /// <param name="objectType">对象类型，如果为 null 则使用 TSource 类型</param>
    /// <returns>源对象（支持链式调用）</returns>
    /// <exception cref="ArgumentNullException">当 source 为 null 时</exception>
    public static TSource SetDefaultsForExtraProperties<TSource>(this TSource source, Type? objectType = null)
        where TSource : IHasExtraProperties
    {
        ArgumentNullException.ThrowIfNull(source);

        if (objectType == null)
        {
            objectType = typeof(TSource);
        }

        var properties = ObjectExtensionManager.Instance.GetProperties(objectType);

        foreach (var property in properties)
        {
            if (source.HasProperty(property.Name))
            {
                continue;
            }

            source.ExtraProperties[property.Name] = property.GetDefaultValue();
        }

        return source;
    }

    /// <summary>
    /// 为指定对象设置额外属性的默认值
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="objectType">对象类型</param>
    /// <exception cref="ArgumentNullException">当参数为 null 时</exception>
    /// <exception cref="ArgumentException">当 source 不实现 IHasExtraProperties 接口时</exception>
    public static void SetDefaultsForExtraProperties(object source, Type objectType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(objectType);

        if (source is not IHasExtraProperties)
        {
            throw new ArgumentException($"给定的 {nameof(source)} 对象未实现 {nameof(IHasExtraProperties)} 接口", nameof(source));
        }

        ((IHasExtraProperties)source).SetDefaultsForExtraProperties(objectType);
    }

    /// <summary>
    /// 将额外属性的值设置到对应的常规属性中
    /// </summary>
    /// <param name="source">源对象</param>
    /// <exception cref="ArgumentNullException">当 source 为 null 时</exception>
    public static void SetExtraPropertiesToRegularProperties(this IHasExtraProperties source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var properties = source.GetType().GetProperties()
            .Where(x => source.ExtraProperties.ContainsKey(x.Name)
                        && x.GetSetMethod(true) != null)
            .ToList();

        foreach (var property in properties)
        {
            property.SetValue(source, source.ExtraProperties[property.Name]);
            source.RemoveProperty(property.Name);
        }
    }

    /// <summary>
    /// 检查两个对象是否具有相同的额外属性
    /// </summary>
    /// <param name="source">第一个对象</param>
    /// <param name="other">要比较的对象</param>
    /// <returns>如果两个对象具有相同的额外属性则返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当任一对象为 null 时</exception>
    public static bool HasSameExtraProperties(
         this IHasExtraProperties source,
         IHasExtraProperties other)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(other);

        return source.ExtraProperties.HasSameItems(other.ExtraProperties);
    }
}
