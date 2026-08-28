// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 队列扩展方法测试
/// </summary>
/// <remarks>
/// 本类不覆盖 Count/Where/Select 三个重载，原因见交付报告的疑似缺陷段落：
/// 它们在同命名空间内会解析回自身，属于无限递归，调用即崩进程。
/// </remarks>
public class QueueExtensionsTests
{
    /// <summary>
    /// 批量入队保持先进先出顺序
    /// </summary>
    [Fact]
    public void EnqueueRange_KeepsFifoOrder()
    {
        var queue = new Queue<int>();

        queue.EnqueueRange([1, 2, 3]);

        Assert.Equal(new[] { 1, 2, 3 }, queue.ToArray());
    }

    /// <summary>
    /// 队列或元素集合为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void EnqueueRange_WhenArgumentIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QueueExtensions.EnqueueRange<int>(null!, [1]));
        Assert.Throws<ArgumentNullException>(() => new Queue<int>().EnqueueRange(null!));
    }

    /// <summary>
    /// 批量出队按顺序取出并从队列中移除
    /// </summary>
    [Fact]
    public void DequeueRange_TakesItemsFromFront()
    {
        var queue = new Queue<int>([1, 2, 3]);

        var taken = queue.DequeueRange(2);

        Assert.Equal([1, 2], taken);
        Assert.Equal(new[] { 3 }, queue.ToArray());
    }

    /// <summary>
    /// 出队 0 个元素时队列不变
    /// </summary>
    [Fact]
    public void DequeueRange_WithZeroCount_ChangesNothing()
    {
        var queue = new Queue<int>([1]);

        var taken = queue.DequeueRange(0);

        Assert.Empty(taken);
        Assert.Equal(new[] { 1 }, queue.ToArray());
    }

    /// <summary>
    /// 数量非法时抛下标越界异常
    /// </summary>
    [Fact]
    public void DequeueRange_WhenCountInvalid_Throws()
    {
        var queue = new Queue<int>([1]);

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.DequeueRange(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.DequeueRange(2));
    }

    /// <summary>
    /// 尝试批量出队在数量合法时成功
    /// </summary>
    [Fact]
    public void TryDequeueRange_WhenCountValid_ReturnsTrue()
    {
        var queue = new Queue<string>(["a", "b", "c"]);

        var success = queue.TryDequeueRange(2, out var items);

        Assert.True(success);
        Assert.Equal(["a", "b"], items);
        Assert.Equal(new[] { "c" }, queue.ToArray());
    }

    /// <summary>
    /// 数量非法时返回假、给出空集合且队列保持不变
    /// </summary>
    [Fact]
    public void TryDequeueRange_WhenCountInvalid_ReturnsFalseAndKeepsQueue()
    {
        var queue = new Queue<string>(["a"]);

        var tooMany = queue.TryDequeueRange(5, out var items);
        var negative = queue.TryDequeueRange(-1, out var negativeItems);

        Assert.False(tooMany);
        Assert.Empty(items);
        Assert.False(negative);
        Assert.Empty(negativeItems);
        Assert.Equal(new[] { "a" }, queue.ToArray());
    }

    /// <summary>
    /// 清空队列并返回全部元素
    /// </summary>
    [Fact]
    public void DrainToList_EmptiesQueueAndReturnsAllItems()
    {
        var queue = new Queue<int>([1, 2]);

        var drained = queue.DrainToList();

        Assert.Equal([1, 2], drained);
        Assert.Empty(queue);
    }

    /// <summary>
    /// 空队列清空后返回空集合
    /// </summary>
    [Fact]
    public void DrainToList_WhenEmpty_ReturnsEmpty()
    {
        Assert.Empty(new Queue<int>().DrainToList());
    }

    /// <summary>
    /// 安全查看队首在非空时返回真
    /// </summary>
    [Fact]
    public void TryPeek_WhenNotEmpty_ReturnsHeadWithoutRemoving()
    {
        var queue = new Queue<string>(["a", "b"]);

        var success = QueueExtensions.TryPeek(queue, out var item);

        Assert.True(success);
        Assert.Equal("a", item);
        Assert.Equal(2, queue.Count);
    }

    /// <summary>
    /// 空队列查看队首返回假与默认值
    /// </summary>
    [Fact]
    public void TryPeek_WhenEmpty_ReturnsFalse()
    {
        var queue = new Queue<string>();

        var success = QueueExtensions.TryPeek(queue, out var item);

        Assert.False(success);
        Assert.Null(item);
    }

    /// <summary>
    /// 空与非空判断互为反面
    /// </summary>
    [Fact]
    public void IsEmptyAndIsNotEmpty_AreComplementary()
    {
        var empty = new Queue<int>();
        var filled = new Queue<int>([1]);

        Assert.True(empty.IsEmpty());
        Assert.False(empty.IsNotEmpty());
        Assert.False(filled.IsEmpty());
        Assert.True(filled.IsNotEmpty());
    }

    /// <summary>
    /// 转数组保持先进先出顺序
    /// </summary>
    [Fact]
    public void ToArrayPreserveOrder_KeepsFifoOrder()
    {
        var queue = new Queue<int>([1, 2, 3]);

        Assert.Equal(new[] { 1, 2, 3 }, queue.ToArrayPreserveOrder());
    }

    /// <summary>
    /// 复制得到顺序一致且互不影响的新队列
    /// </summary>
    [Fact]
    public void Clone_ReturnsIndependentCopyWithSameOrder()
    {
        var queue = new Queue<int>([1, 2]);

        var clone = queue.Clone();
        clone.Enqueue(3);

        Assert.NotSame(queue, clone);
        Assert.Equal(new[] { 1, 2 }, queue.ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, clone.ToArray());
    }

    /// <summary>
    /// 按谓词判断是否存在匹配元素
    /// </summary>
    [Fact]
    public void Contains_WithPredicate_DetectsMatchingElement()
    {
        var queue = new Queue<int>([1, 2, 3]);

        Assert.True(queue.Contains(x => x == 2));
        Assert.False(queue.Contains(x => x > 100));
    }

    /// <summary>
    /// 谓词为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void Contains_WhenPredicateIsNull_Throws()
    {
        var queue = new Queue<int>();

        Assert.Throws<ArgumentNullException>(() => queue.Contains((Func<int, bool>)null!));
    }

    /// <summary>
    /// 遍历按先进先出顺序，带索引重载给出递增下标
    /// </summary>
    [Fact]
    public void ForEach_VisitsInFifoOrder()
    {
        var queue = new Queue<string>(["a", "b"]);
        List<string> visited = [];
        List<int> indexes = [];

        queue.ForEach(value => visited.Add(value));
        queue.ForEach((value, index) =>
        {
            visited.Add(value);
            indexes.Add(index);
        });

        Assert.Equal(new[] { "a", "b", "a", "b" }, visited);
        Assert.Equal(new[] { 0, 1 }, indexes);
    }

    /// <summary>
    /// 限长时从队首丢弃多余元素
    /// </summary>
    [Fact]
    public void LimitSize_DropsOldestItems()
    {
        var queue = new Queue<int>([1, 2, 3, 4]);

        queue.LimitSize(2);

        Assert.Equal(new[] { 3, 4 }, queue.ToArray());
    }

    /// <summary>
    /// 限长值不小于当前长度时不做改动
    /// </summary>
    [Fact]
    public void LimitSize_WhenAlreadyWithinLimit_ChangesNothing()
    {
        var queue = new Queue<int>([1, 2]);

        queue.LimitSize(5);

        Assert.Equal(new[] { 1, 2 }, queue.ToArray());
    }

    /// <summary>
    /// 限长值为负时抛下标越界异常
    /// </summary>
    [Fact]
    public void LimitSize_WhenNegative_Throws()
    {
        var queue = new Queue<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.LimitSize(-1));
    }

    /// <summary>
    /// 未满时入队不淘汰，已满时淘汰队首并返回被淘汰元素
    /// </summary>
    [Fact]
    public void EnqueueWithLimit_EvictsOldestWhenFull()
    {
        var queue = new Queue<string>(["a", "b"]);

        var noEviction = queue.EnqueueWithLimit("c", 3);
        var evicted = queue.EnqueueWithLimit("d", 3);

        Assert.Null(noEviction);
        Assert.Equal("a", evicted);
        Assert.Equal(new[] { "b", "c", "d" }, queue.ToArray());
    }

    /// <summary>
    /// 限长值小于 1 时抛下标越界异常
    /// </summary>
    [Fact]
    public void EnqueueWithLimit_WhenMaxSizeLessThanOne_Throws()
    {
        var queue = new Queue<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.EnqueueWithLimit(1, 0));
    }
}
