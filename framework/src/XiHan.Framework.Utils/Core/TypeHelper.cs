#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TypeHelper
// Guid:1e15e82d-4a46-4f37-9f99-f3af5d8da83e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 05:00:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using XiHan.Framework.Utils.Collections;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Localization;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 类型辅助工具类
/// 提供类型检查、转换、泛型处理等常用功能
/// </summary>
public static class TypeHelper
{
    /// <summary>
    /// 浮点数类型集合
    /// </summary>
    private static readonly HashSet<Type> FloatingTypes =
        [
            typeof(float),
            typeof(double),
            typeof(decimal)
        ];

    /// <summary>
    /// 非可空基元类型集合
    /// </summary>
    private static readonly HashSet<Type> NonNullablePrimitiveTypes =
        [
            typeof(byte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(sbyte),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(bool),
            typeof(float),
            typeof(decimal),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(Guid)
        ];

    /// <summary>
    /// 检查指定类型是否为非可空基元类型
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <returns>如果是非可空基元类型则返回 true，否则返回 false</returns>
    public static bool IsNonNullablePrimitiveType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.IsPrimitive || NonNullablePrimitiveTypes.Contains(type);
    }

    /// <summary>
    /// 检查指定对象是否为 Func 委托类型
    /// </summary>
    /// <param name="obj">要检查的对象</param>
    /// <returns>如果是 Func 委托类型则返回 true，否则返回 false</returns>
    public static bool IsFunc(object? obj)
    {
        if (obj == null)
        {
            return false;
        }

        var type = obj.GetType();
        return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Func<>);
    }

    /// <summary>
    /// 检查指定对象是否为指定返回类型的 Func 委托
    /// </summary>
    /// <typeparam name="TReturn">Func 委托的返回类型</typeparam>
    /// <param name="obj">要检查的对象</param>
    /// <returns>如果是指定返回类型的 Func 委托则返回 true，否则返回 false</returns>
    public static bool IsFunc<TReturn>(object? obj)
    {
        return obj != null && obj.GetType() == typeof(Func<TReturn>);
    }

    /// <summary>
    /// 检查指定类型是否为扩展基元类型（包括常用的值类型和字符串）
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <param name="includeNullables">是否包含可空类型</param>
    /// <param name="includeEnums">是否包含枚举类型</param>
    /// <returns>如果是扩展基元类型则返回 true，否则返回 false</returns>
    public static bool IsPrimitiveExtended(Type type, bool includeNullables = true, bool includeEnums = false)
    {
        ArgumentNullException.ThrowIfNull(type);

        return IsPrimitiveExtendedInternal(type, includeEnums) || (includeNullables && IsNullable(type)
            && type.GenericTypeArguments.Length != 0
            && IsPrimitiveExtendedInternal(type.GenericTypeArguments[0], includeEnums));
    }

    /// <summary>
    /// 检查指定类型是否为可空类型 (Nullable&lt;T&gt;)
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <returns>如果是可空类型则返回 true，否则返回 false</returns>
    public static bool IsNullable(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    /// <summary>
    /// 检查指定类型是否为可空枚举类型
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <returns>如果是可空枚举类型则返回 true，否则返回 false</returns>
    public static bool IsNullableEnum(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.IsGenericType &&
               type.GetGenericTypeDefinition() == typeof(Nullable<>) &&
               type.GenericTypeArguments.Length == 1 &&
               type.GenericTypeArguments[0].IsEnum;
    }

    /// <summary>
    /// 如果类型是可空类型，获取其第一个泛型参数；否则返回原类型
    /// </summary>
    /// <param name="t">要处理的类型</param>
    /// <returns>底层类型或原类型</returns>
    public static Type GetFirstGenericArgumentIfNullable(this Type t)
    {
        ArgumentNullException.ThrowIfNull(t);

        return t.GetGenericArguments().Length > 0 && t.GetGenericTypeDefinition() == typeof(Nullable<>) ? t.GetGenericArguments().First() : t;
    }

    /// <summary>
    /// 检查指定类型是否为可枚举类型，并获取元素类型
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <param name="itemType">输出参数：元素类型</param>
    /// <param name="includePrimitives">是否包含基元类型</param>
    /// <returns>如果是可枚举类型则返回 true，否则返回 false</returns>
    public static bool IsEnumerable(Type type, out Type? itemType, bool includePrimitives = true)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!includePrimitives && IsPrimitiveExtended(type))
        {
            itemType = null;
            return false;
        }

        var enumerableTypes = ReflectionHelper.GetImplementedGenericTypes(type, typeof(IEnumerable<>));
        if (enumerableTypes.Count == 1)
        {
            itemType = enumerableTypes[0].GenericTypeArguments[0];
            return true;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            itemType = typeof(object);
            return true;
        }

        itemType = null;
        return false;
    }

    /// <summary>
    /// 检查指定类型是否为字典类型，并获取键值类型
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <param name="keyType">输出参数：键类型</param>
    /// <param name="valueType">输出参数：值类型</param>
    /// <returns>如果是字典类型则返回 true，否则返回 false</returns>
    public static bool IsDictionary(Type type, out Type? keyType, out Type? valueType)
    {
        ArgumentNullException.ThrowIfNull(type);

        var dictionaryTypes = ReflectionHelper
            .GetImplementedGenericTypes(
                type,
                typeof(IDictionary<,>)
            );

        if (dictionaryTypes.Count == 1)
        {
            keyType = dictionaryTypes[0].GenericTypeArguments[0];
            valueType = dictionaryTypes[0].GenericTypeArguments[1];
            return true;
        }

        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            keyType = typeof(object);
            valueType = typeof(object);
            return true;
        }

        keyType = null;
        valueType = null;

        return false;
    }

    /// <summary>
    /// 获取指定泛型类型的默认值
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <returns>类型 T 的默认值</returns>
    public static T? GetDefaultValue<T>()
    {
        return default;
    }

    /// <summary>
    /// 获取指定类型的默认值
    /// </summary>
    /// <param name="type">要获取默认值的类型</param>
    /// <returns>指定类型的默认值</returns>
    public static object? GetDefaultValue(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>
    /// 获取类型的完整名称，处理可空类型和泛型类型的特殊格式
    /// </summary>
    /// <param name="type">要获取完整名称的类型</param>
    /// <returns>格式化后的类型完整名称</returns>
    public static string GetFullNameHandlingNullableAndGenerics(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return type.GenericTypeArguments[0].FullName + "?";
        }

        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            var genericTypeFullName = genericType.FullName!;
            return $"{genericTypeFullName.Left(genericTypeFullName.IndexOf('`'))}<{type.GenericTypeArguments.Select(GetFullNameHandlingNullableAndGenerics).JoinAsString(",")}>";
        }

        return type.FullName ?? type.Name;
    }

    /// <summary>
    /// 获取类型的简化名称，用于前端展示或序列化
    /// </summary>
    /// <param name="type">要获取简化名称的类型</param>
    /// <returns>简化后的类型名称</returns>
    public static string GetSimplifiedName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return GetSimplifiedName(type.GenericTypeArguments[0]) + "?";
        }

        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            var genericTypeFullName = genericType.FullName!;
            return $"{genericTypeFullName.Left(genericTypeFullName.IndexOf('`'))}<{type.GenericTypeArguments.Select(GetSimplifiedName).JoinAsString(",")}>";
        }

        return type switch
        {
            _ when type == typeof(string) => "string",
            _ when type == typeof(int) => "number",
            _ when type == typeof(long) => "number",
            _ when type == typeof(bool) => "boolean",
            _ when type == typeof(char) => "string",
            _ when type == typeof(double) => "number",
            _ when type == typeof(float) => "number",
            _ when type == typeof(decimal) => "number",
            _ when type == typeof(DateTime) => "string",
            _ when type == typeof(DateTimeOffset) => "string",
            _ when type.FullName == "System.DateOnly" => "string",
            _ when type.FullName == "System.TimeOnly" => "string",
            _ when type == typeof(TimeSpan) => "string",
            _ when type == typeof(Guid) => "string",
            _ when type == typeof(byte) => "number",
            _ when type == typeof(sbyte) => "number",
            _ when type == typeof(short) => "number",
            _ when type == typeof(ushort) => "number",
            _ when type == typeof(uint) => "number",
            _ when type == typeof(ulong) => "number",
            _ when type == typeof(nint) => "number",
            _ when type == typeof(nuint) => "number",
            _ when type == typeof(object) => "object",
            _ when type.IsEnum => "enum",
            _ => type.FullName ?? type.Name
        };
    }

    /// <summary>
    /// 将字符串转换为指定的泛型类型
    /// </summary>
    /// <typeparam name="TTargetType">目标类型</typeparam>
    /// <param name="value">要转换的字符串值</param>
    /// <returns>转换后的对象，转换失败则返回 null</returns>
    public static object? ConvertFromString<TTargetType>(string value)
    {
        return ConvertFromString(typeof(TTargetType), value);
    }

    /// <summary>
    /// 将字符串转换为指定类型的对象
    /// </summary>
    /// <param name="targetType">目标类型</param>
    /// <param name="value">要转换的字符串值</param>
    /// <returns>转换后的对象，转换失败则返回 null</returns>
    public static object? ConvertFromString(Type targetType, string? value)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (value == null)
        {
            return null;
        }

        var converter = TypeDescriptor.GetConverter(targetType);

        if (IsFloatingType(targetType))
        {
            using (CultureHelper.Use(CultureInfo.InvariantCulture))
            {
                return converter.ConvertFromString(value.Replace(',', '.'));
            }
        }

        return converter.ConvertFromString(value);
    }

    /// <summary>
    /// 检查指定类型是否为浮点数类型
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <param name="includeNullable">是否包含可空浮点数类型</param>
    /// <returns>如果是浮点数类型则返回 true，否则返回 false</returns>
    public static bool IsFloatingType(Type type, bool includeNullable = true)
    {
        ArgumentNullException.ThrowIfNull(type);

        return FloatingTypes.Contains(type) || (includeNullable &&
            IsNullable(type) &&
            type.GenericTypeArguments.Length > 0 &&
            FloatingTypes.Contains(type.GenericTypeArguments[0]));
    }

    /// <summary>
    /// 将对象转换为指定的泛型类型
    /// </summary>
    /// <typeparam name="TTargetType">目标类型</typeparam>
    /// <param name="value">要转换的对象</param>
    /// <returns>转换后的对象</returns>
    /// <exception cref="InvalidOperationException">转换失败时抛出</exception>
    public static object ConvertFrom<TTargetType>(object value)
    {
        return ConvertFrom(typeof(TTargetType), value);
    }

    /// <summary>
    /// 将对象转换为指定类型
    /// </summary>
    /// <param name="targetType">目标类型</param>
    /// <param name="value">要转换的对象</param>
    /// <returns>转换后的对象</returns>
    /// <exception cref="InvalidOperationException">转换失败时抛出</exception>
    public static object ConvertFrom(Type targetType, object value)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(value);

        return TypeDescriptor
            .GetConverter(targetType)
            .ConvertFrom(value)!;
    }

    /// <summary>
    /// 去除可空类型的包装，返回底层类型
    /// </summary>
    /// <param name="type">要处理的类型</param>
    /// <returns>如果是可空类型则返回底层类型，否则返回原类型</returns>
    public static Type StripNullable(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return IsNullable(type)
            ? type.GenericTypeArguments[0]
            : type;
    }

    /// <summary>
    /// 检查对象是否为其类型的默认值
    /// </summary>
    /// <param name="obj">要检查的对象</param>
    /// <returns>如果是默认值则返回 true，否则返回 false</returns>
    public static bool IsDefaultValue(object? obj)
    {
        return obj == null || obj.Equals(GetDefaultValue(obj.GetType()));
    }

    /// <summary>
    /// 内部方法：检查类型是否为扩展基元类型
    /// </summary>
    /// <param name="type">要检查的类型</param>
    /// <param name="includeEnums">是否包含枚举类型</param>
    /// <returns>如果是扩展基元类型则返回 true，否则返回 false</returns>
    private static bool IsPrimitiveExtendedInternal(Type type, bool includeEnums)
    {
        return type.IsPrimitive || (includeEnums && type.IsEnum) ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) ||
            type == typeof(Guid);
    }
}
