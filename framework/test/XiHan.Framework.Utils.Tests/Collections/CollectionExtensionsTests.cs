// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;
// 框架的 CollectionExtensions 与 BCL 的 System.Collections.Generic.CollectionExtensions
// 同名，隐式 using 下静态调用会二义；用别名锚定到被测的那个。
using FrameworkCollectionExtensions = XiHan.Framework.Utils.Collections.CollectionExtensions;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 集合扩展方法测试
/// </summary>
/// <remarks>
/// 覆盖每个公开方法的正常路径、null 入参、空集合与重复项边界。
/// </remarks>
public class CollectionExtensionsTests
{
    /// <summary>
    /// null 集合判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenNull_ReturnsTrue()
    {
        Assert.True(FrameworkCollectionExtensions.IsNullOrEmpty<int>(null));
        Assert.True(FrameworkCollectionExtensions.IsNullOrEmpty<string>(null));
    }

    /// <summary>
    /// 空集合判为空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenEmpty_ReturnsTrue()
    {
        Assert.True(new List<int>().IsNullOrEmpty());
        Assert.True(Array.Empty<string>().IsNullOrEmpty());
        Assert.True(new HashSet<int>().IsNullOrEmpty());
    }

    /// <summary>
    /// 非空集合判为非空
    /// </summary>
    [Fact]
    public void IsNullOrEmpty_WhenNotEmpty_ReturnsFalse()
    {
        Assert.False(new List<int> { 0 }.IsNullOrEmpty());
        Assert.False(new[] { "a" }.IsNullOrEmpty());
    }

    /// <summary>
    /// 条件为真时添加，为假时不添加
    /// </summary>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void AddIf_WithBooleanFlag_AddsOnlyWhenFlagIsTrue(bool flag, int expectedCount)
    {
        var source = new List<int>();

        source.AddIf(1, flag);

        Assert.Equal(expectedCount, source.Count);
    }

    /// <summary>
    /// 条件函数返回真时添加
    /// </summary>
    [Fact]
    public void AddIf_WithFunc_EvaluatesConditionAndAdds()
    {
        var source = new List<int>();
        var evaluated = 0;

        source.AddIf(1, () =>
        {
            evaluated++;
            return true;
        });
        source.AddIf(2, () =>
        {
            evaluated++;
            return false;
        });

        Assert.Equal(2, evaluated);
        Assert.Equal(new[] { 1 }, source);
    }

