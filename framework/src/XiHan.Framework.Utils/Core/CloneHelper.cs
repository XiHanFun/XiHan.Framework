#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CloneHelper
// Guid:5f4e3d2c-1b0a-9f8e-7d6c-5b4a3f2e1d0c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/20 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 对象克隆帮助类
/// </summary>
public static class CloneHelper
{
    #region 深拷贝

    /// <summary>
    /// 深度克隆对象（使用反射递归复制）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="source">源对象</param>
    /// <returns>克隆后的对象</returns>
    public static T? DeepCopy<T>(T? source)
    {
        if (source == null)
        {
            return default;
        }

        return (T)DeepCopy(source, new Dictionary<object, object>(CompareHelper.ReferenceEqualityComparer.Instance))!;
    }

    /// <summary>
    /// 使用 JSON 序列化进行深度克隆
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="source">源对象</param>
    /// <param name="options">JSON 序列化选项</param>
    /// <returns>克隆后的对象</returns>
    public static T? DeepCopyJson<T>(T? source, JsonSerializerOptions? options = null)
    {
        if (source == null)
        {
            return default;
        }

        try
        {
            var json = JsonSerializer.Serialize(source, options);
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch
        {
            // 如果 JSON 序列化失败，回退到反射深拷贝
            return DeepCopy(source);
        }
    }

    /// <summary>
    /// 深度克隆对象（内部方法，用于处理循环引用）
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="cloneMap">已克隆对象映射，用于处理循环引用</param>
    /// <returns>克隆后的对象</returns>
    private static object? DeepCopy(object? source, Dictionary<object, object> cloneMap)
    {
        if (source == null)
        {
            return null;
        }

        var type = source.GetType();

        // 处理基本类型、字符串和其他不可变类型
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(Guid) ||
            type.IsEnum)
        {
            return source;
        }

        // 检查是否已经克隆过（处理循环引用）
        if (cloneMap.TryGetValue(source, out var existingClone))
        {
            return existingClone;
        }

        // 处理可空类型
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return source;
        }

        // 处理数组
        if (type.IsArray)
        {
            return DeepCopyArray((Array)source, cloneMap);
        }

        // 处理字典
        if (source is IDictionary dictionary)
        {
            return DeepCopyDictionary(dictionary, cloneMap);
        }

        // 处理列表和其他集合
        if (source is IList list)
        {
            return DeepCopyList(list, cloneMap);
        }

