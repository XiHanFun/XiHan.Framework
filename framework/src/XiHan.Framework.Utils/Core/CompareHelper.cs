#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CompareHelper
// Guid:4e3d2c1b-0a9f-8e7d-6c5b-4a3f2e1d0c9b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/19 23:45:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 对象比较帮助类
/// </summary>
public static class CompareHelper
{
    #region 深度比较

    /// <summary>
    /// 深度比较两个对象是否相等
    /// </summary>
    /// <param name="obj1">第一个对象</param>
    /// <param name="obj2">第二个对象</param>
    /// <returns>如果相等则返回 true，否则返回 false</returns>
    public static bool DeepEquals(object? obj1, object? obj2)
    {
        return DeepEquals(obj1, obj2, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    /// <summary>
    /// 深度比较两个对象是否相等（内部方法，用于处理循环引用）
    /// </summary>
    /// <param name="obj1">第一个对象</param>
    /// <param name="obj2">第二个对象</param>
    /// <param name="visited">已访问对象集合，用于检测循环引用</param>
    /// <returns>如果相等则返回 true，否则返回 false</returns>
    private static bool DeepEquals(object? obj1, object? obj2, HashSet<object> visited)
    {
        // 处理 null 值
        if (obj1 == null && obj2 == null)
        {
            return true;
        }
        if (obj1 == null || obj2 == null)
        {
            return false;
        }

        // 引用相等
        if (ReferenceEquals(obj1, obj2))
        {
            return true;
        }

        // 类型不同
        if (obj1.GetType() != obj2.GetType())
        {
            return false;
        }

        var type = obj1.GetType();

        // 处理基本类型和字符串
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(Guid))
        {
            return obj1.Equals(obj2);
        }

        // 处理枚举
        if (type.IsEnum)
        {
            return obj1.Equals(obj2);
        }

        // 检测循环引用
        if (visited.Contains(obj1))
        {
            return true; // 假设循环引用的对象相等
        }

        visited.Add(obj1);

        try
        {
            // 处理数组
            if (type.IsArray)
            {
                return DeepEqualsArray((Array)obj1, (Array)obj2, visited);
            }

            // 处理字典
            if (obj1 is IDictionary dict1 && obj2 is IDictionary dict2)
            {
                return DeepEqualsDictionary(dict1, dict2, visited);
            }

            // 处理集合
            if (obj1 is IEnumerable enum1 && obj2 is IEnumerable enum2)
            {
                return DeepEqualsEnumerable(enum1, enum2, visited);
            }

            // 处理自定义对象
            return DeepEqualsObject(obj1, obj2, visited);
        }
        finally
        {
            visited.Remove(obj1);
        }
    }

    /// <summary>
    /// 深度比较两个数组
    /// </summary>
    /// <param name="array1">第一个数组</param>
    /// <param name="array2">第二个数组</param>
    /// <param name="visited">已访问对象集合</param>
    /// <returns>比较结果</returns>
    private static bool DeepEqualsArray(Array array1, Array array2, HashSet<object> visited)
    {
        if (array1.Length != array2.Length)
        {
            return false;
        }

        if (array1.Rank != array2.Rank)
        {
            return false;
        }

        for (var i = 0; i < array1.Length; i++)
        {
            if (!DeepEquals(array1.GetValue(i), array2.GetValue(i), visited))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 深度比较两个字典
    /// </summary>
    /// <param name="dict1">第一个字典</param>
    /// <param name="dict2">第二个字典</param>
    /// <param name="visited">已访问对象集合</param>
    /// <returns>比较结果</returns>
    private static bool DeepEqualsDictionary(IDictionary dict1, IDictionary dict2, HashSet<object> visited)
    {
        if (dict1.Count != dict2.Count)
        {
            return false;
        }

        foreach (var key in dict1.Keys)
        {
            if (!dict2.Contains(key))
            {
                return false;
            }

            if (!DeepEquals(dict1[key], dict2[key], visited))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 深度比较两个可枚举对象
    /// </summary>
    /// <param name="enum1">第一个可枚举对象</param>
    /// <param name="enum2">第二个可枚举对象</param>
    /// <param name="visited">已访问对象集合</param>
    /// <returns>比较结果</returns>
    private static bool DeepEqualsEnumerable(IEnumerable enum1, IEnumerable enum2, HashSet<object> visited)
    {
        var enumerator1 = enum1.GetEnumerator();
        var enumerator2 = enum2.GetEnumerator();

        try
        {
            while (true)
            {
                var hasNext1 = enumerator1.MoveNext();
                var hasNext2 = enumerator2.MoveNext();

                if (hasNext1 != hasNext2)
                {
                    return false;
                }

                if (!hasNext1)
                {
                    break;
                }

                if (!DeepEquals(enumerator1.Current, enumerator2.Current, visited))
                {
                    return false;
                }
            }
        }
        finally
        {
            if (enumerator1 is IDisposable disposable1)
            {
                disposable1.Dispose();
            }
            if (enumerator2 is IDisposable disposable2)
            {
                disposable2.Dispose();
            }
        }

        return true;
    }

    /// <summary>
    /// 深度比较两个自定义对象
    /// </summary>
    /// <param name="obj1">第一个对象</param>
    /// <param name="obj2">第二个对象</param>
    /// <param name="visited">已访问对象集合</param>
    /// <returns>比较结果</returns>
    private static bool DeepEqualsObject(object obj1, object obj2, HashSet<object> visited)
    {
        var type = obj1.GetType();
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // 比较字段
        foreach (var field in fields)
        {
            var value1 = field.GetValue(obj1);
            var value2 = field.GetValue(obj2);

            if (!DeepEquals(value1, value2, visited))
            {
                return false;
            }
        }

        // 比较属性
        foreach (var property in properties)
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            try
            {
                var value1 = property.GetValue(obj1);
                var value2 = property.GetValue(obj2);

                if (!DeepEquals(value1, value2, visited))
                {
                    return false;
                }
            }
            catch
            {
                // 忽略无法访问的属性
                continue;
            }
        }

        return true;
    }

    #endregion

    #region 浅比较

    /// <summary>
    /// 浅比较两个对象是否相等（只比较引用和基本类型）
    /// </summary>
    /// <param name="obj1">第一个对象</param>
    /// <param name="obj2">第二个对象</param>
    /// <returns>如果相等则返回 true，否则返回 false</returns>
    public static bool ShallowEquals(object? obj1, object? obj2)
    {
        if (obj1 == null && obj2 == null)
        {
            return true;
        }
        if (obj1 == null || obj2 == null)
        {
            return false;
        }

        if (ReferenceEquals(obj1, obj2))
        {
            return true;
        }

        if (obj1.GetType() != obj2.GetType())
        {
            return false;
        }

        var type = obj1.GetType();

        // 对于值类型和字符串，使用默认比较
        if (type.IsValueType || type == typeof(string))
        {
            return obj1.Equals(obj2);
        }

        // 对于引用类型，只比较引用
        return false;
    }

    #endregion

    #region 集合比较

    /// <summary>
    /// 比较两个集合是否相等（元素顺序相关）
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="collection1">第一个集合</param>
    /// <param name="collection2">第二个集合</param>
    /// <param name="comparer">自定义比较器</param>
    /// <returns>如果相等则返回 true，否则返回 false</returns>
    public static bool CompareCollections<T>(IEnumerable<T>? collection1, IEnumerable<T>? collection2,
        IEqualityComparer<T>? comparer = null)
    {
        if (collection1 == null && collection2 == null)
        {
            return true;
        }
        if (collection1 == null || collection2 == null)
        {
            return false;
        }

        comparer ??= EqualityComparer<T>.Default;

        return collection1.SequenceEqual(collection2, comparer);
    }

    /// <summary>
    /// 比较两个集合是否相等（元素顺序无关）
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="collection1">第一个集合</param>
    /// <param name="collection2">第二个集合</param>
    /// <param name="comparer">自定义比较器</param>
    /// <returns>如果相等则返回 true，否则返回 false</returns>
    public static bool CompareCollectionsUnordered<T>(IEnumerable<T>? collection1, IEnumerable<T>? collection2,
        IEqualityComparer<T>? comparer = null)
    {
        if (collection1 == null && collection2 == null)
        {
            return true;
        }
        if (collection1 == null || collection2 == null)
        {
            return false;
        }

        comparer ??= EqualityComparer<T>.Default;

        var set1 = new HashSet<T>(collection1, comparer);
        var set2 = new HashSet<T>(collection2, comparer);

        return set1.SetEquals(set2);
    }

    /// <summary>
    /// 比较两个字典是否相等
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="dict1">第一个字典</param>
    /// <param name="dict2">第二个字典</param>
    /// <param name="keyComparer">键比较器</param>
    /// <param name="valueComparer">值比较器</param>
    /// <returns>如果相等则返回 true，否则返回 false</returns>
    public static bool CompareDictionaries<TKey, TValue>(IDictionary<TKey, TValue>? dict1,
        IDictionary<TKey, TValue>? dict2, IEqualityComparer<TKey>? keyComparer = null,
        IEqualityComparer<TValue>? valueComparer = null) where TKey : notnull
    {
        if (dict1 == null && dict2 == null)
        {
            return true;
        }
        if (dict1 == null || dict2 == null)
        {
            return false;
        }

        if (dict1.Count != dict2.Count)
        {
            return false;
        }

        keyComparer ??= EqualityComparer<TKey>.Default;
        valueComparer ??= EqualityComparer<TValue>.Default;

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value2))
            {
                return false;
            }

            if (!valueComparer.Equals(kvp.Value, value2))
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region 哈希码计算

    /// <summary>
    /// 计算对象的深度哈希码
    /// </summary>
    /// <param name="obj">对象</param>
    /// <returns>哈希码</returns>
    public static int GetHashCodeDeep(object? obj)
    {
        if (obj == null)
        {
            return 0;
        }

        return GetHashCodeDeep(obj, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    /// <summary>
    /// 计算集合的组合哈希码（顺序相关）
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="collection">集合</param>
    /// <param name="comparer">比较器</param>
    /// <returns>哈希码</returns>
    public static int GetCollectionHashCode<T>(IEnumerable<T>? collection, IEqualityComparer<T>? comparer = null)
    {
        if (collection == null)
        {
            return 0;
        }

        comparer ??= EqualityComparer<T>.Default;
        var hash = 0;

        foreach (var item in collection)
        {
            hash = HashCode.Combine(hash, comparer.GetHashCode(item!));
        }

        return hash;
    }

    /// <summary>
    /// 计算集合的组合哈希码（顺序无关）
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    /// <param name="collection">集合</param>
    /// <param name="comparer">比较器</param>
    /// <returns>哈希码</returns>
    public static int GetCollectionHashCodeUnordered<T>(IEnumerable<T>? collection, IEqualityComparer<T>? comparer = null)
    {
        if (collection == null)
        {
            return 0;
        }

        comparer ??= EqualityComparer<T>.Default;
        var hash = 0;

        foreach (var item in collection)
        {
            hash ^= comparer.GetHashCode(item!);
        }

        return hash;
    }

    /// <summary>
    /// 计算对象的深度哈希码（内部方法，用于处理循环引用）
    /// </summary>
    /// <param name="obj">对象</param>
    /// <param name="visited">已访问对象集合</param>
    /// <returns>哈希码</returns>
    private static int GetHashCodeDeep(object? obj, HashSet<object> visited)
    {
        if (obj == null)
        {
            return 0;
        }

        var type = obj.GetType();

        // 基本类型和字符串
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(Guid))
        {
            return obj.GetHashCode();
        }

        // 枚举
        if (type.IsEnum)
        {
            return obj.GetHashCode();
        }

        // 检测循环引用
        if (visited.Contains(obj))
        {
            return obj.GetType().GetHashCode();
        }

        visited.Add(obj);

        try
        {
            var hash = type.GetHashCode();

            // 数组
            if (type.IsArray && obj is Array array)
            {
                for (var i = 0; i < array.Length; i++)
                {
                    hash ^= GetHashCodeDeep(array.GetValue(i), visited);
                }
                return hash;
            }

            // 字典
            if (obj is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    hash ^= GetHashCodeDeep(entry.Key, visited);
                    hash ^= GetHashCodeDeep(entry.Value, visited);
                }
                return hash;
            }

            // 集合
            if (obj is IEnumerable enumerable && type != typeof(string))
            {
                foreach (var item in enumerable)
                {
                    hash ^= GetHashCodeDeep(item, visited);
                }
                return hash;
            }

            // 自定义对象
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                hash ^= GetHashCodeDeep(value, visited);
            }

            return hash;
        }
        finally
        {
            visited.Remove(obj);
        }
    }

    #endregion

    #region 特殊比较器

    /// <summary>
    /// 引用相等比较器
    /// </summary>
    public sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        /// <summary>
        /// 单例实例
        /// </summary>
        public static readonly ReferenceEqualityComparer Instance = new();

        private ReferenceEqualityComparer()
        { }

        /// <summary>
        /// 比较两个对象的引用是否相等
        /// </summary>
        /// <param name="x">第一个对象</param>
        /// <param name="y">第二个对象</param>
        /// <returns>如果引用相等则返回 true，否则返回 false</returns>
        public new bool Equals(object? x, object? y)
        {
            return ReferenceEquals(x, y);
        }

        /// <summary>
        /// 获取对象的哈希码
        /// </summary>
        /// <param name="obj">对象</param>
        /// <returns>哈希码</returns>
        public int GetHashCode(object obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }

    #endregion
}
