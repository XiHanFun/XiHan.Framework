#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExtraPropertyDictionaryExtensions
// Guid:34adf497-02e5-4369-bb8e-98377f5c448c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 04:58:44
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.ObjectMapping.Extensions.Data;

/// <summary>
/// ExtraPropertyDictionary 扩展方法类
/// 提供额外属性字典的实用扩展方法，包括类型转换和比较功能
/// </summary>
public static class ExtraPropertyDictionaryExtensions
{
    /// <summary>
    /// 将指定键对应的值转换为枚举类型
    /// </summary>
    /// <typeparam name="T">目标枚举类型</typeparam>
    /// <param name="extraPropertyDictionary">额外属性字典</param>
    /// <param name="key">属性键</param>
    /// <returns>转换后的枚举值</returns>
    /// <exception cref="ArgumentNullException">当 extraPropertyDictionary 或 key 为 null 时</exception>
    /// <exception cref="ArgumentException">当值无法转换为指定枚举类型时</exception>
    public static T ToEnum<T>(this ExtraPropertyDictionary extraPropertyDictionary, string key)
        where T : Enum
    {
        ArgumentNullException.ThrowIfNull(extraPropertyDictionary);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (extraPropertyDictionary[key]!.GetType() == typeof(T))
        {
            return (T)extraPropertyDictionary[key]!;
        }

        extraPropertyDictionary[key] = Enum.Parse(typeof(T), extraPropertyDictionary[key]!.ToString()!, ignoreCase: true);
        return (T)extraPropertyDictionary[key]!;
    }

    /// <summary>
    /// 将指定键对应的值转换为指定的枚举类型
    /// </summary>
    /// <param name="extraPropertyDictionary">额外属性字典</param>
    /// <param name="key">属性键</param>
    /// <param name="enumType">目标枚举类型</param>
    /// <returns>转换后的枚举值</returns>
    /// <exception cref="ArgumentNullException">当参数为 null 时</exception>
    /// <exception cref="ArgumentException">当 enumType 不是枚举类型或值无法转换时</exception>
    public static object ToEnum(this ExtraPropertyDictionary extraPropertyDictionary, string key, Type enumType)
    {
        ArgumentNullException.ThrowIfNull(extraPropertyDictionary);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(enumType);

        if (!enumType.IsEnum || extraPropertyDictionary[key]!.GetType() == enumType)
        {
            return extraPropertyDictionary[key]!;
        }

        extraPropertyDictionary[key] = Enum.Parse(enumType, extraPropertyDictionary[key]!.ToString()!, ignoreCase: true);
        return extraPropertyDictionary[key]!;
    }

    /// <summary>
    /// 检查两个额外属性字典是否包含相同的键值对
    /// </summary>
    /// <param name="dictionary">第一个字典</param>
    /// <param name="otherDictionary">要比较的字典</param>
    /// <returns>如果两个字典包含相同的键值对则返回 true，否则返回 false</returns>
    /// <exception cref="ArgumentNullException">当任一字典为 null 时</exception>
    public static bool HasSameItems(this ExtraPropertyDictionary dictionary, ExtraPropertyDictionary otherDictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(otherDictionary);

        if (dictionary.Count != otherDictionary.Count)
        {
            return false;
        }

        foreach (var key in dictionary.Keys)
        {
            if (!otherDictionary.TryGetValue(key, out var value) ||
                dictionary[key]?.ToString() != value?.ToString())
            {
                return false;
            }
        }

        return true;
    }
}
