// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 列表扩展方法测试
/// </summary>
/// <remarks>
/// 多处刻意把变量声明为 <see cref="IList{T}"/> 而不是 <see cref="List{T}"/>：
/// List 自带同名实例方法（InsertRange/FindIndex），会盖住这里要测的扩展方法。
/// </remarks>
public class ListExtensionsTests
{
    /// <summary>
    /// 按索引批量插入并保持原有顺序
    /// </summary>
    [Fact]
    public void InsertRange_InsertsItemsInOrderAtIndex()
    {
        IList<string> source = new List<string> { "a", "d" };

        source.InsertRange(1, new[] { "b", "c" });

        Assert.Equal(new[] { "a", "b", "c", "d" }, source);
    }

    /// <summary>
    /// 插入空序列时列表不变
    /// </summary>
    [Fact]
    public void InsertRange_WithEmptyItems_ChangesNothing()
    {
        IList<string> source = new List<string> { "a" };

        source.InsertRange(0, Array.Empty<string>());

        Assert.Equal(new[] { "a" }, source);
    }

    /// <summary>
    /// 命中返回下标，未命中返回 -1
    /// </summary>
    [Fact]
    public void FindIndex_ReturnsMatchedIndexOrMinusOne()
    {
        IList<int> source = new List<int> { 10, 20, 30 };

        Assert.Equal(1, source.FindIndex(x => x == 20));
        Assert.Equal(-1, source.FindIndex(x => x == 99));
    }

    /// <summary>
    /// 空列表查找返回 -1
    /// </summary>
    [Fact]
    public void FindIndex_WhenEmpty_ReturnsMinusOne()
    {
        IList<int> source = new List<int>();

        Assert.Equal(-1, source.FindIndex(_ => true));
    }

    /// <summary>
    /// 首部与尾部追加
    /// </summary>
    [Fact]
    public void AddFirstAndAddLast_AppendToBothEnds()
    {
        IList<string> source = new List<string> { "m" };

        source.AddFirst("f");
        source.AddLast("l");

        Assert.Equal(new[] { "f", "m", "l" }, source);
    }

    /// <summary>
    /// 在指定项之后插入
    /// </summary>
    [Fact]
    public void InsertAfter_WithExistingItem_InsertsRightAfterIt()
    {
        IList<string> source = new List<string> { "a", "c" };

        source.InsertAfter("a", "b");

        Assert.Equal(new[] { "a", "b", "c" }, source);
    }

    /// <summary>
    /// 参照项不存在时退化为首部插入
    /// </summary>
    [Fact]
    public void InsertAfter_WhenExistingItemMissing_AddsToFront()
    {
        IList<string> source = new List<string> { "a" };

        source.InsertAfter("missing", "x");

        Assert.Equal(new[] { "x", "a" }, source);
    }

    /// <summary>
    /// 按选择器在匹配项之后插入
    /// </summary>
    [Fact]
    public void InsertAfter_WithSelector_InsertsRightAfterMatch()
    {
        IList<string> source = new List<string> { "a", "c" };

        source.InsertAfter(x => x == "a", "b");

        Assert.Equal(new[] { "a", "b", "c" }, source);
    }

    /// <summary>
    /// 选择器未命中时退化为首部插入
    /// </summary>
    [Fact]
    public void InsertAfter_WithSelector_WhenNoMatch_AddsToFront()
    {
        IList<string> source = new List<string> { "a" };

        source.InsertAfter(x => x == "zzz", "x");

        Assert.Equal(new[] { "x", "a" }, source);
    }

    /// <summary>
    /// 在指定项之前插入
    /// </summary>
    [Fact]
    public void InsertBefore_WithExistingItem_InsertsRightBeforeIt()
    {
        IList<string> source = new List<string> { "a", "c" };

        source.InsertBefore("c", "b");

        Assert.Equal(new[] { "a", "b", "c" }, source);
    }

    /// <summary>
    /// 参照项不存在时退化为尾部追加
    /// </summary>
    [Fact]
    public void InsertBefore_WhenExistingItemMissing_AddsToEnd()
    {
        IList<string> source = new List<string> { "a" };

        source.InsertBefore("missing", "x");

        Assert.Equal(new[] { "a", "x" }, source);
    }

    /// <summary>
    /// 按选择器在匹配项之前插入，未命中时尾部追加
    /// </summary>
    [Fact]
    public void InsertBefore_WithSelector_InsertsBeforeMatchOrAppends()
    {
        IList<string> matched = new List<string> { "a", "c" };
        IList<string> unmatched = new List<string> { "a" };

        matched.InsertBefore(x => x == "c", "b");
        unmatched.InsertBefore(x => x == "zzz", "x");

        Assert.Equal(new[] { "a", "b", "c" }, matched);
        Assert.Equal(new[] { "a", "x" }, unmatched);
    }

    /// <summary>
    /// 替换所有满足条件的项
    /// </summary>
    [Fact]
    public void ReplaceWhile_ReplacesEveryMatch()
    {
        IList<int> source = new List<int> { 1, 2, 3, 2 };

        source.ReplaceWhile(x => x == 2, 9);

        Assert.Equal(new[] { 1, 9, 3, 9 }, source);
    }