    /// <summary>
    /// 集合为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void AddIf_WhenSourceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FrameworkCollectionExtensions.AddIf<int>(null!, 1, true));
        Assert.Throws<ArgumentNullException>(() => FrameworkCollectionExtensions.AddIf<int>(null!, 1, () => true));
    }

    /// <summary>
    /// 值不为 null 才添加
    /// </summary>
    [Fact]
    public void AddIfNotNull_OnlyAddsNonNullValue()
    {
        var source = new List<string>();

        source.AddIfNotNull("a");
        source.AddIfNotNull(null!);

        Assert.Equal(new[] { "a" }, source);
    }

    /// <summary>
    /// 值类型的默认值不是 null，会被添加
    /// </summary>
    [Fact]
    public void AddIfNotNull_WhenValueTypeDefault_StillAdds()
    {
        var source = new List<int>();

        source.AddIfNotNull(0);

        Assert.Equal(new[] { 0 }, source);
    }

    /// <summary>
    /// 集合为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void AddIfNotNull_WhenSourceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FrameworkCollectionExtensions.AddIfNotNull<string>(null!, "a"));
    }

    /// <summary>
    /// 不存在时添加并返回真，已存在时不添加并返回假
    /// </summary>
    [Fact]
    public void AddIfNotContains_WithSingleItem_AddsOnlyOnce()
    {
        var source = new List<string> { "a" };

        var firstAdd = source.AddIfNotContains("b");
        var secondAdd = source.AddIfNotContains("b");

        Assert.True(firstAdd);
        Assert.False(secondAdd);
        Assert.Equal(new[] { "a", "b" }, source);
    }

    /// <summary>
    /// 批量添加只返回真正被添加的项，且不改变已有项
    /// </summary>
    [Fact]
    public void AddIfNotContains_WithItems_ReturnsOnlyAddedItems()
    {
        var source = new List<int> { 1, 2 };

        var added = source.AddIfNotContains([2, 3, 4]);

        Assert.Equal([3, 4], added);
        Assert.Equal(new[] { 1, 2, 3, 4 }, source);
    }

    /// <summary>
    /// 批量添加空序列时不产生任何变化
    /// </summary>
    [Fact]
    public void AddIfNotContains_WithEmptyItems_ChangesNothing()
    {
        var source = new List<int> { 1 };

        var added = source.AddIfNotContains([]);

        Assert.Empty(added);
        Assert.Equal(new[] { 1 }, source);
    }

    /// <summary>
    /// 谓词未命中时用工厂创建并添加，命中时不调用工厂
    /// </summary>
    [Fact]
    public void AddIfNotContains_WithPredicate_UsesFactoryOnlyWhenMissing()
    {
        var source = new List<string> { "abc" };
        var factoryCalls = 0;

        var addedWhenMissing = source.AddIfNotContains(s => s.StartsWith('x'), () =>
        {
            factoryCalls++;
            return "xyz";
        });
        var addedWhenPresent = source.AddIfNotContains(s => s.StartsWith('a'), () =>
        {
            factoryCalls++;
            return "aaa";
        });

        Assert.True(addedWhenMissing);
        Assert.False(addedWhenPresent);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(new[] { "abc", "xyz" }, source);
    }

    /// <summary>
    /// 移除所有满足条件的项并返回被移除项
    /// </summary>
    [Fact]
    public void RemoveAllWhere_RemovesMatchedItemsAndReturnsThem()
    {
        var source = new List<int> { 1, 2, 3, 4, 5 };

        var removed = source.RemoveAllWhere(x => x % 2 == 0);

        Assert.Equal([2, 4], removed);
        Assert.Equal(new[] { 1, 3, 5 }, source);
    }

    /// <summary>
    /// 无匹配项时集合不变且返回空列表
    /// </summary>
    [Fact]
    public void RemoveAllWhere_WhenNothingMatches_ReturnsEmpty()
    {
        var source = new List<int> { 1, 3 };

        var removed = source.RemoveAllWhere(x => x > 100);

        Assert.Empty(removed);
        Assert.Equal(new[] { 1, 3 }, source);
    }

    /// <summary>
    /// 空集合上移除不抛异常
    /// </summary>
    [Fact]
    public void RemoveAllWhere_WhenSourceEmpty_ReturnsEmpty()
    {
        var source = new List<int>();

        var removed = source.RemoveAllWhere(_ => true);

        Assert.Empty(removed);
    }

    /// <summary>
    /// 集合为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void RemoveAllWhere_WhenSourceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FrameworkCollectionExtensions.RemoveAllWhere<int>(null!, _ => true));
    }

    /// <summary>
    /// 批量移除只移除存在的项，重复项只移除第一个匹配
    /// </summary>
    [Fact]
    public void RemoveAll_RemovesGivenItemsOnce()
    {
        var source = new List<int> { 1, 2, 2, 3 };

        source.RemoveAll([2, 99]);

        Assert.Equal(new[] { 1, 2, 3 }, source);
    }

    /// <summary>
    /// 批量移除空序列时集合不变
    /// </summary>
    [Fact]
    public void RemoveAll_WithEmptyItems_ChangesNothing()
    {
        var source = new List<int> { 1, 2 };

        source.RemoveAll([]);

        Assert.Equal(new[] { 1, 2 }, source);
    }

    /// <summary>
    /// 集合为 null 时抛出参数空异常
    /// </summary>
    [Fact]
    public void RemoveAll_WhenSourceIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FrameworkCollectionExtensions.RemoveAll<int>(null!, [1]));
    }
}
