#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ListExtensions
// Guid:bdfc0ecc-da90-4a92-9f6e-4a7f0a57ba65
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/22 02:43:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.Extensions;

namespace XiHan.Framework.Utils.Collections;

/// <summary>
/// 列表扩展方法
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// 在指定索引的位置插入一个序列的项到列表中
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="index">要插入序列的起始索引</param>
    /// <param name="items">要插入的项的集合</param>
    public static void InsertRange<T>(this IList<T> source, int index, IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            source.Insert(index++, item);
        }
    }

    /// <summary>
    /// 查找列表中满足特定条件的第一个项的索引
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要搜索的列表</param>
    /// <param name="selector">用于测试列表中每个项的条件</param>
    /// <returns>满足条件项的索引，如果未找到则返回 -1</returns>
    public static int FindIndex<T>(this IList<T> source, Predicate<T> selector)
    {
        for (var i = 0; i < source.Count; ++i)
        {
            if (selector(source[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 在列表开头添加一个项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="item">要添加的项</param>
    public static void AddFirst<T>(this IList<T> source, T item)
    {
        source.Insert(0, item);
    }

    /// <summary>
    /// 在列表末尾添加一个项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="item">要添加的项</param>
    public static void AddLast<T>(this IList<T> source, T item)
    {
        source.Insert(source.Count, item);
    }

    /// <summary>
    /// 在列表中指定项之后插入一个新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="existingItem">列表中已存在的项</param>
    /// <param name="item">要插入的新项</param>
    public static void InsertAfter<T>(this IList<T> source, T existingItem, T item)
    {
        var index = source.IndexOf(existingItem);
        if (index < 0)
        {
            source.AddFirst(item);
            return;
        }

        source.Insert(index + 1, item);
    }

    /// <summary>
    /// 根据选择器找到的项之后插入一个新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="selector">用于查找应插入新项之后项的选择器</param>
    /// <param name="item">要插入的新项</param>
    public static void InsertAfter<T>(this IList<T> source, Predicate<T> selector, T item)
    {
        var index = source.FindIndex(selector);
        if (index < 0)
        {
            source.AddFirst(item);
            return;
        }

        source.Insert(index + 1, item);
    }

    /// <summary>
    /// 在列表中指定项之前插入一个新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="existingItem">列表中已存在的项</param>
    /// <param name="item">要插入的新项</param>
    public static void InsertBefore<T>(this IList<T> source, T existingItem, T item)
    {
        var index = source.IndexOf(existingItem);
        if (index < 0)
        {
            source.AddLast(item);
            return;
        }

        source.Insert(index, item);
    }

    /// <summary>
    /// 根据选择器找到的项之前插入一个新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="selector">用于查找应插入新项之前项的选择器</param>
    /// <param name="item">要插入的新项</param>
    public static void InsertBefore<T>(this IList<T> source, Predicate<T> selector, T item)
    {
        var index = source.FindIndex(selector);
        if (index < 0)
        {
            source.AddLast(item);
            return;
        }

        source.Insert(index, item);
    }

    /// <summary>
    /// 遍历列表，替换所有满足特定条件的项为指定的新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="selector">用于测试列表中每个项的条件</param>
    /// <param name="item">要替换的新项</param>
    public static void ReplaceWhile<T>(this IList<T> source, Predicate<T> selector, T item)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (selector(source[i]))
            {
                source[i] = item;
            }
        }
    }

    /// <summary>
    /// 遍历列表，替换所有满足特定条件的项为由工厂方法生成的新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="selector">用于测试列表中每个项的条件</param>
    /// <param name="itemFactory">一个工厂方法，用于生成要替换的新项</param>
    public static void ReplaceWhile<T>(this IList<T> source, Predicate<T> selector, Func<T, T> itemFactory)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (selector(item))
            {
                source[i] = itemFactory(item);
            }
        }
    }

    /// <summary>
    /// 遍历列表，替换第一个满足特定条件的项为指定的新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="selector">用于测试列表中每个项的条件</param>
    /// <param name="item">要替换的新项</param>
    public static void ReplaceOne<T>(this IList<T> source, Predicate<T> selector, T item)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (!selector(source[i]))
            {
                continue;
            }

            source[i] = item;
            return;
        }
    }

    /// <summary>
    /// 遍历列表，替换第一个满足特定条件的项为由工厂方法生成的新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="selector">用于测试列表中每个项的条件</param>
    /// <param name="itemFactory">一个工厂方法，用于生成要替换的新项</param>
    public static void ReplaceOne<T>(this IList<T> source, Predicate<T> selector, Func<T, T> itemFactory)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            if (!selector(item))
            {
                continue;
            }

            source[i] = itemFactory(item);
            return;
        }
    }

    /// <summary>
    /// 遍历列表，替换第一个匹配指定项的项为新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="item">要被替换的项</param>
    /// <param name="replaceWith">新项</param>
    public static void ReplaceOne<T>(this IList<T> source, T item, T replaceWith)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (Comparer<T>.Default.Compare(source[i], item) != 0)
            {
                continue;
            }

            source[i] = replaceWith;
            return;
        }
    }

    /// <summary>
    /// 根据给定的选择器找到列表中的项，并将其移动到目标索引位置
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="selector">用于选择要移动的项的选择器</param>
    /// <param name="targetIndex">项移动到的目标索引位置</param>
    public static void MoveItem<T>(this List<T> source, Predicate<T> selector, int targetIndex)
    {
        // 检查目标索引是否在有效范围内
        if (!targetIndex.IsInRange(0, source.Count - 1))
        {
            throw new IndexOutOfRangeException("目标索引应在 0 到 " + (source.Count - 1) + " 之间");
        }

        // 查找当前项的索引
        var currentIndex = source.FindIndex(0, selector);
        if (currentIndex == targetIndex)
        {
            return;
        }

        // 移除当前项并插入到目标索引位置
        var item = source[currentIndex];
        source.RemoveAt(currentIndex);
        source.Insert(targetIndex, item);
    }

    /// <summary>
    /// 尝试获取列表中满足特定条件的第一个项，如果没有找到则添加新项
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="selector">用于选择项的谓词</param>
    /// <param name="factory">如果没有找到匹配项，则用于创建新项的工厂方法</param>
    /// <returns>返回找到的项或新添加的项</returns>
    public static T GetOrAdd<T>(this IList<T> source, Func<T, bool> selector, Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        var item = source.FirstOrDefault(selector);

        if (item is not null && !EqualityComparer<T>.Default.Equals(item, default))
        {
            return item;
        }

        item = factory();
        source.Add(item);

        return item;
    }

    /// <summary>
    /// 从列表中随机获取一个元素
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <returns>随机选中的元素</returns>
    /// <exception cref="ArgumentException">当列表为空时抛出异常</exception>
    public static T GetRandom<T>(this IList<T> source)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        if (source.Count == 0)
        {
            throw new ArgumentException("列表不能为空", nameof(source));
        }

        var randomIndex = Random.Shared.Next(source.Count);
        return source[randomIndex];
    }

    /// <summary>
    /// 尝试从列表中随机获取一个元素
    /// </summary>
    /// <typeparam name="T">列表中项的类型</typeparam>
    /// <param name="source">要操作的列表</param>
    /// <param name="result">输出参数，包含随机选中的元素（如果成功）</param>
    /// <returns>如果列表不为空则返回 true，否则返回 false</returns>
    public static bool TryGetRandom<T>(this IList<T> source, out T? result)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        if (source.Count == 0)
        {
            result = default;
            return false;
        }

        var randomIndex = Random.Shared.Next(source.Count);
        result = source[randomIndex];
        return true;
    }
}
