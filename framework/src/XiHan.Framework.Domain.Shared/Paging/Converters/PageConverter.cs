#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageConverter
// Guid:3c4d5e6f-7a8b-9c0d-1e2f-3a4b5c6d7e8f
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/2 15:00:00
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
    public static PageRequestMetadata ToMetadata(this BasePageRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new PageRequestMetadata(request.PageIndex, request.PageSize);
    }

    /// <summary>
    /// PageRequestMetadata 转 BasePageRequestDto
    /// </summary>
    public static BasePageRequestDto ToDto(this PageRequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new BasePageRequestDto(metadata.PageIndex, metadata.PageSize);
    }

    /// <summary>
    /// 将分页结果的数据类型转换
    /// </summary>
    public static BasePageResultDto<TTarget> ConvertItems<TSource, TTarget>(
        this BasePageResultDto<TSource> source,
        Func<TSource, TTarget> converter)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(converter);

        var convertedItems = source.Items.Select(converter).ToList();
        return new BasePageResultDto<TTarget>(convertedItems, source.PageResultMetadata);
    }

    /// <summary>
    /// 异步转换分页结果的数据类型
    /// </summary>
    public static async Task<BasePageResultDto<TTarget>> ConvertItemsAsync<TSource, TTarget>(
        this BasePageResultDto<TSource> source,
        Func<TSource, Task<TTarget>> converter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(converter);

        var tasks = source.Items.Select(converter);
        var convertedItems = await Task.WhenAll(tasks);

        return new BasePageResultDto<TTarget>(convertedItems, source.PageResultMetadata);
    }

    /// <summary>
    /// 克隆分页请求
    /// </summary>
    public static BasePageRequestDto Clone(this BasePageRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new BasePageRequestDto(request.PageIndex, request.PageSize)
        {
            Filters = [.. request.Filters],
            Sorts = [.. request.Sorts],
            KeywordFields = [.. request.KeywordFields],
            Keyword = request.Keyword,
            DisablePaging = request.DisablePaging
        };
    }

    /// <summary>
    /// 合并两个分页请求（第二个请求覆盖第一个）
    /// </summary>
    public static BasePageRequestDto Merge(this BasePageRequestDto first, BasePageRequestDto second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var merged = first.Clone();

        // 合并分页参数
        merged.PageIndex = second.PageIndex;
        merged.PageSize = second.PageSize;
        merged.DisablePaging = second.DisablePaging;

        // 合并过滤条件（去重）
        foreach (var filter in second.Filters)
        {
            var existing = merged.Filters.FirstOrDefault(f => f.Field == filter.Field);
            if (existing != null)
            {
                merged.Filters.Remove(existing);
            }

            merged.Filters.Add(filter);
        }

        // 合并排序条件（去重）
        foreach (var sort in second.Sorts)
        {
            var existing = merged.Sorts.FirstOrDefault(s => s.Field == sort.Field);
            if (existing != null)
            {
                merged.Sorts.Remove(existing);
            }

            merged.Sorts.Add(sort);
        }

        // 合并关键字搜索
        if (!string.IsNullOrWhiteSpace(second.Keyword))
        {
            merged.Keyword = second.Keyword;
            merged.KeywordFields = [.. second.KeywordFields];
        }

        return merged;
    }

    /// <summary>
    /// 创建空的分页结果
    /// </summary>
    public static BasePageResultDto<T> CreateEmptyResult<T>(this BasePageRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BasePageResultDto<T>.Empty(request.PageIndex, request.PageSize);
    }

    /// <summary>
    /// 从分页元数据创建空结果
    /// </summary>
    public static BasePageResultDto<T> CreateEmptyResult<T>(this PageRequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return BasePageResultDto<T>.Empty(metadata.PageIndex, metadata.PageSize);
    }
}
