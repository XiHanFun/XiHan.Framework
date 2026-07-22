// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging.Dtos;

/// <summary>
/// 分页请求基类
/// </summary>
public class PageRequestDtoBase
{
    /// <summary>
    /// 查询条件（描述"查什么"）
    /// </summary>
    public QueryConditions Conditions { get; set; } = new();


    /// <summary>
    /// 分页参数
    /// </summary>
    public PageRequestMetadata Page { get; set; } = new();

    /// <summary>
    /// 快速添加过滤条件（链式调用）
    /// </summary>
    public PageRequestDtoBase WithFilter(string field, object? value, QueryOperator @operator = QueryOperator.Equal)
    {
        Conditions.AddFilter(field, value, @operator);
        return this;
    }

    /// <summary>
    /// 快速添加排序条件（链式调用）
    /// </summary>
    public PageRequestDtoBase WithSort(string field, SortDirection direction = SortDirection.Ascending)
    {
        Conditions.AddSort(field, direction);
        return this;
    }

    /// <summary>
    /// 快速设置关键字搜索（链式调用）
    /// </summary>
    public PageRequestDtoBase WithKeyword(string? keyword, params string[] fields)
    {
        Conditions.SetKeyword(keyword, fields);
        return this;
    }

    /// <summary>
    /// 设置页码和每页大小（链式调用）
    /// </summary>
    public PageRequestDtoBase WithPage(int pageIndex, int pageSize)
    {
        Page.PageIndex = pageIndex;
        Page.PageSize = pageSize;
        return this;
    }

    /// <summary>
    /// 创建默认分页请求（第1页，10条）
    /// </summary>
    public static PageRequestDtoBase Default() => new();
}
