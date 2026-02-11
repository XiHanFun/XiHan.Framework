#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryConditions
// Guid:e8f5d7c9-4a3b-2c1d-9e8f-7a6b5c4d3e2f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/10 07:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Models;

/// <summary>
/// 查询条件（只描述"查什么"，永远可以安全进入 Expression）
/// </summary>
public sealed class QueryConditions
{
    /// <summary>
    /// 多字段筛选
    /// </summary>
    public List<QueryFilter> Filters { get; set; } = [];

    /// <summary>
    /// 多字段排序
    /// </summary>
    public List<QuerySort> Sorts { get; set; } = [];

    /// <summary>
    /// 关键字搜索（关键字 + 搜索字段）
    /// </summary>
    public QueryKeyword? Keyword { get; set; }

    /// <summary>
    /// 添加过滤条件
    /// </summary>
    public QueryConditions AddFilter(string field, object? value, QueryOperator @operator = QueryOperator.Equal)
    {
        Filters.Add(new QueryFilter(field, value, @operator));
        return this;
    }

    /// <summary>
    /// 添加过滤条件
    /// </summary>
    public QueryConditions AddFilter(QueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        Filters.Add(filter);
        return this;
    }

    /// <summary>
    /// 批量添加过滤条件
    /// </summary>
    public QueryConditions AddFilters(IEnumerable<QueryFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        Filters.AddRange(filters);
        return this;
    }

    /// <summary>
    /// 添加排序条件
    /// </summary>
    public QueryConditions AddSort(string field, SortDirection direction = SortDirection.Ascending, int priority = 0)
    {
        Sorts.Add(new QuerySort(field, direction, priority));
        return this;
    }

    /// <summary>
    /// 添加排序条件
    /// </summary>
    public QueryConditions AddSort(QuerySort sort)
    {
        ArgumentNullException.ThrowIfNull(sort);
        Sorts.Add(sort);
        return this;
    }

    /// <summary>
    /// 批量添加排序条件
    /// </summary>
    public QueryConditions AddSorts(IEnumerable<QuerySort> sorts)
    {
        ArgumentNullException.ThrowIfNull(sorts);
        Sorts.AddRange(sorts);
        return this;
    }

    /// <summary>
    /// 设置关键字搜索
    /// </summary>
    public QueryConditions SetKeyword(string? keyword, params string[] fields)
    {
        Keyword ??= new QueryKeyword();
        Keyword.Value = keyword;
        if (fields.Length > 0)
        {
            Keyword.Fields = [.. fields];
        }
        return this;
    }

    /// <summary>
    /// 清空所有条件
    /// </summary>
    public QueryConditions Clear()
    {
        Filters.Clear();
        Sorts.Clear();
        Keyword = null;
        return this;
    }

    /// <summary>
    /// 移除指定字段的过滤条件
    /// </summary>
    public QueryConditions RemoveFilter(string field)
    {
        Filters.RemoveAll(f => f.Field.Equals(field, StringComparison.OrdinalIgnoreCase));
        return this;
    }

    /// <summary>
    /// 移除指定字段的排序条件
    /// </summary>
    public QueryConditions RemoveSort(string field)
    {
        Sorts.RemoveAll(s => s.Field.Equals(field, StringComparison.OrdinalIgnoreCase));
        return this;
    }

    /// <summary>
    /// 验证请求是否有效
    /// </summary>
    public bool IsValid()
    {
        // 验证所有过滤条件
        if (Filters.Any(f => !f.IsValid()))
        {
            return false;
        }

        // 验证所有排序条件
        if (Sorts.Any(s => !s.IsValid()))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 克隆当前查询条件
    /// </summary>
    public QueryConditions Clone()
    {
        var cloned = new QueryConditions
        {
            Filters = [.. Filters],
            Sorts = [.. Sorts],
            Keyword = Keyword is not null
                ? new QueryKeyword { Value = Keyword.Value, Fields = [.. Keyword.Fields] }
                : null
        };
        return cloned;
    }
}
