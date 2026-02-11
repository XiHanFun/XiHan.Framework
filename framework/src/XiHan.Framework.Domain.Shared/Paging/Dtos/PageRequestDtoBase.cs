#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageRequestDtoBase
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
/// 分页请求基类
/// </summary>
public class PageRequestDtoBase
{
    /// <summary>
    /// 查询条件（描述"查什么"）
    /// </summary>
    public QueryConditions Conditions { get; set; } = new();

    /// <summary>
    /// 查询行为控制（影响查询管道行为）
    /// </summary>
    public QueryBehavior Behavior { get; set; } = new();

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
    /// 禁用分页（返回所有数据）
    /// </summary>
    public PageRequestDtoBase WithoutPaging()
    {
        Behavior.DisablePaging = true;
        return this;
    }

    /// <summary>
    /// 创建默认分页请求（第1页，10条）
    /// </summary>
    public static PageRequestDtoBase Default() => new();
}
