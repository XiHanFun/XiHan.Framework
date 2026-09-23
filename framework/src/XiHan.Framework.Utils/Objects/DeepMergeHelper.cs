// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Utils.Objects;

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
    /// <param name="configs">按优先级排序的配置列表，第一个为最高优先级，其中的 null 元素会被忽略</param>
    /// <returns>合并后的配置</returns>
    public static T DeepMerge<T>(params T[]? configs) where T : class, new()
    {
        // 快速路径：没有配置
        if (configs is null || configs.Length == 0)
        {
            return new T();
        }

        // 先滤掉 null 元素：ProcessProperty 会对每个元素调 PropertyInfo.GetValue(config)，
        // 实例属性传 null 目标会抛 TargetException，
        // 而"某一层配置缺省就传 null"恰恰是本方法最常见的调用方式（如 DeepMerge(用户配置, 默认配置)）。
        // 原先的 configs is [not null] 只挡住了"单个元素且非 null"，多元素含 null 时直接炸。
        var effectiveConfigs = Array.FindAll(configs, config => config is not null);

        if (effectiveConfigs.Length == 0)
        {
            return new T();
        }

        // 快速路径：只有一个有效配置
        if (effectiveConfigs.Length == 1)
        {
            return DeepClone(effectiveConfigs[0]) as T ?? new T();
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
            ProcessProperty(property, result, effectiveConfigs);
        }

        return result;
    }

    /// <summary>
    /// 处理单个属性的合并
    /// </summary>
    /// <typeparam name="T">配置对象类型</typeparam>
    /// <param name="property">要处理的属性信息</param>
    /// <param name="result">合并结果对象</param>
    /// <param name="configs">配置对象数组</param>
    private static void ProcessProperty<T>(PropertyInfo property, T result, T[] configs) where T : class
    {
        object? valueToSet = null;
        var valueFound = false;

        // 按优先级顺序检查每个配置
        foreach (var config in configs)
        {
            // 获取当前配置的属性值
            var value = property.GetValue(config);

            // 检查值是否为空
            if (IsNullOrDefault(value))
            {
                continue;
            }

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

        // 如果找到了值，设置到结果中
        if (!valueFound)
        {
            return;
        }

        try
        {
            property.SetValue(result, valueToSet);
        }
        catch (Exception ex) when (ex is ArgumentException or TargetInvocationException)
        {
            // 记录或处理属性设置失败的情况
            LogHelper.Error($"无法设置属性 {property.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查两个值是否可以合并
    /// </summary>
    /// <param name="target">目标值（高优先级）</param>
    /// <param name="source">源值（低优先级）</param>
    /// <returns>如果可以合并返回true，否则返回false</returns>
    private static bool CanMerge(object? target, object? source)
    {
        return target is not null && source is not null && ((IsMergeableCollection(target) && IsMergeableCollection(source)) ||
               (IsDictionary(target) && IsDictionary(source)) ||
               (IsComplexObject(target) && IsComplexObject(source) && target.GetType() == source.GetType()));
    }

    /// <summary>
    /// 合并两个值
    /// </summary>
    /// <param name="target">目标值（高优先级）</param>
    /// <param name="source">源值（低优先级）</param>
    /// <returns>合并后的值</returns>
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
    /// <param name="value">要判断的值</param>
    /// <returns>如果需要克隆返回true，否则返回false</returns>
    private static bool NeedsCloning(object? value)
    {
        return value is not null && (IsComplexObject(value) || IsCollection(value) || IsDictionary(value));
    }

    /// <summary>
    /// 判断值是否为空或默认值
    /// </summary>
    /// <param name="value">要判断的值</param>
    /// <returns>如果为空或默认值返回true，否则返回false</returns>
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

        switch (value)
        {
            // 处理字符串
            case string strValue:
                return string.IsNullOrEmpty(strValue);
            // 处理集合
            case ICollection collection:
                return collection.Count == 0;
            // 处理只实现泛型集合接口的类型（HashSet<T>、ISet<T>、IEnumerable<T> 属性等）。
            // 它们不实现非泛型 ICollection，原先会落到下面的默认分支按引用比较，
            // 于是一个空 HashSet<T> 被当成"有值"，把低优先级里真正有内容的集合挡掉。
            case IEnumerable enumerable:
                return !enumerable.Cast<object?>().Any();

            default:
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
    }

    /// <summary>
    /// 是否为可合并的集合类型
    /// </summary>
    /// <param name="value">要判断的值</param>
    /// <returns>如果是可合并的集合类型返回true，否则返回false</returns>
    private static bool IsMergeableCollection(object? value)
    {
        if (value is null)
        {
            return false;
        }

        // 原实现是 type.IsArray || (type.IsGenericType && typeof(IList<>).IsAssignableFrom(type.GetGenericTypeDefinition()) || ...)：
        // 开放泛型类型之间的 IsAssignableFrom 恒为 false（typeof(IList<>).IsAssignableFrom(typeof(List<>)) 就是 false），
        // 于是 List<T>/HashSet<T> 等一律被判成"不可合并"，类注释承诺的"合并集合"对泛型集合完全失效，
        // 只有数组能进合并分支——而数组进去以后 MergeCollections 产出的是 List<T>，
        // 回写数组属性时 SetValue 抛 ArgumentException 被 ProcessProperty 吞掉，属性最终一个值都没设上。
        // 改为按运行时接口判定：是可枚举的、且不是字符串、也不是字典（字典有 MergeDictionaries 专门处理）。
        return value is IEnumerable and not string && !IsDictionary(value);
    }

    /// <summary>
    /// 是否为字典类型
    /// </summary>
    /// <param name="value">要判断的值</param>
    /// <returns>如果是字典类型返回true，否则返回false</returns>
    private static bool IsDictionary(object? value)
    {
        return value is not null and IDictionary;
    }

    /// <summary>
    /// 是否为集合类型
    /// </summary>
    /// <param name="value">要判断的值</param>
    /// <returns>如果是集合类型返回true，否则返回false</returns>
    private static bool IsCollection(object? value)
    {
        return value is ICollection;
    }

    /// <summary>
    /// 是否为复杂对象类型
    /// </summary>
    /// <param name="value">要判断的值</param>
    /// <returns>如果是复杂对象类型返回true，否则返回false</returns>
    private static bool IsComplexObject(object? value)
    {
        if (value is null)
        {
            return false;
        }

        var type = value.GetType();

        // 排除基本类型、枚举和常见值类型
        return type is { IsPrimitive: false, IsEnum: false } &&
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
    /// <param name="target">目标集合（高优先级）</param>
    /// <param name="source">源集合（低优先级）</param>
    /// <returns>合并后的集合</returns>
    /// <remarks>
    /// 原实现只认非泛型 <see cref="IList"/>，<see cref="HashSet{T}"/>、<see cref="Queue{T}"/> 之类
    /// 直接退回 <c>DeepClone(target)</c>，低优先级的项全部丢失；
    /// 现在改为按运行时元素类型合并，并尽量还原回目标的原类型。
    /// </remarks>
    private static object? MergeCollections(object? target, object? source)
    {
        if (target is null || source is null)
        {
            return target ?? source;
        }

        var targetType = target.GetType();
        var elementType = GetEnumerableElementType(targetType);

        if (elementType is null || target is not IEnumerable targetItems || source is not IEnumerable sourceItems)
        {
            return DeepClone(target); // 无法确定元素类型，返回高优先级值的克隆
        }

        // 按"高优先级在前"的顺序收集去重后的项
        var mergedItems = new List<object?>();
        var addedItems = new HashSet<object?>();

        foreach (var item in targetItems)
        {
            if (addedItems.Add(item))
            {
                mergedItems.Add(DeepClone(item));
            }
        }

        foreach (var item in sourceItems)
        {
            if (addedItems.Add(item))
            {
                mergedItems.Add(DeepClone(item));
            }
        }

        // 合并结果必须能回写到原属性上：
        // 调用方 ProcessProperty / MergeComplexObjects 会把结果 SetValue 回去，
        // 类型不兼容时 SetValue 抛 ArgumentException 并被 catch 吞掉，属性最终一个值都没设上。
        return targetType.IsArray
            ? CreateArray(elementType, mergedItems)
            : TryCreateCollection(targetType, elementType, mergedItems) ?? CreateList(elementType, mergedItems);
    }

    /// <summary>
    /// 合并字典
    /// </summary>
    /// <param name="target">目标字典（高优先级）</param>
    /// <param name="source">源字典（低优先级）</param>
    /// <returns>合并后的字典</returns>
    private static object? MergeDictionaries(object? target, object? source)
    {
        if (target is null)
        {
            return source is not null ? DeepClone(source) : null;
        }

        if (source is null || target is not IDictionary targetDict || source is not IDictionary sourceDict)
        {
            return DeepClone(target);
        }

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

    /// <summary>
    /// 合并复杂对象
    /// </summary>
    /// <param name="target">目标对象（高优先级）</param>
    /// <param name="source">源对象（低优先级）</param>
    /// <returns>合并后的对象</returns>
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
            }
        }

        return result;
    }

    /// <summary>
    /// 深度克隆对象
    /// </summary>
    /// <param name="obj">要克隆的对象</param>
    /// <returns>克隆后的对象</returns>
    private static object? DeepClone(object? obj)
    {
        if (obj is null)
        {
            return null;
        }

        var type = obj.GetType();

        // 处理简单类型(直接返回)
        return IsSimpleType(type)
            ? obj
            : obj switch
            {
                // 处理字典
                IDictionary dict => CloneDictionary(dict),
                // 处理列表
                IList list => CloneList(list),
                // 处理只实现泛型集合接口的类型（HashSet<T>、Queue<T>、SortedSet<T> 等）。
                // 它们既不是 IDictionary 也不是非泛型 IList，原先会落到 CloneComplexObject，
                // 而那条路靠"Activator.CreateInstance + 复制可写属性"工作，
                // 这类集合没有可写属性，结果是克隆出一个空集合——元素被静默丢光。
                // 重建不了原类型时仍退回 CloneComplexObject，保持原有行为。
                IEnumerable enumerable => CloneEnumerable(enumerable) ?? CloneComplexObject(obj),
                _ => CloneComplexObject(obj)
            };

        // 处理复杂对象
    }

    /// <summary>
    /// 取可枚举类型的元素类型
    /// </summary>
    /// <param name="type">集合类型</param>
    /// <returns>元素类型，无法确定时返回 null</returns>
    private static Type? GetEnumerableElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        foreach (var contract in type.GetInterfaces())
        {
            if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return contract.GetGenericArguments()[0];
            }
        }

        return null;
    }

    /// <summary>
    /// 用 <c>IEnumerable&lt;TElement&gt;</c> 构造函数重建目标集合类型
    /// </summary>
    /// <param name="collectionType">目标集合类型</param>
    /// <param name="elementType">元素类型</param>
    /// <param name="items">元素集合</param>
    /// <returns>重建后的集合，类型不支持该构造函数时返回 null</returns>
    private static object? TryCreateCollection(Type collectionType, Type elementType, List<object?> items)
    {
        if (collectionType.IsAbstract || collectionType.IsInterface)
        {
            return null;
        }

        var constructor = collectionType.GetConstructor([typeof(IEnumerable<>).MakeGenericType(elementType)]);
        if (constructor is null)
        {
            return null;
        }

        // Stack<T> 的枚举顺序是栈顶在前，直接回灌构造函数会把栈整个翻过来，因此先反转一次
        var ordered = collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(Stack<>)
            ? Enumerable.Reverse(items).ToList()
            : items;

        try
        {
            return constructor.Invoke([CreateList(elementType, ordered)]);
        }
        catch (TargetInvocationException ex)
        {
            LogHelper.Error($"无法重建集合类型 {collectionType.FullName}: {ex.InnerException?.Message ?? ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 用指定元素类型创建 <see cref="List{T}"/>
    /// </summary>
    /// <param name="elementType">元素类型</param>
    /// <param name="items">元素集合</param>
    /// <returns>强类型列表</returns>
    private static IList CreateList(Type elementType, List<object?> items)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    /// <summary>
    /// 用指定元素类型创建数组
    /// </summary>
    /// <param name="elementType">元素类型</param>
    /// <param name="items">元素集合</param>
    /// <returns>强类型数组</returns>
    private static Array CreateArray(Type elementType, List<object?> items)
    {
        var array = Array.CreateInstance(elementType, items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            array.SetValue(items[i], i);
        }

        return array;
    }

    /// <summary>
    /// 克隆只实现泛型集合接口的集合
    /// </summary>
    /// <param name="source">要克隆的集合</param>
    /// <returns>克隆后的集合，无法重建原类型时返回 null</returns>
    private static object? CloneEnumerable(IEnumerable source)
    {
        var type = source.GetType();
        var elementType = GetEnumerableElementType(type);
        if (elementType is null)
        {
            return null;
        }

        var items = new List<object?>();
        foreach (var item in source)
        {
            items.Add(DeepClone(item));
        }

        return TryCreateCollection(type, elementType, items);
    }

    /// <summary>
    /// 判断是否为简单类型(不需要深度克隆)
    /// </summary>
    /// <param name="type">要判断的类型</param>
    /// <returns>如果是简单类型返回true，否则返回false</returns>
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
    /// <param name="dict">要克隆的字典</param>
    /// <returns>克隆后的字典</returns>
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
    /// <param name="list">要克隆的列表</param>
    /// <returns>克隆后的列表</returns>
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
                newList.Add(DeepClone(item));
            }

            return newList;
        }

        // 非泛型列表的处理
        var genericList = new ArrayList();
        foreach (var item in list)
        {
            genericList.Add(DeepClone(item));
        }

        return genericList;
    }

    /// <summary>
    /// 克隆复杂对象
    /// </summary>
    /// <param name="obj">要克隆的复杂对象</param>
    /// <returns>克隆后的对象</returns>
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
                }
            }

            return newObj;
        }
        catch
        {
            // 如果对象克隆失败，返回原对象
            // 在生产环境中可能需要记录此错误
            LogHelper.Error($"无法克隆类型 {type.FullName} 的对象");
            return obj;
        }
    }
}
