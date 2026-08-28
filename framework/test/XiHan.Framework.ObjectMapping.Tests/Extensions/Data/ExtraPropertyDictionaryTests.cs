// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.ObjectMapping.Extensions.Data;

namespace XiHan.Framework.ObjectMapping.Tests.Extensions.Data;

/// <summary>
/// 额外属性字典测试
/// </summary>
/// <remarks>
/// 该类型是持久化与序列化的载体，两个契约必须锁死：
/// 1. 它就是 <c>Dictionary&lt;string, object?&gt;</c>，允许 null 值、键区分大小写；
/// 2. 它是「引用相等」的类，内容相同的两个实例并不相等——值比较只能走 HasSameItems，
///    这一点很容易被误用成 Equals，因此专门写了断言把语义钉住。
/// </remarks>
public class ExtraPropertyDictionaryTests
{
    /// <summary>
    /// 默认构造得到空字典
    /// </summary>
    [Fact]
    public void Constructor_Default_CreatesEmptyDictionary()
    {
        var sut = new ExtraPropertyDictionary();

        Assert.Empty(sut);
    }

    /// <summary>
    /// 该类型必须继续以 Dictionary&lt;string, object?&gt; 形式对外暴露，序列化契约依赖它
    /// </summary>
    [Fact]
    public void Type_IsStringToNullableObjectDictionary()
    {
        var sut = new ExtraPropertyDictionary();

        Assert.IsAssignableFrom<Dictionary<string, object?>>(sut);
        Assert.IsAssignableFrom<IDictionary<string, object?>>(sut);
    }

    /// <summary>
    /// 使用源字典构造时会复制全部键值对
    /// </summary>
    [Fact]
    public void Constructor_WithDictionary_CopiesAllEntries()
    {
        var source = new Dictionary<string, object?>
        {
            ["Name"] = "曦寒",
            ["Age"] = 18
        };

        var sut = new ExtraPropertyDictionary(source);

        Assert.Equal(2, sut.Count);
        Assert.Equal("曦寒", sut["Name"]);
        Assert.Equal(18, sut["Age"]);
    }

    /// <summary>
    /// 使用源字典构造得到的是快照，之后修改源字典不会影响它
    /// </summary>
    [Fact]
    public void Constructor_WithDictionary_TakesSnapshotInsteadOfSharingStorage()
    {
        var source = new Dictionary<string, object?>
        {
            ["Name"] = "曦寒"
        };

        var sut = new ExtraPropertyDictionary(source);
        source["Name"] = "改过了";
        source["Added"] = 1;

        Assert.Equal("曦寒", sut["Name"]);
        Assert.False(sut.ContainsKey("Added"));
    }

    /// <summary>
    /// 源字典为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_WhenDictionaryNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ExtraPropertyDictionary(null!));
    }

    /// <summary>
    /// 允许存放 null 值，且 null 值的键仍然算「存在」
    /// </summary>
    [Fact]
    public void Indexer_AllowsNullValueAndKeepsKeyPresent()
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Nullable"] = null
        };

        Assert.True(sut.ContainsKey("Nullable"));
        Assert.Null(sut["Nullable"]);
        Assert.Single(sut);
    }

    /// <summary>
    /// 键比较为序数区分大小写，不做任何忽略大小写的宽松处理
    /// </summary>
    [Fact]
    public void Keys_AreCaseSensitive()
    {
        var sut = new ExtraPropertyDictionary
        {
            ["Name"] = "大写",
            ["name"] = "小写"
        };

        Assert.Equal(2, sut.Count);
        Assert.Equal("大写", sut["Name"]);
        Assert.Equal("小写", sut["name"]);
    }

    /// <summary>
    /// 内容相同的两个实例互不相等：它是类不是记录，只有引用相等
    /// </summary>
    [Fact]
    public void Equals_WithSameContent_IsStillReferenceEquality()
    {
        var left = new ExtraPropertyDictionary
        {
            ["Name"] = "曦寒"
        };
        var right = new ExtraPropertyDictionary
        {
            ["Name"] = "曦寒"
        };

        Assert.False(left.Equals(right));
        Assert.NotSame(left, right);
        Assert.True(left.Equals(left));
    }
}
