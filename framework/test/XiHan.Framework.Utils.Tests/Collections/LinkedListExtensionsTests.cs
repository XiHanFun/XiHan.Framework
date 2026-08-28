// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 链表扩展方法测试
/// </summary>
/// <remarks>
/// 本类不覆盖 Count/Any/All 三个谓词重载，原因见交付报告的疑似缺陷段落：
/// 它们在同命名空间内会解析回自身，属于无限递归，调用即崩进程。
/// </remarks>
public class LinkedListExtensionsTests
{
    /// <summary>
    /// 批量追加到尾部并保持顺序
    /// </summary>
    [Fact]
    public void AddRange_AppendsInOrder()
    {
        var list = new LinkedList<int>();
        list.AddLast(1);

        list.AddRange([2, 3]);

        Assert.Equal([1, 2, 3], list);
    }

    /// <summary>
    /// 链表或元素集合为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void AddRange_WhenArgumentIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LinkedListExtensions.AddRange<int>(null!, [1]));
        Assert.Throws<ArgumentNullException>(() => new LinkedList<int>().AddRange(null!));
    }

    /// <summary>
    /// 批量插入到头部时保持元素集合内部的相对顺序
    /// </summary>
    [Fact]
    public void AddRangeFirst_PrependsPreservingOrder()
    {
        var list = new LinkedList<int>();
        list.AddLast(9);

        list.AddRangeFirst([1, 2]);

        Assert.Equal([1, 2, 9], list);
    }

    /// <summary>
    /// 在指定节点之后批量插入
    /// </summary>
    [Fact]
    public void AddRangeAfter_InsertsAfterGivenNode()
    {
        var list = new LinkedList<int>([1, 4]);
        var node = list.GetNodeAt(0);

        list.AddRangeAfter(node, [2, 3]);

        Assert.Equal([1, 2, 3, 4], list);
    }

    /// <summary>
    /// 在指定节点之前批量插入并保持顺序
    /// </summary>
    [Fact]
    public void AddRangeBefore_InsertsBeforeGivenNodePreservingOrder()
    {
        var list = new LinkedList<int>([1, 4]);
        var node = list.GetNodeAt(1);

        list.AddRangeBefore(node, [2, 3]);

        Assert.Equal([1, 2, 3, 4], list);
    }

    /// <summary>
    /// 查找首个与末个匹配节点
    /// </summary>
    [Fact]
    public void FindFirstAndFindLast_ReturnMatchingNodes()
    {
        var list = new LinkedList<int>([1, 2, 3, 2]);

        var first = list.FindFirst(x => x == 2);
        var last = list.FindLast(x => x == 2);

        Assert.NotNull(first);
        Assert.NotNull(last);
        Assert.Same(list.First!.Next, first);
        Assert.Same(list.Last, last);
    }

    /// <summary>
    /// 没有匹配项时返回 null
    /// </summary>
    [Fact]
    public void FindFirstAndFindLast_WhenNoMatch_ReturnNull()
    {
        var list = new LinkedList<int>([1]);

        Assert.Null(list.FindFirst(x => x == 99));
        Assert.Null(list.FindLast(x => x == 99));
    }

    /// <summary>
    /// 查找全部匹配节点
    /// </summary>
    [Fact]
    public void FindAll_ReturnsEveryMatchingNode()
    {
        var list = new LinkedList<int>([1, 2, 3, 4]);

        var matched = list.FindAll(x => x % 2 == 0).ToList();

        Assert.Equal(2, matched.Count);
        Assert.Equal([2, 4], matched.Select(node => node.Value));
    }

    /// <summary>
    /// 移除全部匹配项并返回移除数量
    /// </summary>
    [Fact]
    public void RemoveAll_RemovesMatchesAndReturnsCount()
    {
        var list = new LinkedList<int>([1, 2, 3, 4]);

        var removed = list.RemoveAll(x => x % 2 == 0);

        Assert.Equal(2, removed);
        Assert.Equal([1, 3], list);
    }

    /// <summary>
    /// 无匹配项时返回 0 且链表不变
    /// </summary>
    [Fact]
    public void RemoveAll_WhenNoMatch_ReturnsZero()
    {
        var list = new LinkedList<int>([1, 3]);

        Assert.Equal(0, list.RemoveAll(x => x > 100));
        Assert.Equal([1, 3], list);
    }

    /// <summary>
    /// 反转链表元素顺序
    /// </summary>
    [Fact]
    public void Reverse_ReversesElementOrder()
    {
        var list = new LinkedList<int>([1, 2, 3]);

        list.Reverse();

        Assert.Equal([3, 2, 1], list);
    }

    /// <summary>
    /// 空链表与单元素链表反转后保持不变
    /// </summary>
    [Fact]
    public void Reverse_WhenAtMostOneElement_ChangesNothing()
    {
        var empty = new LinkedList<int>();
        var single = new LinkedList<int>([7]);

        empty.Reverse();
        single.Reverse();

        Assert.Empty(empty);
        Assert.Equal([7], single);
    }

    /// <summary>
    /// 前半段与后半段索引都能取到正确节点
    /// </summary>
    [Fact]
    public void GetNodeAt_ReturnsNodeForBothHalves()
    {
        var list = new LinkedList<int>([10, 20, 30, 40]);

        Assert.Equal(10, list.GetNodeAt(0).Value);
        Assert.Equal(20, list.GetNodeAt(1).Value);
        Assert.Equal(30, list.GetNodeAt(2).Value);
        Assert.Equal(40, list.GetNodeAt(3).Value);
    }

    /// <summary>
    /// 索引越界时抛下标越界异常
    /// </summary>
    [Fact]
    public void GetNodeAt_WhenIndexOutOfRange_Throws()
    {
        var list = new LinkedList<int>([1]);

        Assert.Throws<ArgumentOutOfRangeException>(() => list.GetNodeAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.GetNodeAt(1));
    }

    /// <summary>
    /// 安全取节点在越界时返回假而不抛异常
    /// </summary>
    [Fact]
    public void TryGetNodeAt_ReturnsFalseWhenOutOfRange()
    {
        var list = new LinkedList<int>([1, 2]);

        Assert.True(list.TryGetNodeAt(1, out var node));
        Assert.NotNull(node);
        Assert.Equal(2, node!.Value);

        Assert.False(list.TryGetNodeAt(9, out var missing));
        Assert.Null(missing);
    }

    /// <summary>
    /// 空与非空判断互为反面
    /// </summary>
    [Fact]
    public void IsEmptyAndIsNotEmpty_AreComplementary()
    {
        var empty = new LinkedList<int>();
        var filled = new LinkedList<int>([1]);

        Assert.True(empty.IsEmpty());
        Assert.False(empty.IsNotEmpty());
        Assert.False(filled.IsEmpty());
        Assert.True(filled.IsNotEmpty());
    }

    /// <summary>
    /// 转数组保持链表顺序
    /// </summary>
    [Fact]
    public void ToArrayPreserveOrder_KeepsOrder()
    {
        var list = new LinkedList<string>(["a", "b"]);

        Assert.Equal(new[] { "a", "b" }, list.ToArrayPreserveOrder());
    }

    /// <summary>
    /// 复制得到独立的新链表
    /// </summary>
    [Fact]
    public void Clone_ReturnsIndependentCopy()
    {
        var list = new LinkedList<int>([1, 2]);

        var clone = list.Clone();
        clone.AddLast(3);

        Assert.NotSame(list, clone);
        Assert.Equal([1, 2], list);
        Assert.Equal([1, 2, 3], clone);
    }

    /// <summary>
    /// 遍历每个元素，带索引重载给出递增下标
    /// </summary>
    [Fact]
    public void ForEach_VisitsEveryElementWithOptionalIndex()
    {
        var list = new LinkedList<string>(["a", "b"]);
        List<string> visited = [];
        List<int> indexes = [];

        list.ForEach(value => visited.Add(value));
        list.ForEach((value, index) =>
        {
            visited.Add(value);
            indexes.Add(index);
        });

        Assert.Equal(new[] { "a", "b", "a", "b" }, visited);
        Assert.Equal(new[] { 0, 1 }, indexes);
    }

    /// <summary>
    /// 按节点遍历，允许在回调里删除当前节点
    /// </summary>
    [Fact]
    public void ForEachNode_AllowsRemovingCurrentNode()
    {
        var list = new LinkedList<int>([1, 2, 3]);

        list.ForEachNode(node =>
        {
            if (node.Value == 2)
            {
                list.Remove(node);
            }
        });

        Assert.Equal([1, 3], list);
    }

    /// <summary>
    /// 筛选得到新链表，原链表不变
    /// </summary>
    [Fact]
    public void Where_ReturnsNewLinkedListWithoutTouchingSource()
    {
        var list = new LinkedList<int>([1, 2, 3, 4]);

        var filtered = list.Where(x => x % 2 == 0);

        Assert.Equal([2, 4], filtered);
        Assert.Equal([1, 2, 3, 4], list);
    }

    /// <summary>
    /// 投影得到新链表
    /// </summary>
    [Fact]
    public void Select_ProjectsIntoNewLinkedList()
    {
        var list = new LinkedList<int>([1, 2]);

        var projected = list.Select(x => x * 10);

        Assert.Equal([10, 20], projected);
    }

    /// <summary>
    /// 合并两个链表得到新链表，原链表都不变
    /// </summary>
    [Fact]
    public void Concat_MergesIntoNewLinkedList()
    {
        var first = new LinkedList<int>([1, 2]);
        var second = new LinkedList<int>([3]);

        var merged = first.Concat(second);

        Assert.Equal([1, 2, 3], merged);
        Assert.Equal([1, 2], first);
        Assert.Equal([3], second);
    }

    /// <summary>
    /// 限长时从头部丢弃多余元素
    /// </summary>
    [Fact]
    public void LimitSize_DropsFromFront()
    {
        var list = new LinkedList<int>([1, 2, 3, 4, 5]);

        list.LimitSize(3);

        Assert.Equal([3, 4, 5], list);
    }

    /// <summary>
    /// 限长值不小于当前长度时不做改动
    /// </summary>
    [Fact]
    public void LimitSize_WhenAlreadyWithinLimit_ChangesNothing()
    {
        var list = new LinkedList<int>([1, 2]);

        list.LimitSize(5);

        Assert.Equal([1, 2], list);
    }

    /// <summary>
    /// 限长值为负时抛下标越界异常
    /// </summary>
    [Fact]
    public void LimitSize_WhenNegative_Throws()
    {
        var list = new LinkedList<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => list.LimitSize(-1));
    }

    /// <summary>
    /// 未满时追加不淘汰，已满时淘汰头部并返回被淘汰元素
    /// </summary>
    [Fact]
    public void AddLastWithLimit_EvictsHeadWhenFull()
    {
        var list = new LinkedList<string>(["a", "b"]);

        var noEviction = list.AddLastWithLimit("c", 3);
        var evicted = list.AddLastWithLimit("d", 3);

        Assert.Null(noEviction);
        Assert.Equal("a", evicted);
        Assert.Equal(["b", "c", "d"], list);
    }

    /// <summary>
    /// 限长值小于 1 时抛下标越界异常
    /// </summary>
    [Fact]
    public void AddLastWithLimit_WhenMaxSizeLessThanOne_Throws()
    {
        var list = new LinkedList<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => list.AddLastWithLimit(1, 0));
    }
}
