#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DeepMergeHelper
// Guid:e49e99ba-cfa7-453b-b25f-3aeb949463b6
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/14 14:09:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 深度合并帮助类，用于合并多个对象配置，保留优先级最高的非空值，同时合并集合和字典
/// </summary>
public static class DeepMergeHelper
{
    // 缓存类型属性，提高性能
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = [];

    /// <summary>
    /// 深度合并多个配置，按优先级返回合并后的配置
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    /// <param name="configs">按优先级排序的配置列表，第一个为最高优先级</param>
    /// <returns>合并后的配置</returns>
    public static T DeepMerge<T>(params T[] configs) where T : class, new()
    {
        // 快速路径：没有配置或只有一个配置
        if (configs is null || configs.Length == 0)
        {
            return new T();
        }

        if (configs.Length == 1 && configs[0] is not null)
        {
            return DeepClone(configs[0]) as T ?? new T();
        }

        // 创建结果对象
        var result = new T();
        var type = typeof(T);

        // 从缓存获取属性或添加到缓存
        if (!PropertyCache.TryGetValue(type, out var properties))
        {
            properties = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite)];
            PropertyCache[type] = properties;
        }

        // 处理每个属性
        foreach (var property in properties)
        {
            ProcessProperty(property, result, configs);
        }

        return result;
    }

    /// <summary>
    /// 处理单个属性的合并
    /// </summary>
    private static void ProcessProperty<T>(PropertyInfo property, T result, T[] configs) where T : class
    {
        object? valueToSet = null;
        var valueFound = false;

        // 按优先级顺序检查每个配置
        foreach (var config in configs)
        {
            if (config is null)
            {
                continue;
            }

            // 获取当前配置的属性值
            var value = property.GetValue(config);

            // 检查值是否为空
            if (!IsNullOrDefault(value))
            {
                if (!valueFound)
                {
                    // 首次找到非空值
                    valueToSet = NeedsCloning(value) ? DeepClone(value) : value;
                    valueFound = true;
                }
                else if (CanMerge(valueToSet, value))
                {
                    // 尝试合并值
                    valueToSet = MergeValues(valueToSet, value);
                }
                // 对于已找到的标量值，保持高优先级的值
            }
        }

        // 如果找到了值，设置到结果中
        if (valueFound)
        {
            try
            {
                property.SetValue(result, valueToSet);
            }
            catch (Exception ex) when (ex is ArgumentException or TargetInvocationException)
            {
                // 记录或处理属性设置失败的情况
                ConsoleLogger.Error($"无法设置属性 {property.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 检查两个值是否可以合并
    /// </summary>
    private static bool CanMerge(object? target, object? source)
    {
        return target is not null && source is not null && ((IsMergeableCollection(target) && IsMergeableCollection(source)) ||
               (IsDictionary(target) && IsDictionary(source)) ||
               (IsComplexObject(target) && IsComplexObject(source) && target.GetType() == source.GetType()));
    }

    /// <summary>
    /// 合并两个值
    /// </summary>
    private static object? MergeValues(object? target, object? source)
    {
        if (IsMergeableCollection(target) && IsMergeableCollection(source))
        {
            return MergeCollections(target, source);
        }

        if (IsDictionary(target) && IsDictionary(source))
        {
            return MergeDictionaries(target, source);
        }

        if (IsComplexObject(target) && IsComplexObject(source) && target?.GetType() == source?.GetType())
        {
            return MergeComplexObjects(target, source);
        }

        return target; // 默认返回高优先级值
    }

    /// <summary>
    /// 判断对象是否需要克隆
    /// </summary>
    private static bool NeedsCloning(object? value)
    {
        return value is not null && (IsComplexObject(value) || IsCollection(value) || IsDictionary(value));
    }

    /// <summary>
    /// 判断值是否为空或默认值
    /// </summary>
    private static bool IsNullOrDefault(object? value)
    {
        if (value is null)
        {
            return true;
        }

        var type = value.GetType();

        // 处理可空类型
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return value.Equals(Activator.CreateInstance(type));
        }

        // 处理字符串
        if (value is string strValue)
        {
            return string.IsNullOrEmpty(strValue);
        }

        // 处理集合
        if (value is ICollection collection)
        {
            return collection.Count == 0;
        }

        // 其他类型使用默认相等比较
        try
        {
            return value.Equals(Activator.CreateInstance(type));
        }
        catch
        {
            // 某些类型可能没有默认构造函数
            return false;
        }
    }

    /// <summary>
    /// 是否为可合并的集合类型
    /// </summary>
    private static bool IsMergeableCollection(object? value)
    {
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();

        // 数组或实现了泛型集合接口的类型
        return type.IsArray || (type.IsGenericType && (
            typeof(IList<>).IsAssignableFrom(type.GetGenericTypeDefinition()) ||
            typeof(ICollection<>).IsAssignableFrom(type.GetGenericTypeDefinition()) ||
            typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition())
        ));
    }

    /// <summary>
    /// 是否为字典类型
    /// </summary>
    private static bool IsDictionary(object? value)
    {
        return value is not null and IDictionary;
    }

    /// <summary>
    /// 是否为集合类型
    /// </summary>
    private static bool IsCollection(object? value)
    {
        return value is ICollection;
    }

    /// <summary>
    /// 是否为复杂对象类型
    /// </summary>
    private static bool IsComplexObject(object? value)
    {
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();

        // 排除基本类型、枚举和常见值类型
        return !type.IsPrimitive &&
               !type.IsEnum &&
               type != typeof(string) &&
               type != typeof(DateTime) &&
               type != typeof(DateTimeOffset) &&
               type != typeof(TimeSpan) &&
               type != typeof(decimal) &&
               type != typeof(Guid) &&
               !type.IsArray &&
               value is not ICollection;
    }

    /// <summary>
    /// 合并集合
    /// </summary>
    private static object? MergeCollections(object? target, object? source)
    {
        if (target is null || source is null)
        {
            return target ?? source;
        }

        // 处理列表类型
        if (target is IList targetList && source is IList sourceList)
        {
            // 提取元素类型
            Type elementType;
            var targetType = target.GetType();

            if (targetType.IsGenericType)
            {
                elementType = targetType.GetGenericArguments()[0];
            }
            else if (targetType.IsArray)
            {
                elementType = targetType.GetElementType()!;
            }
            else
            {
                return target; // 无法确定元素类型，返回高优先级值
            }

            // 创建新列表
            var listType = typeof(List<>).MakeGenericType(elementType);
            var resultList = (IList)Activator.CreateInstance(listType)!;

            // 添加所有唯一项
            var addedItems = new HashSet<object?>();

            // 先添加高优先级集合中的项
            foreach (var item in targetList)
            {
                if (addedItems.Add(item))
                {
                    _ = resultList.Add(DeepClone(item));
                }
            }

            // 再添加低优先级集合中的项（如果尚未添加）
            foreach (var item in sourceList)
            {
                if (addedItems.Add(item))
                {
                    _ = resultList.Add(DeepClone(item));
                }
            }

            return resultList;
        }

        // 其他集合类型，返回高优先级集合
        return DeepClone(target);
    }

    /// <summary>
    /// 合并字典
    /// </summary>
    private static object? MergeDictionaries(object? target, object? source)
    {
        if (target is null)
        {
            return source is not null ? DeepClone(source) : null;
        }

        if (source is null)
        {
            return DeepClone(target);
        }

        if (target is IDictionary targetDict && source is IDictionary sourceDict)
        {
            // 提取键值类型
            var targetType = target.GetType();
            if (!targetType.IsGenericType)
            {
                return DeepClone(target); // 非泛型字典，返回高优先级值
            }

            var args = targetType.GetGenericArguments();
            var keyType = args[0];
            var valueType = args[1];

            // 创建新字典
            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var resultDict = (IDictionary)Activator.CreateInstance(dictType)!;

            // 复制目标字典的所有项
            foreach (DictionaryEntry entry in targetDict)
            {
                resultDict.Add(entry.Key, DeepClone(entry.Value));
            }

            // 合并源字典中的项
            foreach (DictionaryEntry entry in sourceDict)
            {
                if (!resultDict.Contains(entry.Key))
                {
                    // 如果键不存在，添加克隆的值
                    resultDict.Add(entry.Key, DeepClone(entry.Value));
                }
                else if (CanMerge(resultDict[entry.Key], entry.Value))
                {
                    // 如果两个值可以合并，进行合并
                    resultDict[entry.Key] = MergeValues(resultDict[entry.Key], entry.Value);
                }
                // 对于不能合并的项，保持高优先级值
            }

            return resultDict;
        }

        return DeepClone(target);
    }

    /// <summary>
    /// 合并复杂对象
    /// </summary>
    private static object? MergeComplexObjects(object? target, object? source)
    {
        if (target is null)
        {
            return source is not null ? DeepClone(source) : null;
        }

        if (source is null)
        {
            return DeepClone(target);
        }

        var type = target.GetType();

        // 创建新对象
        object? result;
        try
        {
            result = Activator.CreateInstance(type);
        }
        catch
        {
            // 如果无法创建实例，返回高优先级对象的克隆
            return DeepClone(target);
        }

        // 获取或从缓存中检索属性
        if (!PropertyCache.TryGetValue(type, out var properties))
        {
            properties = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite)];
            PropertyCache[type] = properties;
        }

        // 依次处理每个属性
        foreach (var property in properties)
        {
            var targetValue = property.GetValue(target);
            var sourceValue = property.GetValue(source);

            try
            {
                if (!IsNullOrDefault(targetValue))
                {
                    // 高优先级值非空，合并或使用高优先级值
                    if (CanMerge(targetValue, sourceValue))
                    {
                        property.SetValue(result, MergeValues(targetValue, sourceValue));
                    }
                    else
                    {
                        property.SetValue(result, DeepClone(targetValue));
                    }
                }
                else if (!IsNullOrDefault(sourceValue))
                {
                    // 低优先级值非空但高优先级值为空
                    property.SetValue(result, DeepClone(sourceValue));
                }
                // 两个值都为空，不设置
            }
            catch
            {
                // 属性设置失败时，跳过
                continue;
            }
        }

        return result;
    }

    /// <summary>
    /// 深度克隆对象
    /// </summary>
    private static object? DeepClone(object? obj)
    {
        if (obj is null)
        {
            return null;
        }

        var type = obj.GetType();

        // 处理简单类型（直接返回）
        if (IsSimpleType(type))
        {
            return obj;
        }

        // 处理字典
        if (obj is IDictionary dict)
        {
            return CloneDictionary(dict);
        }

        // 处理列表
        if (obj is IList list)
        {
            return CloneList(list);
        }

        // 处理复杂对象
        return CloneComplexObject(obj);
    }

    /// <summary>
    /// 判断是否为简单类型（不需要深度克隆）
    /// </summary>
    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(decimal) ||
               type == typeof(Guid);
    }

    /// <summary>
    /// 克隆字典
    /// </summary>
    private static object CloneDictionary(IDictionary dict)
    {
        var type = dict.GetType();

        // 提取键值类型
        if (!type.IsGenericType)
        {
            // 非泛型字典的处理
            var clone = new Hashtable();
            foreach (DictionaryEntry entry in dict)
            {
                clone.Add(entry.Key, DeepClone(entry.Value));
            }
            return clone;
        }

        var args = type.GetGenericArguments();
        var keyType = args[0];
        var valueType = args[1];
        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var newDict = (IDictionary)Activator.CreateInstance(dictType)!;

        foreach (DictionaryEntry entry in dict)
        {
            newDict.Add(entry.Key, DeepClone(entry.Value));
        }

        return newDict;
    }

    /// <summary>
    /// 克隆列表
    /// </summary>
    private static object CloneList(IList list)
    {
        var type = list.GetType();
        Type elementType;

        if (type.IsArray)
        {
            // 数组的处理
            elementType = type.GetElementType()!;
            var array = Array.CreateInstance(elementType, list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                array.SetValue(DeepClone(list[i]), i);
            }
            return array;
        }

        if (type.IsGenericType)
        {
            // 泛型列表的处理
            elementType = type.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(elementType);
            var newList = (IList)Activator.CreateInstance(listType)!;

            foreach (var item in list)
            {
                _ = newList.Add(DeepClone(item));
            }

            return newList;
        }

        // 非泛型列表的处理
        var genericList = new ArrayList();
        foreach (var item in list)
        {
            _ = genericList.Add(DeepClone(item));
        }

        return genericList;
    }

    /// <summary>
    /// 克隆复杂对象
    /// </summary>
    private static object? CloneComplexObject(object obj)
    {
        var type = obj.GetType();

        try
        {
            // 尝试创建新实例
            var newObj = Activator.CreateInstance(type);

            // 获取或从缓存中检索属性
            if (!PropertyCache.TryGetValue(type, out var properties))
            {
                properties = [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite)];
                PropertyCache[type] = properties;
            }

            // 复制所有属性
            foreach (var property in properties)
            {
                try
                {
                    var value = property.GetValue(obj);
                    if (value is not null)
                    {
                        property.SetValue(newObj, DeepClone(value));
                    }
                }
                catch
                {
                    // 单个属性克隆失败，继续处理其他属性
                    continue;
                }
            }

            return newObj;
        }
        catch
        {
            // 如果对象克隆失败，返回原对象
            // 在生产环境中可能需要记录此错误
            ConsoleLogger.Error($"无法克隆类型 {type.FullName} 的对象");
            return obj;
        }
    }
}
