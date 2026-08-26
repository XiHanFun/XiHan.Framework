// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Builders;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Tests.Paging.Builders;

/// <summary>
/// 查询构建器的测试
/// </summary>
public class QueryBuilderTests
{
    /// <summary>
    /// 链式调用必须生成包含过滤、排序、关键字与分页的请求
    /// </summary>
    [Fact]
    public void Build_FluentChain_ProducesRequest()
    {
        var request = QueryBuilder.Create()
            .WhereEqual("Age", 30)
            .OrderByDescending("CreatedAt")
            .SetKeywordSearch("zhang", "Name")
            .SetPaging(2, 50)
            .Build();

        Assert.Single(request.Conditions.Filters);
        Assert.Equal("Age", request.Conditions.Filters[0].Field);
        Assert.Equal(QueryOperator.Equal, request.Conditions.Filters[0].Operator);
        Assert.Equal(30, (int)request.Conditions.Filters[0].Value!);

        Assert.Single(request.Conditions.Sorts);
        Assert.Equal("CreatedAt", request.Conditions.Sorts[0].Field);
        Assert.Equal(SortDirection.Descending, request.Conditions.Sorts[0].Direction);

        Assert.NotNull(request.Conditions.Keyword);
        Assert.Equal("zhang", request.Conditions.Keyword!.Value);
        Assert.Equal(["Name"], request.Conditions.Keyword.Fields);

        Assert.Equal(2, request.Page.PageIndex);
        Assert.Equal(50, request.Page.PageSize);
    }

    /// <summary>
    /// 设置越界分页参数时必须被夹取到合法区间
    /// </summary>
    [Fact]
    public void SetPaging_ClampsOutOfRangeValues()
    {
        var request = QueryBuilder.Create().SetPaging(0, 10000).Build();

        Assert.Equal(1, request.Page.PageIndex);
        Assert.Equal(500, request.Page.PageSize);
    }

    /// <summary>
    /// 从已有请求克隆条件与重置构建器必须按预期工作
    /// </summary>
    [Fact]
    public void FromRequest_And_Reset_Behave()
    {
        var source = new PageRequestDtoBase().WithFilter("Age", 30).WithSort("Name");
        var request = QueryBuilder.FromRequest(source).Build();

        Assert.Single(request.Conditions.Filters);
        Assert.Single(request.Conditions.Sorts);

        var reset = QueryBuilder.Create().WhereEqual("Age", 30).Reset().Build();
        Assert.Empty(reset.Conditions.Filters);
        Assert.Equal(1, reset.Page.PageIndex);
        Assert.Equal(20, reset.Page.PageSize);
    }
}
