// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Collections;

namespace XiHan.Framework.Utils.Tests.Collections;

/// <summary>
/// 可列举扩展方法测试
/// </summary>
public class EnumerableExtensionsTests
{
    /// <summary>
    /// 字符串序列按分隔符拼接
    /// </summary>
    [Fact]
    public void JoinAsString_WithStrings_JoinsWithSeparator()
    {
        var source = new[] { "a", "b", "c" };

        Assert.Equal("a-b-c", source.JoinAsString("-"));
    }

    /// <summary>
    /// 空序列拼接得到空串，单元素不带分隔符
    /// </summary>
    [Fact]
    public void JoinAsString_WhenEmptyOrSingle_OmitsSeparator()
    {
        Assert.Equal(string.Empty, Array.Empty<string>().JoinAsString("-"));
        Assert.Equal("a", new[] { "a" }.JoinAsString("-"));
    }

    /// <summary>
    /// 任意类型序列按 ToString 拼接
    /// </summary>
    [Fact]
    public void JoinAsString_WithGenericItems_UsesToString()
    {
        var source = new[] { 1, 2, 3 };

        Assert.Equal("1,2,3", source.JoinAsString(","));
    }

    /// <summary>
    /// 条件为真时应用谓词过滤
    /// </summary>
    [Fact]
    public void WhereIf_WhenConditionTrue_AppliesPredicate()
    {
        IEnumerable<int> source = [1, 2, 3, 4];

        var result = source.WhereIf(true, x => x > 2);

        Assert.Equal([3, 4], result);
    }

    /// <summary>
    /// 条件为假时原样返回同一个序列实例
    /// </summary>
    [Fact]
    public void WhereIf_WhenConditionFalse_ReturnsSameSequence()
    {
        IEnumerable<int> source = [1, 2, 3, 4];

        var result = source.WhereIf(false, x => x > 2);

        Assert.Same(source, result);
    }

    /// <summary>
    /// 带索引的谓词重载按下标过滤
    /// </summary>
    [Fact]
    public void WhereIf_WithIndexedPredicate_FiltersByIndex()
    {
        IEnumerable<string> source = ["a", "b", "c", "d"];

        var result = source.WhereIf(true, (_, index) => index % 2 == 0);

        Assert.Equal(["a", "c"], result);
    }

    /// <summary>
    /// 带索引的谓词重载在条件为假时同样短路
    /// </summary>
    [Fact]
    public void WhereIf_WithIndexedPredicate_WhenConditionFalse_ReturnsSameSequence()
    {
        IEnumerable<string> source = ["a", "b"];

        var result = source.WhereIf(false, (_, index) => index == 0);

        Assert.Same(source, result);
    }

    /// <summary>
    /// 随机取值一定落在原序列内
    /// </summary>
    [Fact]
    public void GetRandom_ReturnsElementFromSource()
    {
        IEnumerable<int> source = [1, 2, 3];

        for (var i = 0; i < 20; i++)
        {
            Assert.Contains(source.GetRandom(), source);
        }
    }

    /// <summary>
    /// 单元素序列必然返回该元素
    /// </summary>
    [Fact]
    public void GetRandom_WhenSingleElement_ReturnsIt()
    {
        IEnumerable<string> source = ["only"];

        Assert.Equal("only", source.GetRandom());
    }

    /// <summary>
    /// 空序列取随机值抛参数异常
    /// </summary>
    [Fact]
    public void GetRandom_WhenEmpty_Throws()
    {
        IEnumerable<int> source = [];

        Assert.Throws<ArgumentException>(() => source.GetRandom());
    }

    /// <summary>
    /// 序列为 null 时抛参数空异常
    /// </summary>
    [Fact]
    public void GetRandom_WhenNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EnumerableExtensions.GetRandom<int>(null!));
    }

    /// <summary>
    /// 拓扑排序把被依赖项排在依赖方之前
    /// </summary>
    [Fact]
    public void SortByDependencies_PlacesDependenciesFirst()
    {
        var dependencies = new Dictionary<string, string[]>
        {
            ["a"] = [],
            ["b"] = ["a"],
            ["c"] = ["b"]
        };
        var source = new[] { "c", "a", "b" };

        var sorted = source.SortByDependencies(item => dependencies[item]);

        Assert.Equal(new[] { "a", "b", "c" }, sorted);
    }

    /// <summary>
    /// 没有依赖关系时保持原有顺序
    /// </summary>
    [Fact]
    public void SortByDependencies_WhenNoDependencies_KeepsOriginalOrder()
    {
        var source = new[] { "x", "y", "z" };

        var sorted = source.SortByDependencies(_ => []);

        Assert.Equal(new[] { "x", "y", "z" }, sorted);
    }

    /// <summary>
    /// 空序列排序得到空列表
    /// </summary>
    [Fact]
    public void SortByDependencies_WhenEmpty_ReturnsEmpty()
    {
        var sorted = Array.Empty<string>().SortByDependencies(_ => []);

        Assert.Empty(sorted);
    }

    /// <summary>
    /// 出现循环依赖时抛参数异常
    /// </summary>
    [Fact]
    public void SortByDependencies_WhenCircular_Throws()
    {
        var dependencies = new Dictionary<string, string[]>
        {
            ["a"] = ["b"],
            ["b"] = ["a"]
        };
        var source = new[] { "a", "b" };

        var ex = Assert.Throws<ArgumentException>(() => source.SortByDependencies(item => dependencies[item]));
        Assert.Contains("循环依赖", ex.Message);
    }

    /// <summary>
    /// 自定义相等比较器参与访问标记，忽略大小写时同名项只出现一次
    /// </summary>
    [Fact]
    public void SortByDependencies_WithComparer_TreatsEqualItemsAsOne()
    {
        var source = new[] { "A", "a" };

        var sorted = source.SortByDependencies(_ => [], StringComparer.OrdinalIgnoreCase);

        Assert.Equal(new[] { "A" }, sorted);
    }
}
