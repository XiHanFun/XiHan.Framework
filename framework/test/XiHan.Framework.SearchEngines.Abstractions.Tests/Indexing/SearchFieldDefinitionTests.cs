// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Abstractions.Indexing;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Indexing;

/// <summary>
/// 索引字段定义的测试
/// </summary>
/// <remarks>
/// 两个布尔开关的默认值是这个记录唯一的隐含语义：字段默认既不参与关键字检索也不可排序，
/// 必须由调用方显式打开。默认值翻转会让既有索引定义静默改变检索面。
/// </remarks>
public class SearchFieldDefinitionTests
{
    /// <summary>
    /// 未指定开关时既不可检索也不可排序
    /// </summary>
    [Fact]
    public void Constructor_WithoutFlags_DefaultsToNotSearchableAndNotSortable()
    {
        var definition = new SearchFieldDefinition("title", SearchFieldType.Text);

        Assert.Equal("title", definition.Name);
        Assert.Equal(SearchFieldType.Text, definition.Type);
        Assert.False(definition.Searchable);
        Assert.False(definition.Sortable);
    }

    /// <summary>
    /// 显式打开的开关原样保留
    /// </summary>
    [Fact]
    public void Constructor_WithFlags_KeepsThem()
    {
        var definition = new SearchFieldDefinition("views", SearchFieldType.Integer, Searchable: true, Sortable: true);

        Assert.True(definition.Searchable);
        Assert.True(definition.Sortable);
    }

    /// <summary>
    /// 可只打开其中一个开关
    /// </summary>
    /// <param name="searchable">是否参与关键字检索</param>
    /// <param name="sortable">是否可排序</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Constructor_FlagsAreIndependent(bool searchable, bool sortable)
    {
        var definition = new SearchFieldDefinition("title", SearchFieldType.Text, searchable, sortable);

        Assert.Equal(searchable, definition.Searchable);
        Assert.Equal(sortable, definition.Sortable);
    }

    /// <summary>
    /// 四个分量全等时记录相等
    /// </summary>
    [Fact]
    public void Equals_ComparesAllComponents()
    {
        var left = new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true);
        var right = new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// 任一分量不同时记录不相等
    /// </summary>
    [Fact]
    public void Equals_WhenAnyComponentDiffers_IsFalse()
    {
        var baseline = new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true, Sortable: true);

        Assert.NotEqual(baseline, new SearchFieldDefinition("summary", SearchFieldType.Text, Searchable: true, Sortable: true));
        Assert.NotEqual(baseline, new SearchFieldDefinition("title", SearchFieldType.Keyword, Searchable: true, Sortable: true));
        Assert.NotEqual(baseline, new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: false, Sortable: true));
        Assert.NotEqual(baseline, new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true, Sortable: false));
    }

    /// <summary>
    /// with 表达式只改指定分量且不影响原对象
    /// </summary>
    [Fact]
    public void With_ChangesOnlyTargetComponent()
    {
        var original = new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true);

        var copy = original with { Sortable = true };

        Assert.Equal("title", copy.Name);
        Assert.Equal(SearchFieldType.Text, copy.Type);
        Assert.True(copy.Searchable);
        Assert.True(copy.Sortable);
        Assert.False(original.Sortable);
    }

    /// <summary>
    /// 解构按声明顺序给出四个分量
    /// </summary>
    [Fact]
    public void Deconstruct_YieldsComponentsInDeclaredOrder()
    {
        var (name, type, searchable, sortable) = new SearchFieldDefinition("views", SearchFieldType.Integer, Searchable: false, Sortable: true);

        Assert.Equal("views", name);
        Assert.Equal(SearchFieldType.Integer, type);
        Assert.False(searchable);
        Assert.True(sortable);
    }
}
