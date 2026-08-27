// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Indexing;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Indexing;

/// <summary>
/// 索引定义的测试
/// </summary>
/// <remarks>
/// 索引定义是建索引的唯一入参，所以校验必须在构造期就把「无名索引」「空字段清单引用」挡住，
/// 不能等到各实现去后端建索引时才炸。语言是可选项，为空表示交由实现取默认分析器。
/// </remarks>
public class SearchIndexDefinitionTests
{
    /// <summary>
    /// 合法入参保留索引名与字段清单顺序
    /// </summary>
    [Fact]
    public void Constructor_WithValidArguments_KeepsNameAndFieldsInOrder()
    {
        var fields = new[]
        {
            new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true),
            new SearchFieldDefinition("category", SearchFieldType.Keyword),
            new SearchFieldDefinition("views", SearchFieldType.Integer, Sortable: true)
        };

        var definition = new SearchIndexDefinition("articles", fields);

        Assert.Equal("articles", definition.Name);
        Assert.Equal(fields, definition.Fields);
    }

    /// <summary>
    /// 未指定语言时为空，交由实现取默认值
    /// </summary>
    [Fact]
    public void Language_WhenNotSpecified_IsNull()
    {
        var definition = new SearchIndexDefinition("articles", [new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true)]);

        Assert.Null(definition.Language);
    }

    /// <summary>
    /// 指定的语言原样保留
    /// </summary>
    [Fact]
    public void Language_WhenSpecified_IsKept()
    {
        var definition = new SearchIndexDefinition("articles", [new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true)])
        {
            Language = "zh"
        };

        Assert.Equal("zh", definition.Language);
    }

    /// <summary>
    /// 索引名为空引用时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenNameNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SearchIndexDefinition(null!, []));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 索引名为空串或纯空白时抛出参数异常
    /// </summary>
    /// <param name="name">索引名</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_WhenNameBlank_ThrowsArgumentException(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchIndexDefinition(name, []));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 字段清单为空引用时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenFieldsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SearchIndexDefinition("articles", null!));

        Assert.Equal("fields", exception.ParamName);
    }

    /// <summary>
    /// 索引名先于字段清单校验
    /// </summary>
    [Fact]
    public void Constructor_WhenBothInvalid_ReportsNameFirst()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchIndexDefinition(string.Empty, null!));

        Assert.Equal("name", exception.ParamName);
    }

    /// <summary>
    /// 索引定义按引用比较，不做值相等
    /// </summary>
    /// <remarks>
    /// 它是普通类而非记录：两份内容相同的定义互不相等，
    /// 实现方要判断「映射是否变化」必须自己逐字段比对，不能直接用 Equals。
    /// </remarks>
    [Fact]
    public void Equals_ComparesByReference()
    {
        var fields = new[] { new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true) };

        var left = new SearchIndexDefinition("articles", fields);
        var right = new SearchIndexDefinition("articles", fields);

        Assert.NotEqual(left, right);
        Assert.Equal(left, left);
    }
}
