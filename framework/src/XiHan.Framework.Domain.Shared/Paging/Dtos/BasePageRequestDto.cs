#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BasePageRequestDto
// Guid:aec9055a-15c5-48d1-a835-05a28e11cdf3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 07:11:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging.Dtos;

/// <summary>
/// 通用分页请求
/// </summary>
public class BasePageRequestDto
{
    private int _pageIndex = PageRequestMetadata.DefaultPageIndex;

    private int _pageSize = PageRequestMetadata.DefaultPageSize;

    /// <summary>
    /// 构造函数
    /// </summary>
    public BasePageRequestDto()
    {
        PageIndex = PageRequestMetadata.DefaultPageIndex;
        PageSize = PageRequestMetadata.DefaultPageSize;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pageIndex">页码</param>
    /// <param name="pageSize">每页大小</param>
    public BasePageRequestDto(int pageIndex, int pageSize)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
    }

    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value < PageRequestMetadata.DefaultPageIndex ? PageRequestMetadata.DefaultPageIndex : value;
    }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < PageRequestMetadata.MinPageSize => PageRequestMetadata.DefaultPageSize,
            > PageRequestMetadata.MaxPageSize => PageRequestMetadata.MaxPageSize,
            _ => value
        };
    }

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
    public bool DisablePaging { get; set; }

    /// <summary>
    /// 转换为 PageRequestMetadata
    /// </summary>
    public PageRequestMetadata ToPageRequestMetadata() => new(PageIndex, PageSize);

    /// <summary>
    /// 添加过滤条件
    /// </summary>
    public BasePageRequestDto AddFilter(string field, object? value, QueryOperator @operator = QueryOperator.Equal)
    {
        Filters.Add(new QueryFilter(field, value, @operator));
        return this;
    }

    /// <summary>
    /// 添加过滤条件
    /// </summary>
    public BasePageRequestDto AddFilter(QueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        Filters.Add(filter);
        return this;
    }

    /// <summary>
    /// 添加排序条件
    /// </summary>
    public BasePageRequestDto AddSort(string field, SortDirection direction = SortDirection.Ascending, int priority = 0)
    {
        Sorts.Add(new QuerySort(field, direction, priority));
        return this;
    }

    /// <summary>
    /// 添加排序条件
    /// </summary>
    public BasePageRequestDto AddSort(QuerySort sort)
    {
        ArgumentNullException.ThrowIfNull(sort);
        Sorts.Add(sort);
        return this;
    }

    /// <summary>
    /// 设置关键字搜索
    /// </summary>
    public BasePageRequestDto SetKeyword(string? keyword, params string[] fields)
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
