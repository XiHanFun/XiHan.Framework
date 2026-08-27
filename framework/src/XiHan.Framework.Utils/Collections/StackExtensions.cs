// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Utils.Collections;

/// <summary>
/// 堆栈扩展方法
/// </summary>
public static class StackExtensions
{
    /// <summary>
    /// 批量入栈
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="items">要入栈的元素集合</param>
    /// <exception cref="ArgumentNullException">堆栈或元素集合为空时抛出</exception>
    public static void PushRange<T>(this Stack<T> stack, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            stack.Push(item);
        }
    }

    /// <summary>
    /// 批量入栈（保持集合的原始顺序）
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="items">要入栈的元素集合</param>
    /// <exception cref="ArgumentNullException">堆栈或元素集合为空时抛出</exception>
    public static void PushRangeReversed<T>(this Stack<T> stack, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(items);

        // 反转顺序入栈，使得弹出时保持原始顺序
        var itemsArray = items.ToArray();
        for (var i = itemsArray.Length - 1; i >= 0; i--)
        {
            stack.Push(itemsArray[i]);
        }
    }

    /// <summary>
    /// 批量出栈
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="count">要出栈的元素数量</param>
    /// <returns>出栈的元素集合</returns>
    /// <exception cref="ArgumentNullException">堆栈为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">数量小于0或大于堆栈长度时抛出</exception>
    public static IEnumerable<T> PopRange<T>(this Stack<T> stack, int count)
    {
        ArgumentNullException.ThrowIfNull(stack);

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "数量不能小于0");
        }

        if (count > stack.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "数量不能大于堆栈长度");
        }

        var result = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(stack.Pop());
        }
        return result;
    }

    /// <summary>
    /// 尝试出栈多个元素
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="count">要出栈的元素数量</param>
    /// <param name="items">出栈的元素集合</param>
    /// <returns>是否成功出栈指定数量的元素</returns>
    public static bool TryPopRange<T>(this Stack<T> stack, int count, out IEnumerable<T> items)
    {
        items = [];

        if (count < 0 || count > stack.Count)
        {
            return false;
        }

        var result = new List<T>(count);
        var tempItems = new List<T>();

        for (var i = 0; i < count; i++)
        {
            if (stack.TryPop(out var item))
            {
                result.Add(item);
                tempItems.Add(item);
            }
            else
            {
                // 恢复已出栈的元素
                for (var j = tempItems.Count - 1; j >= 0; j--)
                {
                    stack.Push(tempItems[j]);
                }
                return false;
            }
        }

        items = result;
        return true;
    }

    /// <summary>
    /// 清空堆栈并返回所有元素
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <returns>堆栈中的所有元素</returns>
    /// <exception cref="ArgumentNullException">堆栈为空时抛出</exception>
    public static IEnumerable<T> DrainToList<T>(this Stack<T> stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        var result = new List<T>(stack.Count);
        while (stack.Count > 0)
        {
            result.Add(stack.Pop());
        }
        return result;
    }

    /// <summary>
    /// 安全地查看堆栈顶部元素
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="item">堆栈顶部元素</param>
    /// <returns>是否成功查看</returns>
    public static bool TryPeek<T>(this Stack<T> stack, out T? item)
    {
        item = default;
        if (stack.Count == 0)
        {
            return false;
        }

        item = stack.Peek();
        return true;
    }

    /// <summary>
    /// 安全地查看多个顶部元素（不出栈）
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="count">要查看的元素数量</param>
    /// <returns>顶部指定数量的元素</returns>
    /// <exception cref="ArgumentNullException">堆栈为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">数量小于0或大于堆栈长度时抛出</exception>
    public static IEnumerable<T> PeekRange<T>(this Stack<T> stack, int count)
    {
        ArgumentNullException.ThrowIfNull(stack);

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "数量不能小于0");
        }

        if (count > stack.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "数量不能大于堆栈长度");
        }

        var items = stack.ToArray();
        return items.Take(count);
    }

    /// <summary>
    /// 检查堆栈是否为空
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <returns>堆栈是否为空</returns>
    public static bool IsEmpty<T>(this Stack<T> stack)
    {
        return stack?.Count == 0;
    }

    /// <summary>
    /// 检查堆栈是否不为空
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <returns>堆栈是否不为空</returns>
    public static bool IsNotEmpty<T>(this Stack<T> stack)
    {
        return stack?.Count > 0;
    }

    /// <summary>
    /// 将堆栈转换为数组，保持堆栈顺序
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <returns>包含堆栈所有元素的数组</returns>
    /// <exception cref="ArgumentNullException">堆栈为空时抛出</exception>
    public static T[] ToArrayPreserveOrder<T>(this Stack<T> stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        return [.. stack];
    }

    /// <summary>
    /// 复制堆栈
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">原堆栈</param>
    /// <returns>复制的新堆栈</returns>
    /// <exception cref="ArgumentNullException">堆栈为空时抛出</exception>
    public static Stack<T> Clone<T>(this Stack<T> stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        // 原实现是 new Stack<T>(stack.ToArray())：ToArray 给出的是"栈顶到栈底"顺序，
        // 而 Stack<T>(IEnumerable<T>) 按枚举顺序逐个 Push，于是原栈顶被压到了新栈底，
        // 克隆结果与注释承诺的"保持原始堆栈的顺序"正好相反。
        // 改为按"栈底到栈顶"回压（与同文件 DeepClone 的两次反转等价），枚举顺序才与源栈一致。
        var items = stack.ToArray();
        var result = new Stack<T>(items.Length);
        for (var i = items.Length - 1; i >= 0; i--)
        {
            result.Push(items[i]);
        }
        return result;
    }

    /// <summary>
    /// 查找堆栈中是否包含满足条件的元素
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>是否包含满足条件的元素</returns>
    /// <exception cref="ArgumentNullException">堆栈或条件为空时抛出</exception>
    public static bool Contains<T>(this Stack<T> stack, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(predicate);

        // 原实现写的是 stack.Any(predicate)：扩展方法先在本命名空间内找候选，
        // 本文件的 Any(Stack<T>, Func<T,bool>) 对 Stack<T> 是恒等转换、优先于 Enumerable.Any
        // 的 Stack<T>→IEnumerable<T> 引用转换，于是解析回自身，调用即无限递归、栈溢出杀进程。
        // 本文件所有"与 Enumerable 同名"的谓词重载一律改成 Enumerable.Xxx(...) 限定调用。
        return Enumerable.Any(stack, predicate);
    }

    /// <summary>
    /// 统计堆栈中满足条件的元素数量
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>满足条件的元素数量</returns>
    /// <exception cref="ArgumentNullException">堆栈或条件为空时抛出</exception>
    public static int Count<T>(this Stack<T> stack, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(predicate);

        // 同 Contains：stack.Count(predicate) 会解析回本方法，无限递归
        return Enumerable.Count(stack, predicate);
    }

    /// <summary>
    /// 对堆栈中的每个元素执行指定操作（从顶部到底部）
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="action">要执行的操作</param>
    /// <exception cref="ArgumentNullException">堆栈或操作为空时抛出</exception>
    public static void ForEach<T>(this Stack<T> stack, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(action);

        foreach (var item in stack)
        {
            action(item);
        }
    }

    /// <summary>
    /// 对堆栈中的每个元素执行指定操作（带索引，从顶部到底部）
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="action">要执行的操作，参数为元素和索引</param>
    /// <exception cref="ArgumentNullException">堆栈或操作为空时抛出</exception>
    public static void ForEach<T>(this Stack<T> stack, Action<T, int> action)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(action);

        var index = 0;
        foreach (var item in stack)
        {
            action(item, index++);
        }
    }

    /// <summary>
    /// 创建一个新堆栈，包含满足条件的元素
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">原堆栈</param>
    /// <param name="predicate">筛选条件</param>
    /// <returns>包含满足条件元素的新堆栈</returns>
    /// <exception cref="ArgumentNullException">堆栈或条件为空时抛出</exception>
    public static Stack<T> Where<T>(this Stack<T> stack, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(predicate);

        // 同 Contains：stack.Where(predicate) 会解析回本方法，无限递归
        var filteredItems = Enumerable.Where(stack, predicate).ToArray();

        // filteredItems 是"栈顶到栈底"顺序，按底→顶回压才能让新栈的枚举顺序与源栈一致
        var result = new Stack<T>(filteredItems.Length);
        for (var i = filteredItems.Length - 1; i >= 0; i--)
        {
            result.Push(filteredItems[i]);
        }
        return result;
    }

    /// <summary>
    /// 创建一个新堆栈，包含转换后的元素
    /// </summary>
    /// <typeparam name="TSource">原堆栈元素类型</typeparam>
    /// <typeparam name="TResult">目标堆栈元素类型</typeparam>
    /// <param name="stack">原堆栈</param>
    /// <param name="selector">转换函数</param>
    /// <returns>包含转换后元素的新堆栈</returns>
    /// <exception cref="ArgumentNullException">堆栈或转换函数为空时抛出</exception>
    public static Stack<TResult> Select<TSource, TResult>(this Stack<TSource> stack, Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(selector);

        // 同 Contains：stack.Select(selector) 会解析回本方法，无限递归
        var transformedItems = Enumerable.Select(stack, selector).ToArray();

        // 与 Where 同理：按底→顶回压，保持与源栈一致的枚举顺序
        var result = new Stack<TResult>(transformedItems.Length);
        for (var i = transformedItems.Length - 1; i >= 0; i--)
        {
            result.Push(transformedItems[i]);
        }
        return result;
    }

    /// <summary>
    /// 反转堆栈中的元素顺序
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <exception cref="ArgumentNullException">堆栈为空时抛出</exception>
    public static void Reverse<T>(this Stack<T> stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        if (stack.Count <= 1)
        {
            return;
        }

        var items = new T[stack.Count];
        var index = 0;

        while (stack.Count > 0)
        {
            items[index++] = stack.Pop();
        }

        foreach (var item in items)
        {
            stack.Push(item);
        }
    }

    /// <summary>
    /// 获取堆栈的深度副本（递归反转以保持原始顺序）
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">原堆栈</param>
    /// <returns>深度副本的新堆栈</returns>
    /// <exception cref="ArgumentNullException">堆栈为空时抛出</exception>
    public static Stack<T> DeepClone<T>(this Stack<T> stack)
    {
        ArgumentNullException.ThrowIfNull(stack);

        var tempStack = new Stack<T>();
        var result = new Stack<T>();

        // 第一次反转到临时堆栈
        foreach (var item in stack)
        {
            tempStack.Push(item);
        }

        // 第二次反转到结果堆栈，恢复原始顺序
        while (tempStack.Count > 0)
        {
            result.Push(tempStack.Pop());
        }

        return result;
    }

    /// <summary>
    /// 限制堆栈的最大长度，超出时移除底部元素
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="maxSize">最大长度</param>
    /// <exception cref="ArgumentNullException">堆栈为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">最大长度小于0时抛出</exception>
    public static void LimitSize<T>(this Stack<T> stack, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(stack);

        if (maxSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "最大长度不能小于0");
        }

        if (stack.Count <= maxSize)
        {
            return;
        }

        // 将堆栈内容转移到数组，保留最新的maxSize个元素
        var items = stack.ToArray();
        stack.Clear();

        // 重新入栈最新的maxSize个元素
        for (var i = Math.Min(maxSize - 1, items.Length - 1); i >= 0; i--)
        {
            stack.Push(items[i]);
        }
    }

    /// <summary>
    /// 安全地入栈元素，如果堆栈已满则移除底部元素
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="item">要入栈的元素</param>
    /// <param name="maxSize">堆栈最大长度</param>
    /// <returns>被移除的元素（如果有）</returns>
    /// <exception cref="ArgumentNullException">堆栈为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">最大长度小于1时抛出</exception>
    public static T? PushWithLimit<T>(this Stack<T> stack, T item, int maxSize)
    {
        ArgumentNullException.ThrowIfNull(stack);

        if (maxSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), "最大长度不能小于1");
        }

        T? removedItem = default;

        if (stack.Count >= maxSize)
        {
            // 将所有元素转移到数组
            var items = stack.ToArray();
            stack.Clear();

            // 保存被移除的底部元素
            if (items.Length > 0)
            {
                removedItem = items[items.Length - 1];
            }

            // 重新入栈，跳过底部元素
            for (var i = Math.Min(maxSize - 2, items.Length - 2); i >= 0; i--)
            {
                stack.Push(items[i]);
            }
        }

        stack.Push(item);
        return removedItem;
    }

    /// <summary>
    /// 检查是否所有元素都满足条件
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>是否所有元素都满足条件</returns>
    /// <exception cref="ArgumentNullException">堆栈或条件为空时抛出</exception>
    public static bool All<T>(this Stack<T> stack, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(predicate);

        // 同 Contains：stack.All(predicate) 会解析回本方法，无限递归
        return Enumerable.All(stack, predicate);
    }

    /// <summary>
    /// 检查是否至少有一个元素满足条件
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="stack">堆栈实例</param>
    /// <param name="predicate">匹配条件</param>
    /// <returns>是否至少有一个元素满足条件</returns>
    /// <exception cref="ArgumentNullException">堆栈或条件为空时抛出</exception>
    public static bool Any<T>(this Stack<T> stack, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(predicate);

        // 同 Contains：stack.Any(predicate) 就是本方法自身，直接自递归
        return Enumerable.Any(stack, predicate);
    }

    /// <summary>
    /// 合并两个堆栈（第二个堆栈的元素将位于顶部）
    /// </summary>
    /// <typeparam name="T">堆栈元素类型</typeparam>
    /// <param name="first">第一个堆栈</param>
    /// <param name="second">第二个堆栈</param>
    /// <returns>合并后的新堆栈</returns>
    /// <exception cref="ArgumentNullException">任一堆栈为空时抛出</exception>
    public static Stack<T> Concat<T>(this Stack<T> first, Stack<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var result = new Stack<T>();

        // 先添加第一个堆栈的元素（从底部到顶部）
        var firstItems = first.ToArray();
        for (var i = firstItems.Length - 1; i >= 0; i--)
        {
            result.Push(firstItems[i]);
        }

        // 再添加第二个堆栈的元素（从底部到顶部）
        var secondItems = second.ToArray();
        for (var i = secondItems.Length - 1; i >= 0; i--)
        {
            result.Push(secondItems[i]);
        }

        return result;
    }
}
