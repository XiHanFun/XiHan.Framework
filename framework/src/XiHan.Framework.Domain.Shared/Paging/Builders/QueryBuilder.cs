#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryBuilder
// Guid:0c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 15:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging.Builders;

/// <summary>
/// 查询构建器
/// </summary>
public class QueryBuilder
{
    private readonly List<QueryFilter> _filters = [];
    private readonly List<QuerySort> _sorts = [];
    private QueryKeyword? _keyword;
    private int _pageIndex = PageRequestMetadata.DefaultPageIndex;
    private int _pageSize = PageRequestMetadata.DefaultPageSize;

    /// <summary>
    /// 从已有请求创建构建器
    /// </summary>
    public static QueryBuilder FromRequest(PageRequestDtoBase request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var builder = new QueryBuilder();
        builder._filters.AddRange(request.Conditions?.Filters ?? []);
        builder._sorts.AddRange(request.Conditions?.Sorts ?? []);
        builder._keyword = request.Conditions?.Keyword?.Clone();
        builder._pageIndex = request.Page.PageIndex;
        builder._pageSize = request.Page.PageSize;

        return builder;
    }

    /// <summary>
    /// 创建新的构建器
    /// </summary>
    public static QueryBuilder Create()
    {
        return new QueryBuilder();
    }

    /// <summary>
    /// 添加过滤条件
    /// </summary>
    public QueryBuilder AddFilter(string field, object? value, QueryOperator @operator = QueryOperator.Equal)
    {
        _filters.Add(new QueryFilter(field, value, @operator));
        return this;
    }

    /// <summary>
    /// 添加过滤条件
    /// </summary>
    public QueryBuilder AddFilter(QueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _filters.Add(filter);
        return this;
    }

    /// <summary>
    /// 添加等于条件
    /// </summary>
    public QueryBuilder WhereEqual(string field, object value)
    {
        return AddFilter(QueryFilter.Equal(field, value));
    }

    /// <summary>
    /// 添加不等于条件
    /// </summary>
    public QueryBuilder WhereNotEqual(string field, object value)
    {
        return AddFilter(QueryFilter.NotEqual(field, value));
    }

    /// <summary>
    /// 添加大于条件
    /// </summary>
    public QueryBuilder WhereGreaterThan(string field, object value)
    {
        return AddFilter(QueryFilter.GreaterThan(field, value));
    }

    /// <summary>
    /// 添加小于条件
    /// </summary>
    public QueryBuilder WhereLessThan(string field, object value)
    {
        return AddFilter(QueryFilter.LessThan(field, value));
    }

    /// <summary>
    /// 添加包含条件
    /// </summary>
    public QueryBuilder WhereContains(string field, string value)
    {
        return AddFilter(QueryFilter.Contains(field, value));
    }

    /// <summary>
    /// 添加 In 条件
    /// </summary>
    public QueryBuilder WhereIn(string field, params object[] values)
    {
        return AddFilter(QueryFilter.In(field, values));
    }

    /// <summary>
    /// 添加区间条件
    /// </summary>
    public QueryBuilder WhereBetween(string field, object start, object end)
    {
        return AddFilter(QueryFilter.Between(field, start, end));
    }

    /// <summary>
    /// 添加为空条件
    /// </summary>
    public QueryBuilder WhereNull(string field)
    {
        return AddFilter(QueryFilter.IsNull(field));
    }

    /// <summary>
    /// 添加不为空条件
    /// </summary>
    public QueryBuilder WhereNotNull(string field)
    {
        return AddFilter(QueryFilter.IsNotNull(field));
    }

    /// <summary>
    /// 添加排序条件
    /// </summary>
    public QueryBuilder AddSort(string field, SortDirection direction = SortDirection.Ascending, int priority = 0)
    {
        _sorts.Add(new QuerySort(field, direction, priority));
        return this;
    }

    /// <summary>
    /// 添加升序排序
    /// </summary>
    public QueryBuilder OrderBy(string field, int priority = 0)
    {
        return AddSort(field, SortDirection.Ascending, priority);
    }

    /// <summary>
    /// 添加降序排序
    /// </summary>
    public QueryBuilder OrderByDescending(string field, int priority = 0)
    {
        return AddSort(field, SortDirection.Descending, priority);
    }

    /// <summary>
    /// 设置关键字
    /// </summary>
    public QueryBuilder SetKeyword(string? keyword)
    {
        _keyword ??= new QueryKeyword();
        _keyword.Value = keyword;
        return this;
    }

    /// <summary>
    /// 添加关键字搜索字段
    /// </summary>
    public QueryBuilder AddKeywordField(params string[] fields)
    {
        _keyword ??= new QueryKeyword();
        _keyword.AddFields(fields);
        return this;
    }

    /// <summary>
    /// 设置关键字搜索（关键字 + 字段）
    /// </summary>
    public QueryBuilder SetKeywordSearch(string? keyword, params string[] fields)
    {
        _keyword = new QueryKeyword
        {
            Value = keyword,
            Fields = [.. fields]
        };
        return this;
    }

    /// <summary>
    /// 设置页码
    /// </summary>
    public QueryBuilder SetPageIndex(int pageIndex)
    {
        _pageIndex = pageIndex < PageRequestMetadata.DefaultPageIndex
            ? PageRequestMetadata.DefaultPageIndex
            : pageIndex;
        return this;
    }

    /// <summary>
    /// 设置每页大小
    /// </summary>
    public QueryBuilder SetPageSize(int pageSize)
    {
        _pageSize = pageSize switch
        {
            < PageRequestMetadata.MinPageSize => PageRequestMetadata.DefaultPageSize,
            > PageRequestMetadata.MaxPageSize => PageRequestMetadata.MaxPageSize,
            _ => pageSize
        };
        return this;
    }

    /// <summary>
    /// 设置分页参数
    /// </summary>
    public QueryBuilder SetPaging(int pageIndex, int pageSize)
    {
        SetPageIndex(pageIndex);
        SetPageSize(pageSize);
        return this;
    }

    /// <summary>
    /// 清除所有过滤条件
    /// </summary>
    public QueryBuilder ClearFilters()
    {
        _filters.Clear();
        return this;
    }

    /// <summary>
    /// 清除所有排序条件
    /// </summary>
    public QueryBuilder ClearSorts()
    {
        _sorts.Clear();
        return this;
    }

    /// <summary>
    /// 清除关键字搜索
    /// </summary>
    public QueryBuilder ClearKeyword()
    {
        _keyword = null;
        return this;
    }

    /// <summary>
    /// 重置所有条件
    /// </summary>
    public QueryBuilder Reset()
    {
        _filters.Clear();
        _sorts.Clear();
        _keyword = null;
        _pageIndex = PageRequestMetadata.DefaultPageIndex;
        _pageSize = PageRequestMetadata.DefaultPageSize;
        return this;
    }

    /// <summary>
    /// 构建为 BasePageRequestDto
    /// </summary>
    public PageRequestDtoBase Build()
    {
        return new PageRequestDtoBase
        {
            Page = new PageRequestMetadata(_pageIndex, _pageSize),
            Conditions = new QueryConditions
            {
                Filters = [.. _filters],
                Sorts = [.. _sorts],
                Keyword = _keyword?.Clone()
            }
        };
    }
}