    /// <summary>
    /// 用工厂替换所有满足条件的项，工厂能拿到原值
    /// </summary>
    [Fact]
    public void ReplaceWhile_WithFactory_UsesOriginalValue()
    {
        IList<int> source = new List<int> { 1, 2, 3 };

        source.ReplaceWhile(x => x % 2 == 1, x => x * 10);

        Assert.Equal(new[] { 10, 2, 30 }, source);
    }

    /// <summary>
    /// 只替换第一个满足条件的项
    /// </summary>
    [Fact]
    public void ReplaceOne_WithSelector_ReplacesOnlyFirstMatch()
    {
        IList<int> source = new List<int> { 2, 2, 3 };

        source.ReplaceOne(x => x == 2, 9);

        Assert.Equal(new[] { 9, 2, 3 }, source);
    }

    /// <summary>
    /// 工厂重载同样只替换第一个匹配
    /// </summary>
    [Fact]
    public void ReplaceOne_WithFactory_ReplacesOnlyFirstMatch()
    {
        IList<int> source = new List<int> { 2, 2 };

        source.ReplaceOne(x => x == 2, x => x + 100);

        Assert.Equal(new[] { 102, 2 }, source);
    }

    /// <summary>
    /// 按值替换第一个相等项
    /// </summary>
    [Fact]
    public void ReplaceOne_WithValue_ReplacesFirstEqualItem()
    {
        IList<string> source = new List<string> { "a", "b", "a" };

        source.ReplaceOne("a", "z");

        Assert.Equal(new[] { "z", "b", "a" }, source);
    }

    /// <summary>
    /// 无匹配项时列表保持不变
    /// </summary>
    [Fact]
    public void ReplaceOne_WhenNoMatch_ChangesNothing()
    {
        IList<int> source = new List<int> { 1, 2 };

        source.ReplaceOne(x => x == 99, 0);

        Assert.Equal(new[] { 1, 2 }, source);
    }

    /// <summary>
    /// 把匹配项移动到目标下标
    /// </summary>
    [Fact]
    public void MoveItem_MovesMatchedItemToTargetIndex()
    {
        var source = new List<string> { "a", "b", "c" };

        source.MoveItem(x => x == "c", 0);

        Assert.Equal(new[] { "c", "a", "b" }, source);
    }

    /// <summary>
    /// 目标下标与当前下标相同时不做任何改动
    /// </summary>
    [Fact]
    public void MoveItem_WhenAlreadyAtTarget_ChangesNothing()
    {
        var source = new List<string> { "a", "b" };

        source.MoveItem(x => x == "a", 0);

        Assert.Equal(new[] { "a", "b" }, source);
    }

    /// <summary>
    /// 目标下标越界时抛下标越界异常
    /// </summary>
    [Fact]
    public void MoveItem_WhenTargetIndexOutOfRange_Throws()
    {
        var source = new List<string> { "a", "b" };

        Assert.Throws<IndexOutOfRangeException>(() => source.MoveItem(x => x == "a", 2));
        Assert.Throws<IndexOutOfRangeException>(() => source.MoveItem(x => x == "a", -1));
    }

    /// <summary>
    /// 命中时返回既有项且不追加
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenMatchExists_ReturnsExistingWithoutAdding()
    {
        IList<string> source = new List<string> { "abc" };
        var factoryCalls = 0;

        var value = source.GetOrAdd(x => x.StartsWith('a'), () =>
        {
            factoryCalls++;
            return "new";
        });

        Assert.Equal("abc", value);
        Assert.Equal(0, factoryCalls);
        Assert.Single(source);
    }

    /// <summary>
    /// 未命中时用工厂创建并追加
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenNoMatch_CreatesAndAppends()
    {
        IList<string> source = new List<string> { "abc" };

        var value = source.GetOrAdd(x => x.StartsWith('z'), () => "zzz");

        Assert.Equal("zzz", value);
        Assert.Equal(new[] { "abc", "zzz" }, source);
    }

    /// <summary>
    /// 列表为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetOrAdd_WhenSourceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ListExtensions.GetOrAdd<string>(null!, _ => true, () => "x"));
    }

    /// <summary>
    /// 随机取值一定落在原列表内
    /// </summary>
    [Fact]
    public void GetRandom_ReturnsElementFromList()
    {
        IList<int> source = new List<int> { 1, 2, 3 };

        for (var i = 0; i < 20; i++)
        {
            Assert.Contains(source.GetRandom(), source);
        }
    }

    /// <summary>
    /// 空列表取随机值抛参数异常
    /// </summary>
    [Fact]
    public void GetRandom_WhenEmpty_Throws()
    {
        IList<int> source = new List<int>();

        Assert.Throws<ArgumentException>(() => source.GetRandom());
    }

    /// <summary>
    /// 列表为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetRandom_WhenSourceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ListExtensions.GetRandom<int>(null!));
    }

    /// <summary>
    /// 非空列表尝试取随机值返回真
    /// </summary>
    [Fact]
    public void TryGetRandom_WhenNotEmpty_ReturnsTrueWithElement()
    {
        IList<string> source = new List<string> { "only" };

        var success = source.TryGetRandom(out var result);

        Assert.True(success);
        Assert.Equal("only", result);
    }

    /// <summary>
    /// 空列表尝试取随机值返回假与默认值
    /// </summary>
    [Fact]
    public void TryGetRandom_WhenEmpty_ReturnsFalseWithDefault()
    {
        IList<string> source = new List<string>();

        var success = source.TryGetRandom(out var result);

        Assert.False(success);
        Assert.Null(result);
    }
}
