// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Querying;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Querying;

/// <summary>
/// 检索请求的测试
/// </summary>
/// <remarks>
/// 这个类的默认值就是契约的一部分：默认页大小 20、各集合默认为空而非空引用、
/// 关键字可空表示只按过滤条件检索。实现方是直接读这些属性拼查询的，
/// 默认值一变，所有实现的行为跟着变。分页参数的校验在 init 访问器里，
/// 只能通过对象初始化器触发。
/// </remarks>
public class SearchRequestTests
{
    /// <summary>
    /// 只给索引名时其余项取默认值
    /// </summary>
    [Fact]
    public void Constructor_WithIndexOnly_UsesDefaults()
    {
        var request = new SearchRequest("articles");

        Assert.Equal("articles", request.Index);
        Assert.Null(request.Keyword);
        Assert.Empty(request.Fields);
        Assert.Empty(request.Filters);
        Assert.Empty(request.Sorts);
        Assert.Empty(request.HighlightFields);
        Assert.Equal(0, request.Skip);
        Assert.Equal(20, request.Take);
    }

    /// <summary>
    /// 各集合默认为空集合而非空引用
    /// </summary>
    /// <remarks>
    /// 实现方会直接遍历这些集合，默认空引用会把判空责任推给每一个实现。
    /// </remarks>
    [Fact]
    public void Collections_DefaultToEmptyNotNull()
    {
        var request = new SearchRequest("articles");

        Assert.NotNull(request.Fields);
        Assert.NotNull(request.Filters);
        Assert.NotNull(request.Sorts);
        Assert.NotNull(request.HighlightFields);
    }

    /// <summary>
    /// 索引名为空引用时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenIndexNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SearchRequest(null!));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 索引名为空串或纯空白时抛出参数异常
    /// </summary>
    /// <param name="index">索引名</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_WhenIndexBlank_ThrowsArgumentException(string index)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchRequest(index));

        Assert.Equal("index", exception.ParamName);
    }

    /// <summary>
    /// 对象初始化器设置的各项原样保留
    /// </summary>
    [Fact]
    public void ObjectInitializer_KeepsAllComponents()
    {
        var filter = new SearchFilter("category", SearchFilterOperator.Equal, "framework");

        var request = new SearchRequest("articles")
        {
            Keyword = "曦寒",
            Fields = ["title", "summary"],
            Filters = [filter],
            Sorts = [SearchSort.ByScore],
            HighlightFields = ["title"],
            Skip = 20,
            Take = 10
        };

        Assert.Equal("曦寒", request.Keyword);
        Assert.Equal(["title", "summary"], request.Fields);
        Assert.Single(request.Filters);
        Assert.Same(filter, request.Filters[0]);
        Assert.Single(request.Sorts);
        Assert.Same(SearchSort.ByScore, request.Sorts[0]);
        Assert.Equal(["title"], request.HighlightFields);
        Assert.Equal(20, request.Skip);
        Assert.Equal(10, request.Take);
    }

    /// <summary>
    /// 跳过条数为负数时抛出范围异常
    /// </summary>
    /// <param name="skip">跳过条数</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Skip_WhenNegative_ThrowsArgumentOutOfRangeException(int skip)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SearchRequest("articles") { Skip = skip });

        Assert.Contains("不能为负数", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 跳过条数为零或正数时接受
    /// </summary>
    /// <param name="skip">跳过条数</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Skip_WhenNotNegative_IsAccepted(int skip)
    {
        var request = new SearchRequest("articles") { Skip = skip };

        Assert.Equal(skip, request.Skip);
    }

    /// <summary>
    /// 获取条数为零或负数时抛出范围异常
    /// </summary>
    /// <remarks>
    /// 零条不是「不限」而是无意义请求，抽象层直接拒绝，避免各实现把 0 翻译成不同语义。
    /// </remarks>
    /// <param name="take">获取条数</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Take_WhenNotPositive_ThrowsArgumentOutOfRangeException(int take)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new SearchRequest("articles") { Take = take });

        Assert.Contains("必须大于 0", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 获取条数为正数时接受
    /// </summary>
    /// <param name="take">获取条数</param>
    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void Take_WhenPositive_IsAccepted(int take)
    {
        var request = new SearchRequest("articles") { Take = take };

        Assert.Equal(take, request.Take);
    }

    /// <summary>
    /// 只设置跳过条数时获取条数仍取默认页大小
    /// </summary>
    /// <remarks>
    /// 「翻页只改偏移」是最常见的调用姿势，两个分页属性各自独立，
    /// 设一个不会把另一个连带重置。
    /// </remarks>
    [Fact]
    public void Skip_WhenSetAlone_LeavesTakeAtDefault()
    {
        var request = new SearchRequest("articles") { Skip = 40 };

        Assert.Equal(40, request.Skip);
        Assert.Equal(20, request.Take);
    }
}
