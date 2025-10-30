#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageExtensions
// Guid:d95f1fd8-ba4d-4932-b63d-23b6a7d6382b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/3 2:09:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Paging;
using XiHan.Framework.Domain.Paging.Dtos;
using XiHan.Framework.Domain.Paging.Handlers;

namespace XiHan.Framework.Domain.Paging;

/// <summary>
/// 分页扩展方法
/// </summary>
public static class PageExtensions
{
    #region IQueryable 扩展

    /// <summary>
    /// 应用过滤条件
    /// </summary>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, bool condition, SelectCondition filter)
    {
        return condition ? source.Where(filter) : source;
    }

    /// <summary>
    /// 应用过滤条件
    /// </summary>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, SelectCondition filter)
    {
        return CollectionPropertySelector<T>.Where(source, filter);
    }

    /// <summary>
    /// 应用多个过滤条件
    /// </summary>
    public static IQueryable<T> Where<T>(this IQueryable<T> source, IEnumerable<SelectCondition>? filters)
    {
        if (filters == null)
        {
            return source;
        }

        return CollectionPropertySelector<T>.Where(source, filters);
    }

    /// <summary>
    /// 应用排序条件
    /// </summary>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, SortCondition sort)
    {
        return CollectionPropertySortor<T>.OrderBy(source, sort);
    }

    /// <summary>
    /// 应用多个排序条件
    /// </summary>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, IEnumerable<SortCondition>? sorts)
    {
        if (sorts == null || !sorts.Any())
        {
            return source.OrderBy(x => 1); // 默认排序
        }

        return CollectionPropertySortor<T>.OrderBy(source, sorts);
    }

    /// <summary>
    /// 应用分页查询
    /// </summary>
    public static async Task<PageResponse<T>> ToPageResponseAsync<T>(this IQueryable<T> source, PageQuery query, CancellationToken cancellationToken = default)
    {
        // 应用过滤
        if (query.Filters != null && query.Filters.Count != 0)
        {
            source = source.Where(query.Filters);
        }

        // 应用排序
        if (query.Sorts != null && query.Sorts.Count != 0)
        {
            source = source.OrderBy(query.Sorts);
        }

        // 禁用分页时返回所有数据
        if (query.DisablePaging)
        {
            var allItems = await Task.Run(() => source.ToList(), cancellationToken);
            return new PageResponse<T>(allItems, 1, allItems.Count, allItems.Count);
        }

        // 计算总数
        var totalCount = await Task.Run(() => source.Count(), cancellationToken);

        // 应用分页
        var items = await Task.Run(() =>
            source.Skip(query.ToPageInfo().Skip).Take(query.ToPageInfo().Take).ToList(),
            cancellationToken);

        return new PageResponse<T>(items, query.PageIndex, query.PageSize, totalCount);
    }

    /// <summary>
    /// 应用分页查询（同步版本）
    /// </summary>
    public static PageResponse<T> ToPageResponse<T>(this IQueryable<T> source, PageQuery query)
    {
        // 应用过滤
        if (query.Filters != null && query.Filters.Count != 0)
        {
            source = source.Where(query.Filters);
        }

        // 应用排序
        if (query.Sorts != null && query.Sorts.Count != 0)
        {
            source = source.OrderBy(query.Sorts);
        }

        // 禁用分页时返回所有数据
        if (query.DisablePaging)
        {
            var allItems = source.ToList();
            return new PageResponse<T>(allItems, 1, allItems.Count, allItems.Count);
        }

        // 计算总数
        var totalCount = source.Count();

        // 应用分页
        var items = source.Skip(query.ToPageInfo().Skip).Take(query.ToPageInfo().Take).ToList();

        return new PageResponse<T>(items, query.PageIndex, query.PageSize, totalCount);
    }

    /// <summary>
    /// 简单分页
    /// </summary>
    public static PageResponse<T> ToPageResponse<T>(this IQueryable<T> source, int pageIndex, int pageSize)
    {
        var totalCount = source.Count();
        var skip = (pageIndex - 1) * pageSize;
        var items = source.Skip(skip).Take(pageSize).ToList();
        return new PageResponse<T>(items, pageIndex, pageSize, totalCount);
    }

    #endregion

    #region IEnumerable 扩展

    /// <summary>
    /// 应用过滤条件
    /// </summary>
    public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, SelectCondition filter)
    {
        return condition ? source.Where(filter) : source;
    }

    /// <summary>
    /// 应用过滤条件
    /// </summary>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, SelectCondition filter)
    {
        return CollectionPropertySelector<T>.Where(source, filter);
    }

    /// <summary>
    /// 应用多个过滤条件
    /// </summary>
    public static IEnumerable<T> Where<T>(this IEnumerable<T> source, IEnumerable<SelectCondition>? filters)
    {
        if (filters == null)
        {
            return source;
        }

        return CollectionPropertySelector<T>.Where(source, filters);
    }

    /// <summary>
    /// 应用排序条件
    /// </summary>
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, SortCondition sort)
    {
        return CollectionPropertySortor<T>.OrderBy(source, sort);
    }

    /// <summary>
    /// 应用多个排序条件
    /// </summary>
    public static IOrderedEnumerable<T> OrderBy<T>(this IEnumerable<T> source, IEnumerable<SortCondition>? sorts)
    {
        if (sorts == null || !sorts.Any())
        {
            return source.OrderBy(x => 1); // 默认排序
        }

        return CollectionPropertySortor<T>.OrderBy(source, sorts);
    }

    /// <summary>
    /// 应用分页查询
    /// </summary>
    public static PageResponse<T> ToPageResponse<T>(this IEnumerable<T> source, PageQuery query)
    {
        var list = source.ToList();

        // 应用过滤
        IEnumerable<T> filtered = list;
        if (query.Filters != null && query.Filters.Count != 0)
        {
            filtered = filtered.Where(query.Filters);
        }

        // 应用排序
        if (query.Sorts != null && query.Sorts.Count != 0)
        {
            filtered = filtered.OrderBy(query.Sorts);
        }

        var filteredList = filtered.ToList();

        // 禁用分页时返回所有数据
        if (query.DisablePaging)
        {
            return new PageResponse<T>(filteredList, 1, filteredList.Count, filteredList.Count);
        }

        // 计算总数
        var totalCount = filteredList.Count;

        // 应用分页
        var items = filteredList.Skip(query.ToPageInfo().Skip).Take(query.ToPageInfo().Take).ToList();

        return new PageResponse<T>(items, query.PageIndex, query.PageSize, totalCount);
    }

    /// <summary>
    /// 简单分页
    /// </summary>
    public static PageResponse<T> ToPageResponse<T>(this IEnumerable<T> source, int pageIndex, int pageSize)
    {
        var list = source.ToList();
        var totalCount = list.Count;
        var skip = (pageIndex - 1) * pageSize;
        var items = list.Skip(skip).Take(pageSize).ToList();
        return new PageResponse<T>(items, pageIndex, pageSize, totalCount);
    }

    #endregion

    #region List 扩展

    /// <summary>
    /// 转换为分页响应
    /// </summary>
    public static PageResponse<T> ToPageResponse<T>(this List<T> items, int pageIndex, int pageSize, int totalCount)
    {
        return new PageResponse<T>(items, pageIndex, pageSize, totalCount);
    }

    /// <summary>
    /// 转换为分页响应
    /// </summary>
    public static PageResponse<T> ToPageResponse<T>(this List<T> items, PageData pageData)
    {
        return new PageResponse<T>(items, pageData);
    }

    #endregion
}
