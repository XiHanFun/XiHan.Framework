#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueueExtensions
// Guid:8147166b-34b1-4e5e-b181-272e891ca7dc
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/06/05 01:59:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Collections;

/// <summary>
/// 队列扩展方法
/// </summary>
public static class QueueExtensions
{
    /// <summary>
    /// 批量入队
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="items">要入队的元素集合</param>
    /// <exception cref="ArgumentNullException">队列或元素集合为空时抛出</exception>
    public static void EnqueueRange<T>(this Queue<T> queue, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            queue.Enqueue(item);
        }
    }

    /// <summary>
    /// 批量出队
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="count">要出队的元素数量</param>
    /// <returns>出队的元素集合</returns>
    /// <exception cref="ArgumentNullException">队列为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">数量小于0或大于队列长度时抛出</exception>
    public static IEnumerable<T> DequeueRange<T>(this Queue<T> queue, int count)
    {
        ArgumentNullException.ThrowIfNull(queue);

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "数量不能小于0");
        }

        if (count > queue.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "数量不能大于队列长度");
        }

        var result = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(queue.Dequeue());
        }
        return result;
    }

    /// <summary>
    /// 尝试出队多个元素
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="count">要出队的元素数量</param>
    /// <param name="items">出队的元素集合</param>
    /// <returns>是否成功出队指定数量的元素</returns>
    public static bool TryDequeueRange<T>(this Queue<T> queue, int count, out IEnumerable<T> items)
    {
        items = [];

        if (count < 0 || count > queue.Count)
        {
            return false;
        }

        var result = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            if (queue.TryDequeue(out var item))
            {
                result.Add(item);
            }
            else
            {
                // 恢复已出队的元素
                foreach (var restoredItem in result.AsEnumerable().Reverse())
                {
                    var tempQueue = new Queue<T>();
                    tempQueue.Enqueue(restoredItem);
                    while (queue.Count > 0)
                    {
                        tempQueue.Enqueue(queue.Dequeue());
                    }
                    while (tempQueue.Count > 0)
                    {
                        queue.Enqueue(tempQueue.Dequeue());
                    }
                }
                return false;
            }
        }

        items = result;
        return true;
    }

    /// <summary>
    /// 清空队列并返回所有元素
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <returns>队列中的所有元素</returns>
    /// <exception cref="ArgumentNullException">队列为空时抛出</exception>
    public static IEnumerable<T> DrainToList<T>(this Queue<T> queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        var result = new List<T>(queue.Count);
        while (queue.Count > 0)
        {
            result.Add(queue.Dequeue());
        }
        return result;
    }

    /// <summary>
    /// 安全地查看队列头部元素
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="item">队列头部元素</param>
    /// <returns>是否成功查看</returns>
    public static bool TryPeek<T>(this Queue<T> queue, out T? item)
    {
        item = default;
        if (queue.Count == 0)
        {
            return false;
        }

        item = queue.Peek();
        return true;
    }

    /// <summary>
    /// 检查队列是否为空
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <returns>队列是否为空</returns>
    public static bool IsEmpty<T>(this Queue<T> queue)
    {
        return queue?.Count == 0;
    }

    /// <summary>
    /// 检查队列是否不为空
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <returns>队列是否不为空</returns>
    public static bool IsNotEmpty<T>(this Queue<T> queue)
    {
        return queue?.Count > 0;
    }

    /// <summary>
    /// 将队列转换为数组
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <returns>包含队列所有元素的数组</returns>
    /// <exception cref="ArgumentNullException">队列为空时抛出</exception>
    public static T[] ToArrayPreserveOrder<T>(this Queue<T> queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return [.. queue];
    }

    /// <summary>
    /// 复制队列
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">原队列</param>
    /// <returns>复制的新队列</returns>
    /// <exception cref="ArgumentNullException">队列为空时抛出</exception>
    public static Queue<T> Clone<T>(this Queue<T> queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        return new Queue<T>(queue);
    }

    /// <summary>
    /// 查找队列中是否包含满足条件的元素
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>是否包含满足条件的元素</returns>
    /// <exception cref="ArgumentNullException">队列或条件为空时抛出</exception>
    public static bool Contains<T>(this Queue<T> queue, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(predicate);

        return queue.Any(predicate);
    }

    /// <summary>
    /// 统计队列中满足条件的元素数量
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>满足条件的元素数量</returns>
    /// <exception cref="ArgumentNullException">队列或条件为空时抛出</exception>
    public static int Count<T>(this Queue<T> queue, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(predicate);

        return queue.Count(predicate);
    }

    /// <summary>
    /// 对队列中的每个元素执行指定操作
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="action">要执行的操作</param>
    /// <exception cref="ArgumentNullException">队列或操作为空时抛出</exception>
    public static void ForEach<T>(this Queue<T> queue, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(action);

        foreach (var item in queue)
        {
            action(item);
        }
    }

    /// <summary>
    /// 对队列中的每个元素执行指定操作（带索引）
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="action">要执行的操作，参数为元素和索引</param>
    /// <exception cref="ArgumentNullException">队列或操作为空时抛出</exception>
    public static void ForEach<T>(this Queue<T> queue, Action<T, int> action)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(action);

        var index = 0;
        foreach (var item in queue)
        {
            action(item, index++);
        }
    }

    /// <summary>
    /// 创建一个新队列，包含满足条件的元素
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">原队列</param>
    /// <param name="predicate">筛选条件</param>
    /// <returns>包含满足条件元素的新队列</returns>
    /// <exception cref="ArgumentNullException">队列或条件为空时抛出</exception>
    public static Queue<T> Where<T>(this Queue<T> queue, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(predicate);

        var result = new Queue<T>();
        foreach (var item in queue.Where(predicate))
        {
            result.Enqueue(item);
        }
        return result;
    }

    /// <summary>
    /// 创建一个新队列，包含转换后的元素
    /// </summary>
    /// <typeparam name="TSource">原队列元素类型</typeparam>
    /// <typeparam name="TResult">目标队列元素类型</typeparam>
    /// <param name="queue">原队列</param>
    /// <param name="selector">转换函数</param>
    /// <returns>包含转换后元素的新队列</returns>
    /// <exception cref="ArgumentNullException">队列或转换函数为空时抛出</exception>
    public static Queue<TResult> Select<TSource, TResult>(this Queue<TSource> queue, Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(selector);

        var result = new Queue<TResult>();
        foreach (var item in queue.Select(selector))
        {
            result.Enqueue(item);
        }
        return result;
    }

    /// <summary>
    /// 限制队列的最大长度，超出时移除最旧的元素
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="maxSize">最大长度</param>
    /// <exception cref="ArgumentNullException">队列为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">最大长度小于0时抛出</exception>
    public static void LimitSize<T>(this Queue<T> queue, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(queue);

        if (maxSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "最大长度不能小于0");
        }

        while (queue.Count > maxSize)
        {
            queue.Dequeue();
        }
    }

    /// <summary>
    /// 安全地入队元素，如果队列已满则移除最旧的元素
    /// </summary>
    /// <typeparam name="T">队列元素类型</typeparam>
    /// <param name="queue">队列实例</param>
    /// <param name="item">要入队的元素</param>
    /// <param name="maxSize">队列最大长度</param>
    /// <returns>被移除的元素（如果有）</returns>
    /// <exception cref="ArgumentNullException">队列为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">最大长度小于1时抛出</exception>
    public static T? EnqueueWithLimit<T>(this Queue<T> queue, T item, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(queue);

        if (maxSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "最大长度不能小于1");
        }

        T? removedItem = default;

        if (queue.Count >= maxSize)
        {
            removedItem = queue.Dequeue();
        }

        queue.Enqueue(item);
        return removedItem;
    }
}
