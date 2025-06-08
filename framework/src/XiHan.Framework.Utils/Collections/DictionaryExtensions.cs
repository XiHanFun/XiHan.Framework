#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DictionaryExtensions
// Guid:9d5745ef-a54a-4195-a080-cb9250266aa3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 2:44:07
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Collections.Concurrent;
using System.Dynamic;

namespace XiHan.Framework.Utils.Collections;

/// <summary>
/// 字典扩展方法
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// 使用给定的键从字典中获取值。如果找不到，则返回默认值
    /// </summary>
    /// <typeparam name="TKey">键的类型</typeparam>
    /// <typeparam name="TValue">值的类型</typeparam>
    /// <param name="dictionary">要检查和获取的字典</param>
    /// <param name="key">要查找值的键</param>
    /// <returns>如果找到，返回值；如果找不到，返回默认值</returns>
    public static TValue? GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull
    {
        return dictionary.GetValueOrDefault(key);
    }

    /// <summary>
    /// 使用给定的键从字典中获取值。如果找不到，则返回默认值
    /// </summary>
    /// <typeparam name="TKey">键的类型</typeparam>
    /// <typeparam name="TValue">值的类型</typeparam>
    /// <param name="dictionary">要检查和获取的字典</param>
    /// <param name="key">要查找值的键</param>
    /// <returns>如果找到，返回值；如果找不到，返回默认值</returns>
    public static TValue? GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    {
        return dictionary.TryGetValue(key, out var obj) ? obj : default;
    }

    /// <summary>
    /// 使用给定的键从只读字典中获取值。如果找不到，则返回默认值
    /// </summary>
    /// <typeparam name="TKey">键的类型</typeparam>
    /// <typeparam name="TValue">值的类型</typeparam>
    /// <param name="dictionary">要检查和获取的只读字典</param>
    /// <param name="key">要查找值的键</param>
    /// <returns>如果找到，返回值；如果找不到，返回默认值</returns>
    public static TValue? GetOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
    {
        return dictionary.GetValueOrDefault(key);
    }

    /// <summary>
    /// 使用给定的键从并发字典中获取值。如果找不到，则返回默认值
    /// </summary>
    /// <typeparam name="TKey">键的类型，不能为空</typeparam>
    /// <typeparam name="TValue">值的类型</typeparam>
    /// <param name="dictionary">要检查和获取的并发字典</param>
    /// <param name="key">要查找值的键</param>
    /// <returns>如果找到，返回值；如果找不到，返回默认值</returns>
    public static TValue? GetOrDefault<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull
    {
        return dictionary.GetValueOrDefault(key);
    }

    /// <summary>
    /// 使用给定的键从字典中获取值。如果找不到，则使用工厂方法创建并添加值
    /// </summary>
    /// <typeparam name="TKey">键的类型</typeparam>
    /// <typeparam name="TValue">值的类型</typeparam>
    /// <param name="dictionary">要检查和获取的字典</param>
    /// <param name="key">要查找值的键</param>
    /// <param name="factory">如果字典中未找到，则用于创建值的工厂方法</param>
    /// <returns>如果找到，返回值；如果找不到，使用工厂方法创建并返回默认值</returns>
    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> factory)
    {
        return dictionary.TryGetValue(key, out var obj) ? obj : dictionary[key] = factory(key);
    }

    /// <summary>
    /// 使用给定的键从字典中获取值。如果找不到，则使用工厂方法创建并添加值
    /// </summary>
    /// <typeparam name="TKey">键的类型</typeparam>
    /// <typeparam name="TValue">值的类型</typeparam>
    /// <param name="dictionary">要检查和获取的字典</param>
    /// <param name="key">要查找值的键</param>
    /// <param name="factory">如果字典中未找到，则用于创建值的工厂方法</param>
    /// <returns>如果找到，返回值；如果找不到，使用工厂方法创建并返回默认值</returns>
    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> factory)
    {
        return dictionary.GetOrAdd(key, _ => factory());
    }

    /// <summary>
    /// 使用给定的键从并发字典中获取值。如果找不到，则使用工厂方法创建并添加值
    /// </summary>
    /// <typeparam name="TKey">键的类型，不能为空</typeparam>
    /// <typeparam name="TValue">值的类型</typeparam>
    /// <param name="dictionary">要检查和获取的并发字典</param>
    /// <param name="key">要查找值的键</param>
    /// <param name="factory">如果并发字典中未找到，则用于创建值的工厂方法</param>
    /// <returns>如果找到，返回值；如果找不到，使用工厂方法创建并返回默认值</returns>
    public static TValue GetOrAdd<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> factory) where TKey : notnull
    {
        return dictionary.GetOrAdd(key, _ => factory());
    }

    /// <summary>
    /// 将字典转换为动态对象，以便在运行时添加和移除
    /// </summary>
    /// <param name="dictionary">要转换的字典对象</param>
    /// <returns>如果值正确，返回表示对象的 ExpandoObject</returns>
    public static dynamic ConvertToDynamicObject(this Dictionary<string, object> dictionary)
    {
        ExpandoObject expandoObject = new();
        ICollection<KeyValuePair<string, object>> expendObjectCollection = expandoObject!;

        foreach (var keyValuePair in dictionary)
        {
            expendObjectCollection.Add(keyValuePair);
        }

        return expandoObject;
    }

    /// <summary>
    /// 如果字典中存在指定的键，则尝试获取其值
    /// </summary>
    /// <typeparam name="T">值的类型</typeparam>
    /// <param name="dictionary">字典对象</param>
    /// <param name="key">要查找的键</param>
    /// <param name="value">键对应的值，如果键不存在，则为默认值</param>
    /// <returns>如果字典中存在该键，则返回真(True)；否则返回假(False)</returns>
    public static bool TryGetValue<T>(this IDictionary<string, object> dictionary, string key, out T? value)
    {
        if (dictionary.TryGetValue(key, out var valueObj) && valueObj is T t)
        {
            value = t;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// 字典根据 key 删除，返回一个新的字典
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="keys"></param>
    /// <returns>移除后新的字典</returns>
    public static IDictionary<string, object> RemoveByKeys(this IDictionary<string, object> dictionary, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        if (keys.Length == 0)
        {
            return dictionary;
        }

        // 创建一个新的字典，避免修改原始字典
        var newDic = new Dictionary<string, object>(dictionary);
        foreach (var key in keys)
        {
            newDic.Remove(key);
        }
        return newDic;
    }
}
