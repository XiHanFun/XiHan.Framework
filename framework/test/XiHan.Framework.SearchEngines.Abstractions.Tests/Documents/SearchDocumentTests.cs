// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Documents;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Documents;

/// <summary>
/// 带标识的待索引文档的测试
/// </summary>
/// <remarks>
/// 该记录把校验写在属性初始化器里，所以校验只在主构造器这一条路径上生效。
/// 用例锁的是「标识非空白、文档非空」这两条写入前置条件，以及记录的相等语义——
/// 文档按引用比较，不会递归到文档内部字段。
/// </remarks>
public class SearchDocumentTests
{
    /// <summary>
    /// 合法入参原样保留标识与文档
    /// </summary>
    [Fact]
    public void Constructor_WithValidArguments_KeepsIdAndDocument()
    {
        var document = new SearchTestDocument { Title = "曦寒框架" };

        var searchDocument = new SearchDocument<SearchTestDocument>("article-1", document);

        Assert.Equal("article-1", searchDocument.Id);
        Assert.Same(document, searchDocument.Document);
    }

    /// <summary>
    /// 标识为空或纯空白时抛出参数异常
    /// </summary>
    /// <param name="id">文档标识</param>
    // 最后一例是全角空格 U+3000：中文录入场景常见，IsNullOrWhiteSpace 同样判空
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("　")]
    public void Constructor_WhenIdBlank_ThrowsArgumentException(string id)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchDocument<SearchTestDocument>(id, new SearchTestDocument()));

        Assert.Equal("Id", exception.ParamName);
    }

    /// <summary>
    /// 标识为空引用时抛出的是参数异常而非空引用参数异常
    /// </summary>
    /// <remarks>
    /// 校验走的是 IsNullOrWhiteSpace 分支，null 与空白走同一个出口。
    /// 调用方按 ArgumentNullException 捕获会漏掉这一情况，故单独锁死。
    /// </remarks>
    [Fact]
    public void Constructor_WhenIdNull_ThrowsArgumentExceptionRatherThanArgumentNull()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchDocument<SearchTestDocument>(null!, new SearchTestDocument()));

        Assert.Equal("Id", exception.ParamName);
    }

    /// <summary>
    /// 文档为空时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenDocumentNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SearchDocument<SearchTestDocument>("article-1", null!));

        Assert.Equal("Document", exception.ParamName);
    }

    /// <summary>
    /// 标识与文档同时非法时先报标识
    /// </summary>
    /// <remarks>
    /// 属性初始化器按声明顺序执行，Id 在前，所以异常必然指向 Id。
    /// </remarks>
    [Fact]
    public void Constructor_WhenBothInvalid_ReportsIdFirst()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchDocument<SearchTestDocument>(string.Empty, null!));

        Assert.Equal("Id", exception.ParamName);
    }

    /// <summary>
    /// 标识不做去空白归一化
    /// </summary>
    /// <remarks>
    /// 首尾带空白的标识是合法的，且原样入库——
    /// 这意味着 " a " 与 "a" 是两个不同的文档，调用方需自行归一化。
    /// </remarks>
    [Fact]
    public void Constructor_DoesNotTrimId()
    {
        var searchDocument = new SearchDocument<SearchTestDocument>(" article-1 ", new SearchTestDocument());

        Assert.Equal(" article-1 ", searchDocument.Id);
    }

    /// <summary>
    /// 标识与文档实例都相同时记录相等
    /// </summary>
    [Fact]
    public void Equals_WithSameIdAndSameDocumentInstance_IsTrue()
    {
        var document = new SearchTestDocument();

        var left = new SearchDocument<SearchTestDocument>("article-1", document);
        var right = new SearchDocument<SearchTestDocument>("article-1", document);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// 文档为不同实例时记录不相等
    /// </summary>
    [Fact]
    public void Equals_WithDistinctDocumentInstances_IsFalse()
    {
        var left = new SearchDocument<SearchTestDocument>("article-1", new SearchTestDocument { Title = "同一份内容" });
        var right = new SearchDocument<SearchTestDocument>("article-1", new SearchTestDocument { Title = "同一份内容" });

        Assert.NotEqual(left, right);
    }

    /// <summary>
    /// 标识不同时记录不相等
    /// </summary>
    [Fact]
    public void Equals_WithDifferentId_IsFalse()
    {
        var document = new SearchTestDocument();

        Assert.NotEqual(
            new SearchDocument<SearchTestDocument>("article-1", document),
            new SearchDocument<SearchTestDocument>("article-2", document));
    }
}
