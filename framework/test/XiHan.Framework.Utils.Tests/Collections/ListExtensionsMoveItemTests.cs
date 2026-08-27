// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 列表扩展 MoveItem 的异常语义回归测试
/// </summary>
/// <remarks>
/// 原实现两处报错都与真实原因无关：
/// 一是选择器一个都没匹配上时 currentIndex 为 -1，一路走到 source[currentIndex]，
/// 抛出的 ArgumentOutOfRangeException 完全看不出"没找到要移动的项"；
/// 二是空列表时先在 IsInRange(0, -1) 处抛 ArgumentException（最小值大于最大值），
/// 也不是文档承诺的越界异常。
/// </remarks>
public class ListExtensionsMoveItemTests
{
    /// <summary>
    /// 没有任何项匹配选择器时抛出语义明确的异常
    /// </summary>
    [Fact]
    public void MoveItem_WhenNoItemMatches_ThrowsInvalidOperation()
    {
        var source = new List<string> { "a", "b", "c" };

        var ex = Assert.Throws<InvalidOperationException>(() => source.MoveItem(x => x == "z", 0));

        Assert.Contains("未找到", ex.Message);
    }

    /// <summary>
    /// 抛异常后列表内容保持不变
    /// </summary>
    [Fact]
    public void MoveItem_WhenNoItemMatches_LeavesListUnchanged()
    {
        var source = new List<string> { "a", "b", "c" };

        Assert.Throws<InvalidOperationException>(() => source.MoveItem(x => x == "z", 2));

        Assert.Equal(new[] { "a", "b", "c" }, source);
    }

    /// <summary>
    /// 空列表抛越界异常，而不是"最小值大于最大值"的参数异常
    /// </summary>
    [Fact]
    public void MoveItem_WhenListIsEmpty_ThrowsIndexOutOfRange()
    {
        var source = new List<string>();

        Assert.Throws<IndexOutOfRangeException>(() => source.MoveItem(x => x == "a", 0));
    }

    /// <summary>
    /// 正常移动的行为不受影响
    /// </summary>
    [Fact]
    public void MoveItem_WhenMatched_StillMovesToTargetIndex()
    {
        var source = new List<string> { "a", "b", "c" };

        source.MoveItem(x => x == "c", 0);

        Assert.Equal(new[] { "c", "a", "b" }, source);
    }

    /// <summary>
    /// 已经在目标位置时什么都不做
    /// </summary>
    [Fact]
    public void MoveItem_WhenAlreadyAtTarget_ChangesNothing()
    {
        var source = new List<string> { "a", "b", "c" };

        source.MoveItem(x => x == "a", 0);

        Assert.Equal(new[] { "a", "b", "c" }, source);
    }

    /// <summary>
    /// 目标索引越界仍抛越界异常
    /// </summary>
    [Fact]
    public void MoveItem_WhenTargetIndexOutOfRange_ThrowsIndexOutOfRange()
    {
        var source = new List<string> { "a", "b" };

        Assert.Throws<IndexOutOfRangeException>(() => source.MoveItem(x => x == "a", 2));
        Assert.Throws<IndexOutOfRangeException>(() => source.MoveItem(x => x == "a", -1));
    }
}
