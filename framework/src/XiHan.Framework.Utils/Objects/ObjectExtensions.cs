// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Objects;

/// <summary>
/// 对象扩展方法
/// </summary>
public static class ObjectExtensions
{
    #region 转换检查

    /// <summary>
    /// 用于简化和美化将对象转换为类型的操作
    /// </summary>
    /// <typeparam name="T">要转换的类型</typeparam>
    /// <param name="obj">要转换的对象</param>
    /// <returns>转换后的对象</returns>
    public static T As<T>(this object obj)
        where T : class
    {
        return (T)obj;
    }

    /// <summary>
    /// 使用 <see cref="Convert.ChangeType(object,Type)"/> 方法将给定对象转换为值类型
    /// </summary>
    /// <param name="obj">要转换的对象</param>
    /// <typeparam name="T">目标对象的类型</typeparam>
    /// <returns>转换后的对象</returns>
    public static T To<T>(this object obj)
        where T : struct
    {
        return typeof(T) == typeof(Guid)
            ? (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(obj.ToString()!)!
            : (T)Convert.ChangeType(obj, typeof(T), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 检查一个项目是否在列表中
    /// </summary>
    /// <param name="item">要检查的项目</param>
    /// <param name="list">项目列表</param>
    /// <typeparam name="T">项目的类型</typeparam>
    public static bool IsIn<T>(this T item, params T[] list)
    {
        return list.Contains(item);
    }

    /// <summary>
    /// 检查一个项目是否在给定的可枚举对象中
    /// </summary>
    /// <param name="item">要检查的项目</param>
    /// <param name="items">项目</param>
    /// <typeparam name="T">项目的类型</typeparam>
    public static bool IsIn<T>(this T item, IEnumerable<T> items)
    {
        return items.Contains(item);
    }

    /// <summary>
    /// 可用于有条件地对对象执行一个函数并返回修改后的或原始对象
    /// 它对于链式调用很有用
    /// </summary>
    /// <param name="obj">一个对象</param>
    /// <param name="condition">一个条件</param>
    /// <param name="func">仅当条件为 <code>true</code> 时才执行的函数</param>
    /// <typeparam name="T">对象的类型</typeparam>
    /// <returns>
    /// 如果 <paramref name="condition"/> 为 <code>true</code>，则返回由 <paramref name="func"/> 修改后的对象()
    /// 如果 <paramref name="condition"/> 为 <code>false</code>，则返回原始对象
    /// </returns>
    public static T If<T>(this T obj, bool condition, Func<T, T> func)
    {
        return condition ? func(obj) : obj;
    }

    /// <summary>
    /// 可用于有条件地对一个对象执行一个操作并返回原始对象
    /// 它对于对象的链式调用很有用
    /// </summary>
    /// <param name="obj">一个对象</param>
    /// <param name="condition">一个条件</param>
    /// <param name="action">仅当条件为 <code>true</code> 时才执行的操作</param>
    /// <typeparam name="T">对象的类型</typeparam>
    /// <returns>
    /// 返回原始对象
    /// </returns>
    public static T If<T>(this T obj, bool condition, Action<T> action)
    {
        if (condition)
        {
            action(obj);
        }

        return obj;
    }

    #endregion 转换检查

    #region 对象全名

    /// <summary>
    /// 获取对象全名
    /// </summary>
    /// <param name="instance"></param>
    /// <param name="fullName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string GetObjectFullNameOf(this object instance, [CallerArgumentExpression(nameof(instance))] string fullName = "")
    {
        return instance is null
            ? throw new ArgumentNullException(nameof(instance))
            : fullName ?? throw new ArgumentNullException(nameof(fullName));
    }

    #endregion 对象全名

    #region 字段信息

    /// <summary>
    /// 利用反射来判断对象是否包含某个字段
    /// </summary>
    /// <param name="instance">对象</param>
    /// <param name="fieldName">需要判断的字段</param>
    /// <returns>是否包含</returns>
    public static bool IsObjectContainField(this object? instance, string fieldName)
    {
        if (instance is null || string.IsNullOrEmpty(fieldName))
        {
            return false;
        }

        var foundFieldInfo = instance.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return foundFieldInfo is not null;
    }

    /// <summary>
    /// 利用反射来获取对象某字段信息
    /// </summary>
    /// <param name="instance">对象</param>
    /// <param name="fieldName">字段名称</param>
    /// <returns>字段信息，字段不存在时返回 null（先调 IsObjectContainField 或判空）</returns>
    /// <exception cref="ArgumentNullException">当实例为 null 时</exception>
    /// <exception cref="ArgumentException">当字段名为空时</exception>
    public static FieldInfo? GetObjectField(this object? instance, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        return instance.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    /// <summary>
    /// 利用反射来获取对象所有字段信息
    /// </summary>
    /// <param name="instance">对象</param>
    /// <returns>字段信息</returns>
    /// <exception cref="ArgumentNullException">当实例为 null 时</exception>
    public static FieldInfo[] GetObjectFields(this object? instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return instance.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    #endregion 字段信息

    #region 属性信息

    /// <summary>
    /// 利用反射来判断对象是否包含某个属性
    /// </summary>
    /// <param name="instance">对象</param>
    /// <param name="propertyName">需要判断的属性</param>
    /// <returns>是否包含</returns>
    public static bool IsContainObjectProperty(this object? instance, string propertyName)
    {
        if (instance is null || string.IsNullOrEmpty(propertyName))
        {
            return false;
        }

        var foundPropertyInfo = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return foundPropertyInfo is not null;
    }

    /// <summary>
    /// 利用反射来获取对象某属性信息
    /// </summary>
    /// <param name="instance">对象</param>
    /// <param name="propertyName">属性名称</param>
    /// <returns>属性信息，属性不存在时返回 null（先调 IsContainObjectProperty 或判空）</returns>
    /// <exception cref="ArgumentNullException">当实例为 null 时</exception>
    /// <exception cref="ArgumentException">当属性名为空时</exception>
    public static PropertyInfo? GetObjectProperty(this object? instance, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    /// <summary>
    /// 利用反射来获取对象所有属性信息
    /// </summary>
    /// <param name="instance">对象</param>
    /// <returns>属性信息</returns>
    /// <exception cref="ArgumentNullException">当实例为 null 时</exception>
    public static PropertyInfo[] GetObjectProperties(this object? instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        return instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    #endregion 属性信息

    #region 判断为空

    /// <summary>
    /// 判断对象是否为空，为空返回 true
    /// </summary>
    /// <param name="data">要验证的对象</param>
    public static bool IsNullOrEmpty(this object? data)
    {
        // 如果为 null
        if (data is null)
        {
            return true;
        }

        // 如果为空字符串（含仅空白）
        if (data is string text)
        {
            return string.IsNullOrEmpty(text.Trim());
        }

        // 原实现在这里直接 `if (data is not string) return data is DBNull;` 就结束了，
        // 于是任何非 null 非字符串对象（空 List、空数组、空字典……）一律被判成"非空"。
        // 这与 GenericExtensions.IsNullOrEmpty 已修掉的缺陷同源，语义在此对齐。
        if (data is ICollection collection)
        {
            return collection.Count == 0;
        }

        // 非泛型 IEnumerable 兜底：只取第一个元素判定，不整体枚举。
        // 惰性序列在此会被推进一步，这是判空的固有代价，与 Enumerable.Any() 一致。
        if (data is IEnumerable sequence)
        {
            var enumerator = sequence.GetEnumerator();
            try
            {
                return !enumerator.MoveNext();
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        // 如果为 DBNull
        return data is DBNull;
    }

    #endregion 判断为空
}
