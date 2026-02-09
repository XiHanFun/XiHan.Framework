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
    public static PageRequestMetadata ToMetadata(this PageRequestDtoBase request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new PageRequestMetadata(request.PageRequestMetadata.PageIndex, request.PageRequestMetadata.PageSize);
    }

    /// <summary>
    /// PageRequestMetadata 转 BasePageRequestDto
    /// </summary>
    public static PageRequestDtoBase ToDto(this PageRequestMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new PageRequestDtoBase { PageRequestMetadata = new PageRequestMetadata(metadata.PageIndex, metadata.PageSize) };
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
        return new PageResultDtoBase<TTarget>(convertedItems, source.PageResultMetadata);
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

        return new PageResultDtoBase<TTarget>(convertedItems, source.PageResultMetadata);
    }

    /// <summary>
    /// 克隆分页请求
    /// </summary>
    public static PageRequestDtoBase Clone(this PageRequestDtoBase request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var q = request.QueryMetadata;
        return new PageRequestDtoBase
        {
            PageRequestMetadata = new PageRequestMetadata(request.PageRequestMetadata.PageIndex, request.PageRequestMetadata.PageSize),
            QueryMetadata = new QueryMetadata
            {
                Filters = [.. q?.Filters ?? []],
                Sorts = [.. q?.Sorts ?? []],
                KeywordFields = [.. q?.KeywordFields ?? []],
                Keyword = q?.Keyword,
                DisablePaging = q?.DisablePaging ?? false
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
        var mq = merged.QueryMetadata!;
        var sq = second.QueryMetadata;

        merged.PageRequestMetadata = new PageRequestMetadata(second.PageRequestMetadata.PageIndex, second.PageRequestMetadata.PageSize);
        mq.DisablePaging = sq?.DisablePaging ?? false;

        foreach (var filter in sq?.Filters ?? [])
        {
            var existing = mq.Filters.FirstOrDefault(f => f.Field == filter.Field);
            if (existing != null)
            {
                mq.Filters.Remove(existing);
            }

            mq.Filters.Add(filter);
        }

        foreach (var sort in sq?.Sorts ?? [])
        {
            var existing = mq.Sorts.FirstOrDefault(s => s.Field == sort.Field);
            if (existing != null)
            {
                mq.Sorts.Remove(existing);
            }

            mq.Sorts.Add(sort);
        }

        if (!string.IsNullOrWhiteSpace(sq?.Keyword))
        {
            mq.Keyword = sq.Keyword;
            mq.KeywordFields = [.. sq.KeywordFields];
        }

        return merged;
    }

    /// <summary>
    /// 创建空的分页结果
    /// </summary>
    public static PageResultDtoBase<T> CreateEmptyResult<T>(this PageRequestDtoBase request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PageResultDtoBase<T>.Empty(request.PageRequestMetadata.PageIndex, request.PageRequestMetadata.PageSize);
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
