// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Objects;

namespace XiHan.Framework.Utils.Tests.Objects;

/// <summary>
/// 深度合并帮助类的集合合并回归测试
/// </summary>
/// <remarks>
/// 原实现 IsMergeableCollection 用 typeof(IList&lt;&gt;).IsAssignableFrom(type.GetGenericTypeDefinition()) 判定，
/// 开放泛型之间的 IsAssignableFrom 恒为 false，List&lt;T&gt; 一律被判成不可合并，
/// 类注释承诺的"合并集合"对泛型集合完全失效；只有数组能进合并分支，
/// 而数组进去以后 MergeCollections 产出的是 List&lt;T&gt;，回写数组属性时 SetValue 抛 ArgumentException 被吞掉，
/// 属性最终一个值都没设上。本文件把「泛型列表能合并」与「数组合并后仍是数组」两条锁住。
/// </remarks>
public class DeepMergeHelperCollectionTests
{
    /// <summary>
    /// 泛型列表按优先级合并，低优先级的新增项会被并进来
    /// </summary>
    [Fact]
    public void DeepMerge_ListProperty_MergesAcrossConfigs()
    {
        var high = new CollectionConfig { Tags = ["a"] };
        var low = new CollectionConfig { Tags = ["a", "b"] };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.Equal(new[] { "a", "b" }, merged.Tags);
    }

    /// <summary>
    /// 合并结果与两个来源都不是同一个实例
    /// </summary>
    [Fact]
    public void DeepMerge_ListProperty_ResultIsIndependentCopy()
    {
        var high = new CollectionConfig { Tags = ["a"] };
        var low = new CollectionConfig { Tags = ["b"] };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.NotSame(high.Tags, merged.Tags);
        Assert.NotSame(low.Tags, merged.Tags);
        Assert.Equal(new[] { "a", "b" }, merged.Tags);
    }

    /// <summary>
    /// 数组属性合并后仍然是数组，能正常回写到属性上
    /// </summary>
    [Fact]
    public void DeepMerge_ArrayProperty_MergesAndStaysArray()
    {
        var high = new CollectionConfig { Hosts = ["h1"] };
        var low = new CollectionConfig { Hosts = ["h1", "h2"] };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.Equal(new[] { "h1", "h2" }, merged.Hosts);
    }

    /// <summary>
    /// 高优先级集合为空时整体采用低优先级集合
    /// </summary>
    [Fact]
    public void DeepMerge_WhenHighPriorityCollectionEmpty_UsesLowPriority()
    {
        var high = new CollectionConfig();
        var low = new CollectionConfig { Tags = ["b"], Hosts = ["h2"] };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.Equal(new[] { "b" }, merged.Tags);
        Assert.Equal(new[] { "h2" }, merged.Hosts);
    }

    /// <summary>
    /// 三个配置时按优先级依次并入，重复项只保留一份
    /// </summary>
    [Fact]
    public void DeepMerge_WithThreeConfigs_MergesAllAndDeduplicates()
    {
        var first = new CollectionConfig { Tags = ["a"] };
        var second = new CollectionConfig { Tags = ["a", "b"] };
        var third = new CollectionConfig { Tags = ["b", "c"] };

        var merged = DeepMergeHelper.DeepMerge(first, second, third);

        Assert.Equal(new[] { "a", "b", "c" }, merged.Tags);
    }

    /// <summary>
    /// 字符串属性不会被当成可枚举去做集合合并
    /// </summary>
    [Fact]
    public void DeepMerge_StringProperty_IsNotTreatedAsCollection()
    {
        var high = new CollectionConfig { Name = "高" };
        var low = new CollectionConfig { Name = "低" };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.Equal("高", merged.Name);
    }

    /// <summary>
    /// 测试用集合配置对象
    /// </summary>
    private sealed class CollectionConfig
    {
        public string? Name { get; set; }

        public List<string> Tags { get; set; } = [];

        public string[] Hosts { get; set; } = [];
    }
}
