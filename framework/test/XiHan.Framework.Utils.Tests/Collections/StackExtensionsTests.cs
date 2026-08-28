// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 堆栈扩展方法测试
/// </summary>
/// <remarks>
/// 本类不覆盖 Contains/Count/Where/Select/All/Any 六个谓词重载，原因见交付报告的疑似缺陷段落：
/// 它们在同命名空间内会解析回自身，属于无限递归，调用即崩进程。
/// 断言里统一用"栈顶在前"的序列表达堆栈内容，这与 Stack&lt;T&gt; 自身的枚举顺序一致。
/// </remarks>
public class StackExtensionsTests
{
    /// <summary>
    /// 批量入栈按集合顺序逐个压入，最后一个位于栈顶
    /// </summary>
    [Fact]
    public void PushRange_PushesInSequenceOrder()
    {
        var stack = new Stack<int>();

        stack.PushRange([1, 2, 3]);

        Assert.Equal(new[] { 3, 2, 1 }, stack.ToArray());
    }

    /// <summary>
    /// 堆栈或元素集合为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void PushRange_WhenArgumentIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => StackExtensions.PushRange<int>(null!, [1]));
        Assert.Throws<ArgumentNullException>(() => new Stack<int>().PushRange(null!));
    }

    /// <summary>
    /// 反序批量入栈后弹出顺序与原集合一致
    /// </summary>
    [Fact]
    public void PushRangeReversed_MakesPopOrderMatchSourceOrder()
    {
        var stack = new Stack<int>();

        stack.PushRangeReversed([1, 2, 3]);

        Assert.Equal(new[] { 1, 2, 3 }, stack.ToArray());
    }

    /// <summary>
    /// 批量出栈从栈顶开始取
    /// </summary>
    [Fact]
    public void PopRange_TakesFromTop()
    {
        var stack = new Stack<int>();
        stack.PushRange([1, 2, 3]);

        var popped = stack.PopRange(2);

        Assert.Equal([3, 2], popped);
        Assert.Equal(new[] { 1 }, stack.ToArray());
    }

    /// <summary>
    /// 数量非法时抛下标越界异常
    /// </summary>
    [Fact]
    public void PopRange_WhenCountInvalid_Throws()
    {
        var stack = new Stack<int>();
        stack.Push(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => stack.PopRange(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stack.PopRange(2));
    }

    /// <summary>
    /// 尝试批量出栈在数量合法时成功
    /// </summary>
    [Fact]
    public void TryPopRange_WhenCountValid_ReturnsTrue()
    {
        var stack = new Stack<int>();
        stack.PushRange([1, 2, 3]);

        var success = stack.TryPopRange(2, out var items);

        Assert.True(success);
        Assert.Equal([3, 2], items);
        Assert.Equal(new[] { 1 }, stack.ToArray());
    }

    /// <summary>
    /// 数量非法时返回假、给出空集合且堆栈保持不变
    /// </summary>
    [Fact]
    public void TryPopRange_WhenCountInvalid_ReturnsFalseAndKeepsStack()
    {
        var stack = new Stack<int>();
        stack.Push(1);

        var tooMany = stack.TryPopRange(5, out var items);
        var negative = stack.TryPopRange(-1, out var negativeItems);

        Assert.False(tooMany);
        Assert.Empty(items);
        Assert.False(negative);
        Assert.Empty(negativeItems);
        Assert.Equal(new[] { 1 }, stack.ToArray());
    }

    /// <summary>
    /// 清空堆栈并按栈顶到栈底返回全部元素
    /// </summary>
    [Fact]
    public void DrainToList_EmptiesStackFromTop()
    {
        var stack = new Stack<int>();
        stack.PushRange([1, 2]);

        var drained = stack.DrainToList();

        Assert.Equal([2, 1], drained);
        Assert.Empty(stack);
    }

    /// <summary>
    /// 安全查看栈顶在非空时返回真且不出栈
    /// </summary>
    [Fact]
    public void TryPeek_WhenNotEmpty_ReturnsTopWithoutPopping()
    {
        var stack = new Stack<string>();
        stack.PushRange(["a", "b"]);

        var success = StackExtensions.TryPeek(stack, out var item);

        Assert.True(success);
        Assert.Equal("b", item);
        Assert.Equal(2, stack.Count);
    }

    /// <summary>
    /// 空堆栈查看栈顶返回假与默认值
    /// </summary>
    [Fact]
    public void TryPeek_WhenEmpty_ReturnsFalse()
    {
        var stack = new Stack<string>();

        var success = StackExtensions.TryPeek(stack, out var item);

        Assert.False(success);
        Assert.Null(item);
    }

    /// <summary>
    /// 查看多个顶部元素但不出栈
    /// </summary>
    [Fact]
    public void PeekRange_ReturnsTopItemsWithoutPopping()
    {
        var stack = new Stack<int>();
        stack.PushRange([1, 2, 3]);

        var peeked = stack.PeekRange(2);

        Assert.Equal([3, 2], peeked);
        Assert.Equal(3, stack.Count);
    }

    /// <summary>
    /// 查看 0 个元素返回空集合
    /// </summary>
    [Fact]
    public void PeekRange_WithZeroCount_ReturnsEmpty()
    {
        var stack = new Stack<int>();
        stack.Push(1);

        Assert.Empty(stack.PeekRange(0));
    }

    /// <summary>
    /// 数量非法时抛下标越界异常
    /// </summary>
    [Fact]
    public void PeekRange_WhenCountInvalid_Throws()
    {
        var stack = new Stack<int>();
        stack.Push(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => stack.PeekRange(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stack.PeekRange(2));
    }

    /// <summary>
    /// 空与非空判断互为反面
    /// </summary>
    [Fact]
    public void IsEmptyAndIsNotEmpty_AreComplementary()
    {
        var empty = new Stack<int>();
        var filled = new Stack<int>();
        filled.Push(1);

        Assert.True(empty.IsEmpty());
        Assert.False(empty.IsNotEmpty());
        Assert.False(filled.IsEmpty());
        Assert.True(filled.IsNotEmpty());
    }

    /// <summary>
    /// 转数组保持栈顶在前的顺序
    /// </summary>
    [Fact]
    public void ToArrayPreserveOrder_KeepsTopFirst()
    {
        var stack = new Stack<int>();
        stack.PushRange([1, 2, 3]);

        Assert.Equal(new[] { 3, 2, 1 }, stack.ToArrayPreserveOrder());
    }

    /// <summary>
    /// 深拷贝得到顺序一致且互不影响的新堆栈
    /// </summary>
    [Fact]
    public void DeepClone_ReturnsIndependentCopyWithSameOrder()
    {
        var stack = new Stack<int>();
        stack.PushRange([1, 2, 3]);

        var clone = stack.DeepClone();
        clone.Push(4);

        Assert.NotSame(stack, clone);
        Assert.Equal(new[] { 3, 2, 1 }, stack.ToArray());
        Assert.Equal(new[] { 4, 3, 2, 1 }, clone.ToArray());
    }

    /// <summary>
    /// 遍历从栈顶到栈底，带索引重载给出递增下标
    /// </summary>
    [Fact]
    public void ForEach_VisitsFromTopToBottom()
    {
        var stack = new Stack<string>();
        stack.PushRange(["a", "b"]);
        List<string> visited = [];
        List<int> indexes = [];

        stack.ForEach(value => visited.Add(value));
        stack.ForEach((value, index) =>
        {
            visited.Add(value);
            indexes.Add(index);
        });

        Assert.Equal(new[] { "b", "a", "b", "a" }, visited);
        Assert.Equal(new[] { 0, 1 }, indexes);
    }

    /// <summary>
    /// 反转堆栈使原栈底成为栈顶
    /// </summary>
    [Fact]
    public void Reverse_MakesBottomBecomeTop()
    {
        var stack = new Stack<int>();
        stack.PushRange([1, 2, 3]);

        stack.Reverse();

        Assert.Equal(new[] { 1, 2, 3 }, stack.ToArray());
    }

    /// <summary>
    /// 元素不超过一个时反转不改变内容
    /// </summary>
    [Fact]
    public void Reverse_WhenAtMostOneElement_ChangesNothing()
    {
        var empty = new Stack<int>();
        var single = new Stack<int>();
        single.Push(7);

        empty.Reverse();
        single.Reverse();

        Assert.Empty(empty);
        Assert.Equal(new[] { 7 }, single.ToArray());
    }

    /// <summary>
    /// 限长时丢弃栈底元素并保留最新的若干个
    /// </summary>
    [Fact]
    public void LimitSize_KeepsNewestItems()
    {
        var stack = new Stack<int>();
        stack.PushRange([1, 2, 3, 4]);

        stack.LimitSize(2);

        Assert.Equal(new[] { 4, 3 }, stack.ToArray());
    }

    /// <summary>
    /// 限长值不小于当前长度时不做改动
    /// </summary>
    [Fact]
    public void LimitSize_WhenAlreadyWithinLimit_ChangesNothing()
    {
        var stack = new Stack<int>();
        stack.PushRange([1, 2]);

        stack.LimitSize(5);

        Assert.Equal(new[] { 2, 1 }, stack.ToArray());
    }

    /// <summary>
    /// 限长值为负时抛下标越界异常
    /// </summary>
    [Fact]
    public void LimitSize_WhenNegative_Throws()
    {
        var stack = new Stack<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => stack.LimitSize(-1));
    }

    /// <summary>
    /// 未满时入栈不淘汰，已满时淘汰栈底并返回被淘汰元素
    /// </summary>
    [Fact]
    public void PushWithLimit_EvictsBottomWhenFull()
    {
        var stack = new Stack<string>();
        stack.PushRange(["a", "b"]);

        var noEviction = stack.PushWithLimit("c", 3);
        var evicted = stack.PushWithLimit("d", 3);

        Assert.Null(noEviction);
        Assert.Equal("a", evicted);
        Assert.Equal(new[] { "d", "c", "b" }, stack.ToArray());
    }

    /// <summary>
    /// 限长值小于 1 时抛下标越界异常
    /// </summary>
    [Fact]
    public void PushWithLimit_WhenMaxSizeLessThanOne_Throws()
    {
        var stack = new Stack<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => stack.PushWithLimit(1, 0));
    }

    /// <summary>
    /// 合并两个堆栈时第二个堆栈的元素位于顶部，双方原堆栈都不变
    /// </summary>
    [Fact]
    public void Concat_PutsSecondStackOnTop()
    {
        var first = new Stack<int>();
        first.PushRange([1, 2]);
        var second = new Stack<int>();
        second.PushRange([8, 9]);

        var merged = first.Concat(second);

        Assert.Equal(new[] { 9, 8, 2, 1 }, merged.ToArray());
        Assert.Equal(new[] { 2, 1 }, first.ToArray());
        Assert.Equal(new[] { 9, 8 }, second.ToArray());
    }

    /// <summary>
    /// 任一堆栈为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void Concat_WhenArgumentIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => StackExtensions.Concat<int>(null!, new Stack<int>()));
        Assert.Throws<ArgumentNullException>(() => new Stack<int>().Concat(null!));
    }
}
