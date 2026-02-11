#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageConverter
// Guid:3c4d5e6f-7a8b-9c0d-1e2f-3a4b5c6d7e8f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 15:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging.Converters;

/// <summary>
/// 分页转换器
/// </summary>
public static class PageConverter
{
    /// <summary>
    /// BasePageRequestDto 转 PageRequestMetadata
    /// </summary>
    public static PageRequestMetadata ToMetadata(this PageRequestDtoBase request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new PageRequestMetadata(request.Page.PageIndex, request.Page.PageSize);
    }

    /// <summary>
    /// PageRequestMetadata 转 BasePageRequestDto
    /// </summary>
    public static PageRequestDtoBase ToDto(this PageRequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new PageRequestDtoBase
        {
            Page = new PageRequestMetadata(metadata.PageIndex, metadata.PageSize)
        };
    }

    /// <summary>
    /// 将分页结果的数据类型转换
    /// </summary>
    public static PageResultDtoBase<TTarget> ConvertItems<TSource, TTarget>(
        this PageResultDtoBase<TSource> source,
        Func<TSource, TTarget> converter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(converter);

        var convertedItems = source.Items.Select(converter).ToList();
        return new PageResultDtoBase<TTarget>(convertedItems, source.Page);
    }

    /// <summary>
    /// 异步转换分页结果的数据类型
    /// </summary>
    public static async Task<PageResultDtoBase<TTarget>> ConvertItemsAsync<TSource, TTarget>(
        this PageResultDtoBase<TSource> source,
        Func<TSource, Task<TTarget>> converter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(converter);

        var tasks = source.Items.Select(converter);
        var convertedItems = await Task.WhenAll(tasks);

        return new PageResultDtoBase<TTarget>(convertedItems, source.Page);
    }

    /// <summary>
    /// 克隆分页请求
    /// </summary>
    public static PageRequestDtoBase Clone(this PageRequestDtoBase request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cond = request.Conditions;
        return new PageRequestDtoBase
        {
            Page = new PageRequestMetadata(request.Page.PageIndex, request.Page.PageSize),
            Conditions = new QueryConditions
            {
                Filters = [.. cond.Filters],
                Sorts = [.. cond.Sorts],
                Keyword = cond.Keyword != null
                    ? new QueryKeyword { Value = cond.Keyword.Value, Fields = [.. cond.Keyword.Fields] }
                    : null
            },
            Behavior = new QueryBehavior
            {
                DisablePaging = request.Behavior.DisablePaging,
                DisableDefaultSort = request.Behavior.DisableDefaultSort,
                IgnoreTenant = request.Behavior.IgnoreTenant,
                IgnoreSoftDelete = request.Behavior.IgnoreSoftDelete
            }
        };
    }

    /// <summary>
    /// 合并两个分页请求（第二个请求覆盖第一个）
    /// </summary>
    public static PageRequestDtoBase Merge(this PageRequestDtoBase first, PageRequestDtoBase second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var merged = first.Clone();

        // 合并分页参数
        merged.Page = new PageRequestMetadata(second.Page.PageIndex, second.Page.PageSize);

        // 合并查询行为
        merged.Behavior = new QueryBehavior
        {
            DisablePaging = second.Behavior.DisablePaging,
            DisableDefaultSort = second.Behavior.DisableDefaultSort,
            IgnoreTenant = second.Behavior.IgnoreTenant,
            IgnoreSoftDelete = second.Behavior.IgnoreSoftDelete
        };

        // 合并过滤条件
        foreach (var filter in second.Conditions.Filters)
        {
            var existing = merged.Conditions.Filters.FirstOrDefault(f => f.Field == filter.Field);
            if (existing != null)
            {
                merged.Conditions.Filters.Remove(existing);
            }

            merged.Conditions.Filters.Add(filter);
        }

        // 合并排序条件
        foreach (var sort in second.Conditions.Sorts)
        {
            var existing = merged.Conditions.Sorts.FirstOrDefault(s => s.Field == sort.Field);
            if (existing != null)
            {
                merged.Conditions.Sorts.Remove(existing);
            }

            merged.Conditions.Sorts.Add(sort);
        }

        // 合并关键字搜索
        if (!string.IsNullOrWhiteSpace(second.Conditions.Keyword?.Value))
        {
            merged.Conditions.Keyword = new QueryKeyword
            {
                Value = second.Conditions.Keyword.Value,
                Fields = [.. second.Conditions.Keyword.Fields]
            };
        }

        return merged;
    }

    /// <summary>
    /// 创建空的分页结果
    /// </summary>
    public static PageResultDtoBase<T> CreateEmptyResult<T>(this PageRequestDtoBase request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PageResultDtoBase<T>.Empty(request.Page.PageIndex, request.Page.PageSize);
    }

    /// <summary>
    /// 从分页元数据创建空结果
    /// </summary>
    public static PageResultDtoBase<T> CreateEmptyResult<T>(this PageRequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return PageResultDtoBase<T>.Empty(metadata.PageIndex, metadata.PageSize);
    }
}
