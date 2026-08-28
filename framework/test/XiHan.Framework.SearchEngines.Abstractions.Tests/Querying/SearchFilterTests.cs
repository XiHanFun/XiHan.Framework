// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.SearchEngines.Abstractions.Querying;

namespace XiHan.Framework.SearchEngines.Abstractions.Tests.Querying;

/// <summary>
/// 结构化过滤条件的测试
/// </summary>
/// <remarks>
/// 构造期校验是这个类的全部价值所在：把「In 没给候选集」「比较类运算符没给值」挡在翻译成
/// Elasticsearch 查询或 SQL 谓词之前，避免各实现各自处理半成品条件。
/// 用例按运算符分三类覆盖：需要单值的、需要多值的、两者都不需要的。
/// </remarks>
public class SearchFilterTests
{
    /// <summary>
    /// 单值运算符保留字段、运算符与比较值
    /// </summary>
    [Fact]
    public void Constructor_WithSingleValue_KeepsFieldOperatorAndValue()
    {
        var filter = new SearchFilter("views", SearchFilterOperator.GreaterThan, 100);

        Assert.Equal("views", filter.Field);
        Assert.Equal(SearchFilterOperator.GreaterThan, filter.Operator);
        Assert.Equal(100, Assert.IsType<int>(filter.Value));
    }

    /// <summary>
    /// 比较值不做类型转换，装箱后原样保留
    /// </summary>
    /// <remarks>
    /// 抽象层不认识后端的字段类型，值的解释权归实现；这里锁死「不擅自转成字符串」，
    /// 否则数值范围过滤会退化成字符串比较。
    /// </remarks>
    [Fact]
    public void Constructor_DoesNotCoerceValueType()
    {
        Assert.IsType<int>(new SearchFilter("views", SearchFilterOperator.Equal, 100).Value);
        Assert.IsType<double>(new SearchFilter("score", SearchFilterOperator.Equal, 1.5d).Value);
        Assert.IsType<bool>(new SearchFilter("enabled", SearchFilterOperator.Equal, true).Value);
        Assert.IsType<string>(new SearchFilter("category", SearchFilterOperator.Equal, "framework").Value);
    }

    /// <summary>
    /// 单值运算符未给比较值时抛出参数异常
    /// </summary>
    /// <param name="op">运算符</param>
    [Theory]
    [InlineData(SearchFilterOperator.Equal)]
    [InlineData(SearchFilterOperator.NotEqual)]
    [InlineData(SearchFilterOperator.GreaterThan)]
    [InlineData(SearchFilterOperator.GreaterThanOrEqual)]
    [InlineData(SearchFilterOperator.LessThan)]
    [InlineData(SearchFilterOperator.LessThanOrEqual)]
    [InlineData(SearchFilterOperator.StartsWith)]
    public void Constructor_WhenValueRequiredButMissing_ThrowsArgumentException(SearchFilterOperator op)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchFilter("views", op));

        Assert.Equal("value", exception.ParamName);

        // 异常消息要指名是哪个运算符缺值，否则批量条件排障只能靠猜
        Assert.Contains(op.ToString(), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 单值运算符的候选值集合默认为空且非空引用
    /// </summary>
    [Fact]
    public void Values_WhenNotProvided_IsEmptyNotNull()
    {
        var filter = new SearchFilter("category", SearchFilterOperator.Equal, "framework");

        Assert.NotNull(filter.Values);
        Assert.Empty(filter.Values);
    }

    /// <summary>
    /// In 运算符保留候选值集合且不要求单值
    /// </summary>
    [Fact]
    public void Constructor_WithInOperator_KeepsValuesAndAllowsMissingValue()
    {
        var filter = new SearchFilter("category", SearchFilterOperator.In, values: ["guide", "framework"]);

        Assert.Equal(SearchFilterOperator.In, filter.Operator);
        Assert.Null(filter.Value);
        Assert.Equal(["guide", "framework"], filter.Values);
    }

    /// <summary>
    /// In 运算符未给候选值集合时抛出参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenInWithoutValues_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchFilter("category", SearchFilterOperator.In));

        Assert.Equal("values", exception.ParamName);
        Assert.Contains("候选值集合", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// In 运算符给了空候选值集合时抛出参数异常
    /// </summary>
    /// <remarks>
    /// 空集合语义上等价于「恒不命中」，放行会让实现各自决定翻译成 1=0 还是忽略该条件，
    /// 所以在构造期就拒绝。
    /// </remarks>
    [Fact]
    public void Constructor_WhenInWithEmptyValues_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchFilter("category", SearchFilterOperator.In, values: []));

        Assert.Equal("values", exception.ParamName);
    }

    /// <summary>
    /// In 运算符只给了单值仍视为未给候选集
    /// </summary>
    [Fact]
    public void Constructor_WhenInWithOnlySingleValue_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchFilter("category", SearchFilterOperator.In, "framework"));

        Assert.Equal("values", exception.ParamName);
    }

    /// <summary>
    /// Exists 运算符不需要任何值
    /// </summary>
    [Fact]
    public void Constructor_WithExistsOperator_RequiresNeitherValueNorValues()
    {
        var filter = new SearchFilter("summary", SearchFilterOperator.Exists);

        Assert.Equal("summary", filter.Field);
        Assert.Equal(SearchFilterOperator.Exists, filter.Operator);
        Assert.Null(filter.Value);
        Assert.Empty(filter.Values);
    }

    /// <summary>
    /// 字段名为空引用时抛出空引用参数异常
    /// </summary>
    [Fact]
    public void Constructor_WhenFieldNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new SearchFilter(null!, SearchFilterOperator.Exists));

        Assert.Equal("field", exception.ParamName);
    }

    /// <summary>
    /// 字段名为空串或纯空白时抛出参数异常
    /// </summary>
    /// <param name="field">字段名</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_WhenFieldBlank_ThrowsArgumentException(string field)
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchFilter(field, SearchFilterOperator.Exists));

        Assert.Equal("field", exception.ParamName);
    }

    /// <summary>
    /// 字段名先于运算符相关的值校验
    /// </summary>
    [Fact]
    public void Constructor_WhenFieldBlankAndValueMissing_ReportsFieldFirst()
    {
        var exception = Assert.Throws<ArgumentException>(() => new SearchFilter(string.Empty, SearchFilterOperator.Equal));

        Assert.Equal("field", exception.ParamName);
    }

    /// <summary>
    /// In 运算符的候选值集合只需一个元素即可
    /// </summary>
    /// <remarks>
    /// 边界确认：拒绝的是「空集合」而不是「元素不足两个」，
    /// 调用方按同一条代码路径拼多值与单值条件，不必为单值退化成 Equal。
    /// </remarks>
    [Fact]
    public void Constructor_WhenInWithSingleCandidate_IsAccepted()
    {
        var filter = new SearchFilter("category", SearchFilterOperator.In, values: ["framework"]);

        Assert.Single(filter.Values);
        Assert.Equal("framework", Assert.IsType<string>(filter.Values[0]));
    }
}
