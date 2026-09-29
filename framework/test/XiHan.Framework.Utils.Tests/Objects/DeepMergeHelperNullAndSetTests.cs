// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Utils.Objects;

namespace XiHan.Framework.Utils.Tests.Objects;

/// <summary>
/// 深度合并帮助类对 null 配置项与非 IList 集合的回归测试
/// </summary>
/// <remarks>
/// 覆盖两处原先会静默丢数据或直接抛异常的缺陷：
/// 一是 ProcessProperty 对每个元素调 PropertyInfo.GetValue(config)，
/// 数组里混入 null 时实例属性拿不到目标对象，抛 TargetException，
/// 而"某一层配置缺省就传 null"正是本方法最常见的调用方式；
/// 二是 DeepClone 的分支只认 IDictionary 与非泛型 IList，
/// HashSet&lt;T&gt;、Queue&lt;T&gt; 落到 CloneComplexObject，
/// 而那条路靠"Activator.CreateInstance + 复制可写属性"工作，
/// 这类集合没有可写属性，克隆结果是一个空集合，元素被静默丢光。
/// </remarks>
public class DeepMergeHelperNullAndSetTests
{
    /// <summary>
    /// 高优先级配置为 null 时取低优先级配置的值
    /// </summary>
    [Fact]
    public void DeepMerge_WhenFirstConfigIsNull_UsesRemainingConfig()
    {
        var merged = DeepMergeHelper.DeepMerge(null!, new SetConfig { Name = "低" });

        Assert.Equal("低", merged.Name);
    }

    /// <summary>
    /// 低优先级配置为 null 时取高优先级配置的值
    /// </summary>
    [Fact]
    public void DeepMerge_WhenLastConfigIsNull_UsesRemainingConfig()
    {
        var merged = DeepMergeHelper.DeepMerge(new SetConfig { Name = "高" }, null!);

        Assert.Equal("高", merged.Name);
    }

    /// <summary>
    /// 夹在中间的 null 被跳过，其余配置照常按优先级合并
    /// </summary>
    [Fact]
    public void DeepMerge_WhenMiddleConfigIsNull_SkipsItAndMergesRest()
    {
        var merged = DeepMergeHelper.DeepMerge(
            new SetConfig { Tags = ["a"] },
            null!,
            new SetConfig { Name = "低", Tags = ["b"] });

        Assert.Equal("低", merged.Name);
        Assert.Equal(new[] { "a", "b" }, merged.Tags.OrderBy(tag => tag));
    }

    /// <summary>
    /// 全部配置为 null 时返回默认实例
    /// </summary>
    [Fact]
    public void DeepMerge_WhenAllConfigsAreNull_ReturnsDefaultInstance()
    {
        var merged = DeepMergeHelper.DeepMerge(new SetConfig[] { null!, null! });

        Assert.NotNull(merged);
        Assert.Null(merged.Name);
        Assert.Empty(merged.Tags);
    }

    /// <summary>
    /// 唯一配置为 null 时返回默认实例
    /// </summary>
    [Fact]
    public void DeepMerge_WhenSingleConfigIsNull_ReturnsDefaultInstance()
    {
        var merged = DeepMergeHelper.DeepMerge(new SetConfig[] { null! });

        Assert.NotNull(merged);
        Assert.Null(merged.Name);
    }

    /// <summary>
    /// 单配置深拷贝时集合属性的元素不会丢失
    /// </summary>
    [Fact]
    public void DeepMerge_WithSingleConfig_KeepsSetElements()
    {
        var source = new SetConfig { Tags = ["a", "b"] };

        var merged = DeepMergeHelper.DeepMerge(source);

        Assert.Equal(new[] { "a", "b" }, merged.Tags.OrderBy(tag => tag));
        Assert.NotSame(source.Tags, merged.Tags);
    }

    /// <summary>
    /// 集合属性按优先级合并，低优先级的新增项会被并进来
    /// </summary>
    [Fact]
    public void DeepMerge_SetProperty_MergesAcrossConfigs()
    {
        var high = new SetConfig { Tags = ["a", "b"] };
        var low = new SetConfig { Tags = ["c"] };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.Equal(new[] { "a", "b", "c" }, merged.Tags.OrderBy(tag => tag));
    }

    /// <summary>
    /// 合并后的集合仍然是原来的集合类型，能正常回写到属性上
    /// </summary>
    [Fact]
    public void DeepMerge_SetProperty_StaysSameCollectionType()
    {
        var merged = DeepMergeHelper.DeepMerge(
            new SetConfig { Tags = ["a"] },
            new SetConfig { Tags = ["b"] });

        Assert.IsType<HashSet<string>>(merged.Tags);
    }

    /// <summary>
    /// 高优先级集合为空时不遮蔽低优先级集合
    /// </summary>
    /// <remarks>
    /// HashSet&lt;T&gt; 不实现非泛型 ICollection，原先 IsNullOrDefault 落到按引用比较的默认分支，
    /// 一个空集合被当成"有值"，把低优先级里真正有内容的集合整个挡掉。
    /// </remarks>
    [Fact]
    public void DeepMerge_WhenHighPrioritySetIsEmpty_UsesLowPriority()
    {
        var merged = DeepMergeHelper.DeepMerge(
            new SetConfig(),
            new SetConfig { Tags = ["b"] });

        Assert.Equal(new[] { "b" }, merged.Tags);
    }

    /// <summary>
    /// 队列属性克隆后元素与顺序都保持不变
    /// </summary>
    [Fact]
    public void DeepMerge_QueueProperty_KeepsElementsAndOrder()
    {
        var source = new SetConfig();
        source.Steps.Enqueue("first");
        source.Steps.Enqueue("second");

        var merged = DeepMergeHelper.DeepMerge(source);

        Assert.Equal(new[] { "first", "second" }, merged.Steps);
        Assert.NotSame(source.Steps, merged.Steps);
    }

    /// <summary>
    /// 嵌套对象里的集合属性同样不会被清空
    /// </summary>
    [Fact]
    public void DeepMerge_NestedSetProperty_MergesInsteadOfClearing()
    {
        var high = new SetConfig { Child = new NestedSet { Roles = ["admin"] } };
        var low = new SetConfig { Child = new NestedSet { Roles = ["user"] } };

        var merged = DeepMergeHelper.DeepMerge(high, low);

        Assert.NotNull(merged.Child);
        Assert.Equal(new[] { "admin", "user" }, merged.Child!.Roles.OrderBy(role => role));
    }

    /// <summary>
    /// 测试用集合配置对象
    /// </summary>
    private sealed class SetConfig
    {
        public string? Name { get; set; }

        public HashSet<string> Tags { get; set; } = [];

        public Queue<string> Steps { get; set; } = new();

        public NestedSet? Child { get; set; }
    }

    /// <summary>
    /// 测试用嵌套配置对象
    /// </summary>
    private sealed class NestedSet
    {
        public HashSet<string> Roles { get; set; } = [];
    }
}
