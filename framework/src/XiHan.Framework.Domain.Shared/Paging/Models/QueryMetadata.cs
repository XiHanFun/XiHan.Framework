#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryMetadata
// Guid:cdf5f808-d324-485c-b3d3-6a30be802dce
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/10 5:59:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Models;

/// <summary>
/// 查询元数据
/// </summary>
public class QueryMetadata
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
    /// 允许关键字搜索的字段
    /// </summary>
    public List<string> KeywordFields { get; set; } = [];

    /// <summary>
    /// 搜索关键字（可选）
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// 是否禁用分页（返回所有数据）
    /// </summary>
    public bool DisablePaging { get; set; } = false;

    /// <summary>
    /// 添加过滤条件
    /// </summary>
    public QueryMetadata AddFilter(string field, object? value, QueryOperator @operator = QueryOperator.Equal)
    {
        Filters.Add(new QueryFilter(field, value, @operator));
        return this;
    }

    /// <summary>
    /// 添加过滤条件
    /// </summary>
    public QueryMetadata AddFilter(QueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        Filters.Add(filter);
        return this;
    }

    /// <summary>
    /// 添加排序条件
    /// </summary>
    public QueryMetadata AddSort(string field, SortDirection direction = SortDirection.Ascending, int priority = 0)
    {
        Sorts.Add(new QuerySort(field, direction, priority));
        return this;
    }

    /// <summary>
    /// 添加排序条件
    /// </summary>
    public QueryMetadata AddSort(QuerySort sort)
    {
        ArgumentNullException.ThrowIfNull(sort);
        Sorts.Add(sort);
        return this;
    }

    /// <summary>
    /// 设置关键字搜索
    /// </summary>
    public QueryMetadata SetKeyword(string? keyword, params string[] fields)
    {
        Keyword = keyword;
        if (fields.Length > 0)
        {
            KeywordFields = [.. fields];
        }
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
}