        // 处理泛型集合
        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            return DeepCopyGenericCollection(source, cloneMap);
        }

        // 处理自定义对象
        return DeepCopyObject(source, cloneMap);
    }

    /// <summary>
    /// 深度克隆数组
    /// </summary>
    /// <param name="source">源数组</param>
    /// <param name="cloneMap">克隆映射</param>
    /// <returns>克隆后的数组</returns>
    private static Array DeepCopyArray(Array source, Dictionary<object, object> cloneMap)
    {
        var elementType = source.GetType().GetElementType()!;
        var cloned = Array.CreateInstance(elementType, source.Length);
        cloneMap[source] = cloned;

        for (var i = 0; i < source.Length; i++)
        {
            cloned.SetValue(DeepCopy(source.GetValue(i), cloneMap), i);
        }

        return cloned;
    }

    /// <summary>
    /// 深度克隆字典
    /// </summary>
    /// <param name="source">源字典</param>
    /// <param name="cloneMap">克隆映射</param>
    /// <returns>克隆后的字典</returns>
    private static IDictionary DeepCopyDictionary(IDictionary source, Dictionary<object, object> cloneMap)
    {
        var type = source.GetType();
        var cloned = (IDictionary)Activator.CreateInstance(type)!;
        cloneMap[source] = cloned;

        foreach (DictionaryEntry entry in source)
        {
            var clonedKey = DeepCopy(entry.Key, cloneMap);
            if (clonedKey != null)
            {
                cloned[clonedKey] = DeepCopy(entry.Value, cloneMap);
            }
        }

        return cloned;
    }

    /// <summary>
    /// 深度克隆列表
    /// </summary>
    /// <param name="source">源列表</param>
    /// <param name="cloneMap">克隆映射</param>
    /// <returns>克隆后的列表</returns>
    private static IList DeepCopyList(IList source, Dictionary<object, object> cloneMap)
    {
        var type = source.GetType();
        var cloned = (IList)Activator.CreateInstance(type)!;
        cloneMap[source] = cloned;

        foreach (var item in source)
        {
            cloned.Add(DeepCopy(item, cloneMap));
        }

        return cloned;
    }

    /// <summary>
    /// 深度克隆泛型集合
    /// </summary>
    /// <param name="source">源集合</param>
    /// <param name="cloneMap">克隆映射</param>
    /// <returns>克隆后的集合</returns>
    private static object DeepCopyGenericCollection(object source, Dictionary<object, object> cloneMap)
    {
        var type = source.GetType();
        var cloned = Activator.CreateInstance(type);
        if (cloned == null)
        {
            return source;
        }

        cloneMap[source] = cloned;

        // 如果有 Add 方法，使用 Add 方法添加元素
        var addMethod = type.GetMethod("Add");
        if (addMethod != null && source is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                addMethod.Invoke(cloned, [DeepCopy(item, cloneMap)]);
            }
        }

        return cloned;
    }

    /// <summary>
    /// 深度克隆自定义对象
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="cloneMap">克隆映射</param>
    /// <returns>克隆后的对象</returns>
    private static object DeepCopyObject(object source, Dictionary<object, object> cloneMap)
    {
        var type = source.GetType();
        var cloned = Activator.CreateInstance(type);
        if (cloned == null)
        {
            return source;
        }

        cloneMap[source] = cloned;

        // 复制字段
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            if (field.IsInitOnly || field.IsLiteral)
            {
                continue;
            }

            var fieldValue = field.GetValue(source);
            field.SetValue(cloned, DeepCopy(fieldValue, cloneMap));
        }

        // 复制可写属性
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var property in properties)
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            try
            {
                var propertyValue = property.GetValue(source);
                property.SetValue(cloned, DeepCopy(propertyValue, cloneMap));
            }
            catch
            {
                // 忽略无法设置的属性
                continue;
            }
        }

        return cloned;
    }

    #endregion

    #region 浅拷贝

    /// <summary>
    /// 浅拷贝对象（仅复制值类型字段和引用类型的引用）
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="source">源对象</param>
    /// <returns>浅拷贝后的对象</returns>
    public static T? ShallowCopy<T>(T? source) where T : class
    {
        if (source == null)
        {
            return null;
        }

        var type = typeof(T);
        var cloned = Activator.CreateInstance(type);
        if (cloned == null)
        {
            return null;
        }

        // 复制字段
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            if (field.IsInitOnly || field.IsLiteral)
            {
                continue;
            }

            field.SetValue(cloned, field.GetValue(source));
        }

        return (T)cloned;
    }

    /// <summary>
    /// 使用 MemberwiseClone 进行浅拷贝
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="source">源对象</param>
    /// <returns>浅拷贝后的对象</returns>
    public static T? MemberwiseClone<T>(T? source) where T : class
    {
        if (source == null)
        {
            return null;
        }

        var cloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        return (T?)cloneMethod?.Invoke(source, null);
    }

    #endregion

    #region 属性复制

    /// <summary>
    /// 将源对象的属性复制到目标对象
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="target">目标对象</param>
    /// <param name="copyNullValues">是否复制 null 值</param>
    /// <param name="ignoreCase">是否忽略属性名称大小写</param>
    public static void CopyProperties(object source, object target, bool copyNullValues = true, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var sourceType = source.GetType();
        var targetType = target.GetType();

        var sourceProperties = sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, p => p, ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var targetProperties = targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0);

        foreach (var targetProperty in targetProperties)
        {
            if (sourceProperties.TryGetValue(targetProperty.Name, out var sourceProperty))
            {
                if (IsAssignable(sourceProperty.PropertyType, targetProperty.PropertyType))
                {
                    try
                    {
                        var value = sourceProperty.GetValue(source);
                        if (copyNullValues || value != null)
                        {
                            targetProperty.SetValue(target, ConvertValue(value, targetProperty.PropertyType));
                        }
                    }
                    catch
                    {
                        // 忽略转换失败的属性
                        continue;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 将源对象的字段复制到目标对象
    /// </summary>
    /// <param name="source">源对象</param>
    /// <param name="target">目标对象</param>
    /// <param name="copyNullValues">是否复制 null 值</param>
    /// <param name="ignoreCase">是否忽略字段名称大小写</param>
    public static void CopyFields(object source, object target, bool copyNullValues = true, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var sourceType = source.GetType();
        var targetType = target.GetType();

        var sourceFields = sourceType.GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(f => !f.IsInitOnly && !f.IsLiteral)
            .ToDictionary(f => f.Name, f => f, ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var targetFields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(f => !f.IsInitOnly && !f.IsLiteral);

        foreach (var targetField in targetFields)
        {
            if (sourceFields.TryGetValue(targetField.Name, out var sourceField))
            {
                if (IsAssignable(sourceField.FieldType, targetField.FieldType))
                {
                    try
                    {
                        var value = sourceField.GetValue(source);
                        if (copyNullValues || value != null)
                        {
                            targetField.SetValue(target, ConvertValue(value, targetField.FieldType));
                        }
                    }
                    catch
                    {
                        // 忽略转换失败的字段
                        continue;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 对象映射（创建新的目标对象并复制属性）
    /// </summary>
    /// <typeparam name="TTarget">目标类型</typeparam>
    /// <param name="source">源对象</param>
    /// <param name="copyNullValues">是否复制 null 值</param>
    /// <param name="ignoreCase">是否忽略属性名称大小写</param>
    /// <returns>映射后的目标对象</returns>
    public static TTarget? MapObject<TTarget>(object? source, bool copyNullValues = true, bool ignoreCase = false)
        where TTarget : class, new()
    {
        if (source == null)
        {
            return null;
        }

        var target = new TTarget();
        CopyProperties(source, target, copyNullValues, ignoreCase);
        return target;
    }

    /// <summary>
    /// 对象映射（复制到现有的目标对象）
    /// </summary>
    /// <typeparam name="TTarget">目标类型</typeparam>
    /// <param name="source">源对象</param>
    /// <param name="target">目标对象</param>
    /// <param name="copyNullValues">是否复制 null 值</param>
    /// <param name="ignoreCase">是否忽略属性名称大小写</param>
    /// <returns>目标对象</returns>
    public static TTarget MapObject<TTarget>(object source, TTarget target, bool copyNullValues = true, bool ignoreCase = false)
        where TTarget : class
    {
        CopyProperties(source, target, copyNullValues, ignoreCase);
        return target;
    }

    #endregion

    #region 集合克隆

    /// <summary>
    /// 克隆列表
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="source">源列表</param>
    /// <param name="deepCopy">是否进行深拷贝</param>
    /// <returns>克隆后的列表</returns>
    public static List<T>? CloneList<T>(IEnumerable<T>? source, bool deepCopy = false)
    {
        if (source == null)
        {
            return null;
        }

        if (deepCopy)
        {
            var value = new List<T>();
            foreach (var item in source)
            {
                var clonedItem = DeepCopy(item);
                // 如果 T 是不可为 null 的值类型，则跳过 null
                if (clonedItem is T typedItem)
                {
                    value.Add(typedItem);
                }
                // 如果 T 是引用类型，允许 null
                else if (default(T) == null) // T 是引用类型
                {
                    value.Add((T?)clonedItem!);
                }
                // 如果 T 是不可为 null 的值类型且 DeepCopy 返回 null，则不添加
            }
            return value;
        }
        else
        {
            return [.. source];
        }
    }

    /// <summary>
    /// 克隆数组
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="source">源数组</param>
    /// <param name="deepCopy">是否进行深拷贝</param>
    /// <returns>克隆后的数组</returns>
    public static T[]? CloneArray<T>(T[]? source, bool deepCopy = false)
    {
        if (source == null)
        {
            return null;
        }

        if (deepCopy)
        {
            return [.. source.Select(item => DeepCopy(item)!)];
        }
        else
        {
            var cloned = new T[source.Length];
            Array.Copy(source, cloned, source.Length);
            return cloned;
        }
    }

    /// <summary>
    /// 克隆字典
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="source">源字典</param>
    /// <param name="deepCopy">是否进行深拷贝</param>
    /// <returns>克隆后的字典</returns>
    public static Dictionary<TKey, TValue>? CloneDictionary<TKey, TValue>(IDictionary<TKey, TValue>? source, bool deepCopy = false)
        where TKey : notnull
    {
        if (source == null)
        {
            return null;
        }

        if (deepCopy)
        {
            return source.ToDictionary(
                kvp => DeepCopy(kvp.Key)!,
                kvp => DeepCopy(kvp.Value)!
            );
        }
        else
        {
            return new Dictionary<TKey, TValue>(source);
        }
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 判断源类型是否可以赋值给目标类型
    /// </summary>
    /// <param name="sourceType">源类型</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>是否可以赋值</returns>
    private static bool IsAssignable(Type sourceType, Type targetType)
    {
        if (targetType.IsAssignableFrom(sourceType))
        {
            return true;
        }

        // 处理可空类型
        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var underlyingType = Nullable.GetUnderlyingType(targetType)!;
            return sourceType == underlyingType || underlyingType.IsAssignableFrom(sourceType);
        }

        return false;
    }

    /// <summary>
    /// 转换值到目标类型
    /// </summary>
    /// <param name="value">源值</param>
    /// <param name="targetType">目标类型</param>
    /// <returns>转换后的值</returns>
    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null)
        {
            return null;
        }

        var sourceType = value.GetType();

        if (targetType.IsAssignableFrom(sourceType))
        {
            return value;
        }

        // 处理可空类型
        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null)
        {
            return ConvertValue(value, underlyingType);
        }

        // 尝试使用 Convert.ChangeType
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return value;
        }
    }

    #endregion
}
