#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:LinkedListExtensions
// Guid:ca84b56f-aea1-4cf1-9657-bd46103bb89a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/5 2:05:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Collections;

/// <summary>
/// 链表扩展方法
/// </summary>
public static class LinkedListExtensions
{
    /// <summary>
    /// 批量添加元素到链表末尾
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="items">要添加的元素集合</param>
    /// <exception cref="ArgumentNullException">链表或元素集合为空时抛出</exception>
    public static void AddRange<T>(this LinkedList<T> list, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            list.AddLast(item);
        }
    }

    /// <summary>
    /// 批量添加元素到链表开头
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="items">要添加的元素集合</param>
    /// <exception cref="ArgumentNullException">链表或元素集合为空时抛出</exception>
    public static void AddRangeFirst<T>(this LinkedList<T> list, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(items);

        // 从末尾开始添加，保持顺序
        var itemsArray = items.ToArray();
        for (var i = itemsArray.Length - 1; i >= 0; i--)
        {
            list.AddFirst(itemsArray[i]);
        }
    }

    /// <summary>
    /// 在指定节点后批量插入元素
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="node">参考节点</param>
    /// <param name="items">要插入的元素集合</param>
    /// <exception cref="ArgumentNullException">链表、节点或元素集合为空时抛出</exception>
    public static void AddRangeAfter<T>(this LinkedList<T> list, LinkedListNode<T> node, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(items);

        var currentNode = node;
        foreach (var item in items)
        {
            currentNode = list.AddAfter(currentNode, item);
        }
    }

    /// <summary>
    /// 在指定节点前批量插入元素
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="node">参考节点</param>
    /// <param name="items">要插入的元素集合</param>
    /// <exception cref="ArgumentNullException">链表、节点或元素集合为空时抛出</exception>
    public static void AddRangeBefore<T>(this LinkedList<T> list, LinkedListNode<T> node, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(items);

        var currentNode = node;
        // 从末尾开始添加，保持顺序
        var itemsArray = items.ToArray();
        for (var i = itemsArray.Length - 1; i >= 0; i--)
        {
            currentNode = list.AddBefore(currentNode, itemsArray[i]);
        }
    }

    /// <summary>
    /// 查找满足条件的第一个节点
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>满足条件的第一个节点，如果未找到则返回null</returns>
    /// <exception cref="ArgumentNullException">链表或条件为空时抛出</exception>
    public static LinkedListNode<T>? FindFirst<T>(this LinkedList<T> list, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(predicate);

        var current = list.First;
        while (current != null)
        {
            if (predicate(current.Value))
            {
                return current;
            }
            current = current.Next;
        }
        return null;
    }

    /// <summary>
    /// 查找满足条件的最后一个节点
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>满足条件的最后一个节点，如果未找到则返回null</returns>
    /// <exception cref="ArgumentNullException">链表或条件为空时抛出</exception>
    public static LinkedListNode<T>? FindLast<T>(this LinkedList<T> list, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(predicate);

        var current = list.Last;
        while (current != null)
        {
            if (predicate(current.Value))
            {
                return current;
            }
            current = current.Previous;
        }
        return null;
    }

    /// <summary>
    /// 查找所有满足条件的节点
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>满足条件的所有节点</returns>
    /// <exception cref="ArgumentNullException">链表或条件为空时抛出</exception>
    public static IEnumerable<LinkedListNode<T>> FindAll<T>(this LinkedList<T> list, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(predicate);

        var result = new List<LinkedListNode<T>>();
        var current = list.First;
        while (current != null)
        {
            if (predicate(current.Value))
            {
                result.Add(current);
            }
            current = current.Next;
        }
        return result;
    }

    /// <summary>
    /// 移除所有满足条件的节点
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="predicate">移除条件</param>
    /// <returns>移除的节点数量</returns>
    /// <exception cref="ArgumentNullException">链表或条件为空时抛出</exception>
    public static int RemoveAll<T>(this LinkedList<T> list, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(predicate);

        var count = 0;
        var current = list.First;
        while (current != null)
        {
            var next = current.Next;
            if (predicate(current.Value))
            {
                list.Remove(current);
                count++;
            }
            current = next;
        }
        return count;
    }

    /// <summary>
    /// 反转链表
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <exception cref="ArgumentNullException">链表为空时抛出</exception>
    public static void Reverse<T>(this LinkedList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        if (list.Count <= 1)
        {
            return;
        }

        var items = new T[list.Count];
        var index = list.Count - 1;
        
        var current = list.First;
        while (current != null)
        {
            items[index--] = current.Value;
            current = current.Next;
        }

        list.Clear();
        foreach (var item in items)
        {
            list.AddLast(item);
        }
    }

    /// <summary>
    /// 获取指定索引处的节点
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="index">索引位置</param>
    /// <returns>指定索引处的节点</returns>
    /// <exception cref="ArgumentNullException">链表为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">索引超出范围时抛出</exception>
    public static LinkedListNode<T> GetNodeAt<T>(this LinkedList<T> list, int index)
    {
        ArgumentNullException.ThrowIfNull(list);
        
        if (index < 0 || index >= list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "索引超出范围");
        }

        LinkedListNode<T>? current;
        var count = list.Count;
        
        // 根据索引位置选择从头还是从尾开始遍历
        if (index < count / 2)
        {
            current = list.First;
            for (var i = 0; i < index; i++)
            {
                current = current!.Next;
            }
        }
        else
        {
            current = list.Last;
            for (var i = count - 1; i > index; i--)
            {
                current = current!.Previous;
            }
        }

        return current!;
    }

    /// <summary>
    /// 安全获取指定索引处的节点
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="index">索引位置</param>
    /// <param name="node">获取到的节点</param>
    /// <returns>是否成功获取节点</returns>
    public static bool TryGetNodeAt<T>(this LinkedList<T> list, int index, out LinkedListNode<T>? node)
    {
        node = null;
        
        if (list == null || index < 0 || index >= list.Count)
        {
            return false;
        }

        try
        {
            node = list.GetNodeAt(index);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查链表是否为空
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <returns>链表是否为空</returns>
    public static bool IsEmpty<T>(this LinkedList<T> list)
    {
        return list?.Count == 0;
    }

    /// <summary>
    /// 检查链表是否不为空
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <returns>链表是否不为空</returns>
    public static bool IsNotEmpty<T>(this LinkedList<T> list)
    {
        return list?.Count > 0;
    }

    /// <summary>
    /// 将链表转换为数组，保持顺序
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <returns>包含链表所有元素的数组</returns>
    /// <exception cref="ArgumentNullException">链表为空时抛出</exception>
    public static T[] ToArrayPreserveOrder<T>(this LinkedList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return [.. list];
    }

    /// <summary>
    /// 复制链表
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">原链表</param>
    /// <returns>复制的新链表</returns>
    /// <exception cref="ArgumentNullException">链表为空时抛出</exception>
    public static LinkedList<T> Clone<T>(this LinkedList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return new LinkedList<T>(list);
    }

    /// <summary>
    /// 对链表中的每个元素执行指定操作
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="action">要执行的操作</param>
    /// <exception cref="ArgumentNullException">链表或操作为空时抛出</exception>
    public static void ForEach<T>(this LinkedList<T> list, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(action);

        var current = list.First;
        while (current != null)
        {
            action(current.Value);
            current = current.Next;
        }
    }

    /// <summary>
    /// 对链表中的每个元素执行指定操作（带索引）
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="action">要执行的操作，参数为元素和索引</param>
    /// <exception cref="ArgumentNullException">链表或操作为空时抛出</exception>
    public static void ForEach<T>(this LinkedList<T> list, Action<T, int> action)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(action);

        var index = 0;
        var current = list.First;
        while (current != null)
        {
            action(current.Value, index++);
            current = current.Next;
        }
    }

    /// <summary>
    /// 对链表中的每个节点执行指定操作
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="action">要执行的操作</param>
    /// <exception cref="ArgumentNullException">链表或操作为空时抛出</exception>
    public static void ForEachNode<T>(this LinkedList<T> list, Action<LinkedListNode<T>> action)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(action);

        var current = list.First;
        while (current != null)
        {
            var next = current.Next; // 保存下一个节点，防止操作中删除当前节点
            action(current);
            current = next;
        }
    }

    /// <summary>
    /// 统计满足条件的元素数量
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>满足条件的元素数量</returns>
    /// <exception cref="ArgumentNullException">链表或条件为空时抛出</exception>
    public static int Count<T>(this LinkedList<T> list, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(predicate);

        return list.Count(predicate);
    }

    /// <summary>
    /// 检查是否包含满足条件的元素
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>是否包含满足条件的元素</returns>
    /// <exception cref="ArgumentNullException">链表或条件为空时抛出</exception>
    public static bool Any<T>(this LinkedList<T> list, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(predicate);

        return list.Any(predicate);
    }

    /// <summary>
    /// 检查是否所有元素都满足条件
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>是否所有元素都满足条件</returns>
    /// <exception cref="ArgumentNullException">链表或条件为空时抛出</exception>
    public static bool All<T>(this LinkedList<T> list, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(predicate);

        return list.All(predicate);
    }

    /// <summary>
    /// 创建一个新链表，包含满足条件的元素
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">原链表</param>
    /// <param name="predicate">筛选条件</param>
    /// <returns>包含满足条件元素的新链表</returns>
    /// <exception cref="ArgumentNullException">链表或条件为空时抛出</exception>
    public static LinkedList<T> Where<T>(this LinkedList<T> list, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(predicate);

        var result = new LinkedList<T>();
        var current = list.First;
        while (current != null)
        {
            if (predicate(current.Value))
            {
                result.AddLast(current.Value);
            }
            current = current.Next;
        }
        return result;
    }

    /// <summary>
    /// 创建一个新链表，包含转换后的元素
    /// </summary>
    /// <typeparam name="TSource">原链表元素类型</typeparam>
    /// <typeparam name="TResult">目标链表元素类型</typeparam>
    /// <param name="list">原链表</param>
    /// <param name="selector">转换函数</param>
    /// <returns>包含转换后元素的新链表</returns>
    /// <exception cref="ArgumentNullException">链表或转换函数为空时抛出</exception>
    public static LinkedList<TResult> Select<TSource, TResult>(this LinkedList<TSource> list, Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(selector);

        var result = new LinkedList<TResult>();
        var current = list.First;
        while (current != null)
        {
            result.AddLast(selector(current.Value));
            current = current.Next;
        }
        return result;
    }

    /// <summary>
    /// 合并两个链表
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="first">第一个链表</param>
    /// <param name="second">第二个链表</param>
    /// <returns>合并后的新链表</returns>
    /// <exception cref="ArgumentNullException">任一链表为空时抛出</exception>
    public static LinkedList<T> Concat<T>(this LinkedList<T> first, LinkedList<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var result = new LinkedList<T>(first);
        var current = second.First;
        while (current != null)
        {
            result.AddLast(current.Value);
            current = current.Next;
        }
        return result;
    }

    /// <summary>
    /// 限制链表的最大长度，超出时移除头部元素
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="maxSize">最大长度</param>
    /// <exception cref="ArgumentNullException">链表为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">最大长度小于0时抛出</exception>
    public static void LimitSize<T>(this LinkedList<T> list, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(list);
        
        if (maxSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "最大长度不能小于0");
        }

        while (list.Count > maxSize)
        {
            list.RemoveFirst();
        }
    }

    /// <summary>
    /// 安全地添加元素到末尾，如果链表已满则移除头部元素
    /// </summary>
    /// <typeparam name="T">链表元素类型</typeparam>
    /// <param name="list">链表实例</param>
    /// <param name="item">要添加的元素</param>
    /// <param name="maxSize">链表最大长度</param>
    /// <returns>被移除的元素（如果有）</returns>
    /// <exception cref="ArgumentNullException">链表为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">最大长度小于1时抛出</exception>
    public static T? AddLastWithLimit<T>(this LinkedList<T> list, T item, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(list);
        
        if (maxSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "最大长度不能小于1");
        }

        T? removedItem = default;
        
        if (list.Count >= maxSize)
        {
            removedItem = list.First!.Value;
            list.RemoveFirst();
        }
        
        list.AddLast(item);
        return removedItem;
    }
}
