﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CollectionExtensions
// Guid:898d6533-96e8-445d-aed8-5c04d707696f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/22 2:08:11
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Utils.Collections;

/// <summary>
/// 集合扩展方法
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// 检查给定的集合对象是否为空或者没有任何项
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">要检查的集合</param>
    /// <returns>如果集合为null或空则返回true，否则返回false</returns>
    public static bool IsNullOrEmpty<T>(this ICollection<T>? source)
    {
        return source is not { Count: > 0 };
    }

    /// <summary>
    /// 如果条件成立，添加项
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">要操作的集合</param>
    /// <param name="value">要添加的值</param>
    /// <param name="flag">条件标志，为true时添加项</param>
    public static void AddIf<T>(this ICollection<T> source, T value, bool flag)
    {
        _ = Guard.NotNull(source, nameof(source));

        if (flag)
        {
            source.Add(value);
        }
    }

    /// <summary>
    /// 如果条件成立，添加项
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">要操作的集合</param>
    /// <param name="value">要添加的值</param>
    /// <param name="func">条件函数，返回true时添加项</param>
    public static void AddIf<T>(this ICollection<T> source, T value, Func<bool> func)
    {
        _ = Guard.NotNull(source, nameof(source));

        if (func())
        {
            source.Add(value);
        }
    }

    /// <summary>
    /// 如果给定的集合对象不为空，则添加一个项
    /// </summary>
    /// <typeparam name="T">集合元素类型</typeparam>
    /// <param name="source">要操作的集合</param>
    /// <param name="value">要添加的值（如果不为null）</param>
    public static void AddIfNotNull<T>(this ICollection<T> source, T value)
    {
        _ = Guard.NotNull(source, nameof(source));

        if (value is not null)
        {
            source.Add(value);
        }
    }

    /// <summary>
    /// 如果集合中尚未包含该项，则将其添加到集合中
    /// </summary>
    /// <typeparam name="T">集合中项的类型</typeparam>
    /// <param name="source">集合对象</param>
    /// <param name="item">要检查并添加的项</param>
    /// <returns>如果添加了项，则返回真(True)；如果没有添加(即项已存在)则返回假(False)</returns>
    public static bool AddIfNotContains<T>(this ICollection<T> source, T item)
    {
        _ = Guard.NotNull(source, nameof(source));

        if (source.Contains(item))
        {
            return false;
        }

        source.Add(item);
        return true;
    }

    /// <summary>
    /// 向集合中添加尚未包含的项
    /// </summary>
    /// <typeparam name="T">集合中项的类型</typeparam>
    /// <param name="source">集合对象</param>
    /// <param name="items">要检查并添加的项的集合</param>
    /// <returns>返回添加的项的集合</returns>
    public static IEnumerable<T> AddIfNotContains<T>(this ICollection<T> source, IEnumerable<T> items)
    {
        _ = Guard.NotNull(source, nameof(source));
        var enumerable = items as T[] ?? [.. items];
        _ = Guard.NotNull(enumerable, nameof(items));

        List<T> addedItems = [];

        foreach (var item in enumerable)
        {
            if (source.Contains(item))
            {
                continue;
            }

            source.Add(item);
            addedItems.Add(item);
        }

        return addedItems;
    }

    /// <summary>
    /// 如果集合中尚未包含满足给定谓词条件的项，则将项添加到集合中
    /// </summary>
    /// <typeparam name="T">集合中项的类型</typeparam>
    /// <param name="source">集合对象</param>
    /// <param name="predicate">决定项是否已存在于集合中的条件</param>
    /// <param name="itemFactory">返回项的工厂函数</param>
    /// <returns>如果添加了项，则返回真(True)；如果没有添加(即项已存在)则返回假(False)</returns>
    public static bool AddIfNotContains<T>(this ICollection<T> source, Func<T, bool> predicate, Func<T> itemFactory)
    {
        _ = Guard.NotNull(source, nameof(source));

        if (source.Any(predicate))
        {
            return false;
        }

        source.Add(itemFactory());
        return true;
    }

    /// <summary>
    /// 移除集合中所有满足给定谓词条件的项
    /// </summary>
    /// <typeparam name="T">集合中项的类型</typeparam>
    /// <param name="source">集合对象</param>
    /// <param name="predicate">用于移除项的条件</param>
    /// <returns>被移除项的列表</returns>
    public static IList<T> RemoveAllWhere<T>(this ICollection<T> source, Func<T, bool> predicate)
    {
        _ = Guard.NotNull(source, nameof(source));

        var items = source.Where(predicate).ToList();

        foreach (var item in items)
        {
            _ = source.Remove(item);
        }

        return items;
    }

    /// <summary>
    /// 从集合中移除所有指定的项
    /// </summary>
    /// <typeparam name="T">集合中项的类型</typeparam>
    /// <param name="source">集合对象</param>
    /// <param name="items">要移除的项的集合</param>
    public static void RemoveAll<T>(this ICollection<T> source, IEnumerable<T> items)
    {
        _ = Guard.NotNull(source, nameof(source));
        var enumerable = items as T[] ?? [.. items];
        _ = Guard.NotNull(enumerable, nameof(items));

        foreach (var item in enumerable)
        {
            _ = source.Remove(item);
        }
    }
}
