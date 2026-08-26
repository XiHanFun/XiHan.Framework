// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Attributes;
using XiHan.Framework.Domain.Shared.Paging.Builders;
using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Builders;

/// <summary>
/// 自动查询构建器的测试
/// </summary>
public class AutoQueryBuilderTests
{
    /// <summary>
    /// 字符串属性默认必须生成包含过滤条件
    /// </summary>
    [Fact]
    public void BuildFrom_StringProperty_GeneratesContainsFilter()
    {
        var dto = new { Name = "zhang" };

        var request = AutoQueryBuilder.BuildFrom(dto);

        Assert.Single(request.Conditions.Filters);
        var filter = request.Conditions.Filters[0];
        Assert.Equal("Name", filter.Field);
        Assert.Equal(QueryOperator.Contains, filter.Operator);
        Assert.Equal("zhang", (string)filter.Value!);
    }

    /// <summary>
    /// 完整 DTO 必须生成过滤、范围、列表、关键字与分页条件
    /// </summary>
    [Fact]
    public void BuildFrom_FullDto_GeneratesFilters_AndKeyword()
    {
        var start = new DateTime(2025, 1, 1);
        var end = new DateTime(2025, 2, 1);
        var dto = new QueryDto
        {
            Name = "zhang",
            Age = 18,
            CreateTimeRange = [start, end],
            StatusList = [1, 2, 3],
            Keyword = "hello",
            Title = "abc"
        };

        var request = AutoQueryBuilder.BuildFrom(dto);

        var filters = request.Conditions.Filters;
        Assert.Equal(5, filters.Count);

        Assert.Contains(filters, f => f.Field == "Name" && f.Operator == QueryOperator.Contains);
        Assert.Contains(filters, f => f.Field == "Age" && f.Operator == QueryOperator.Equal);
        Assert.Contains(filters, f => f.Field == "CreateTime" && f.Operator == QueryOperator.Between);
        Assert.Contains(filters, f => f.Field == "Status" && f.Operator == QueryOperator.In);
        Assert.Contains(filters, f => f.Field == "Title" && f.Operator == QueryOperator.Contains);

        Assert.NotNull(request.Conditions.Keyword);
        Assert.Equal("hello", request.Conditions.Keyword!.Value);
        Assert.Equal(["Title"], request.Conditions.Keyword.Fields);
    }
}

/// <summary>
/// 自动查询样例 DTO
/// </summary>
public class QueryDto
{
    /// <summary>
    /// 字符串名称字段
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 可空整数年龄字段
    /// </summary>
    public int? Age { get; set; }

    /// <summary>
    /// 创建时间范围字段
    /// </summary>
    public DateTime[]? CreateTimeRange { get; set; }

    /// <summary>
    /// 状态列表字段
    /// </summary>
    public List<int>? StatusList { get; set; }

    /// <summary>
    /// 关键字输入字段
    /// </summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// 参与关键字搜索的标题字段
    /// </summary>
    [KeywordSearch]
    public string Title { get; set; } = string.Empty;
}
