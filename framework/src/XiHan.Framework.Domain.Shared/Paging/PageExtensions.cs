#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageExtensions
// Guid:d95f1fd8-ba4d-4932-b63d-23b6a7d6382b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/03 02:09:58
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging;

/// <summary>
/// 分页扩展方法
/// </summary>
public static class PageExtensions
{
    #region IQueryable 扩展

    /// <summary>
    /// 应用分页
    /// </summary>
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, PageRequestMetadata pageRequest)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(pageRequest);

        return query.Skip(pageRequest.Skip).Take(pageRequest.Take);
    }

    /// <summary>
    /// 应用分页
    /// </summary>
    public static IQueryable<T> ApplyPaging<T>(this IQueryable<T> query, PageRequestDtoBase pageRequest)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(pageRequest);

        if (pageRequest.Behavior.DisablePaging)
        {
            return query;
        }

        var meta = pageRequest.Page;
        return query.Skip(meta.Skip).Take(meta.Take);
    }

    /// <summary>
    /// 应用分页并返回分页结果
    /// </summary>
    public static async Task<PageResultDtoBase<T>> ToPageResultAsync<T>(
        this IQueryable<T> query,
        PageRequestDtoBase pageRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(pageRequest);

        var totalCount = query.Count();
        var meta = pageRequest.Page;

        if (totalCount == 0)
        {
            return PageResultDtoBase<T>.Empty(meta.PageIndex, meta.PageSize);
        }

        var items = pageRequest.Behavior.DisablePaging
            ? query.ToList()
            : [.. query.Skip(meta.Skip).Take(meta.Take)];

        return PageResultDtoBase<T>.Create(items, pageRequest, totalCount);
    }

    /// <summary>
    /// 应用分页并返回分页结果（同步版本）
    /// </summary>
    public static PageResultDtoBase<T> ToPageResult<T>(
        this IQueryable<T> query,
        PageRequestDtoBase pageRequest)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(pageRequest);

        var totalCount = query.Count();
        var meta = pageRequest.Page;

        if (totalCount == 0)
        {
            return PageResultDtoBase<T>.Empty(meta.PageIndex, meta.PageSize);
        }

        var items = pageRequest.Behavior.DisablePaging
            ? query.ToList()
            : [.. query.Skip(meta.Skip).Take(meta.Take)];

        return PageResultDtoBase<T>.Create(items, pageRequest, totalCount);
    }

    #endregion

    #region IEnumerable 扩展

    /// <summary>
    /// 应用分页
    /// </summary>
    public static IEnumerable<T> ApplyPaging<T>(this IEnumerable<T> source, PageRequestMetadata pageRequest)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pageRequest);

        return source.Skip(pageRequest.Skip).Take(pageRequest.Take);
    }

    /// <summary>
    /// 应用分页
    /// </summary>
    public static IEnumerable<T> ApplyPaging<T>(this IEnumerable<T> source, PageRequestDtoBase pageRequest)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pageRequest);

        if (pageRequest.Behavior.DisablePaging)
        {
            return source;
        }

        var meta = pageRequest.Page;
        return source.Skip(meta.Skip).Take(meta.Take);
    }

    /// <summary>
    /// 转换为分页结果
    /// </summary>
    public static PageResultDtoBase<T> ToPageResult<T>(
        this IEnumerable<T> source,
        PageRequestDtoBase pageRequest)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(pageRequest);

        var list = source as IList<T> ?? [.. source];
        var totalCount = list.Count;
        var meta = pageRequest.Page;

        if (totalCount == 0)
        {
            return PageResultDtoBase<T>.Empty(meta.PageIndex, meta.PageSize);
        }

        var items = pageRequest.Behavior.DisablePaging
            ? list
            : [.. list.Skip(meta.Skip).Take(meta.Take)];

        return PageResultDtoBase<T>.Create(items, pageRequest, totalCount);
    }

    /// <summary>
    /// 转换为分页结果
    /// </summary>
    public static PageResultDtoBase<T> ToPageResult<T>(
        this IEnumerable<T> source,
        int pageIndex,
        int pageSize,
        int totalCount)
    {
        ArgumentNullException.ThrowIfNull(source);

        var items = source.ToList();
        return PageResultDtoBase<T>.Create(items, pageIndex, pageSize, totalCount);
    }

    #endregion

    #region List 扩展

    /// <summary>
    /// 列表转分页结果
    /// </summary>
    public static PageResultDtoBase<T> ToPageResult<T>(
        this List<T> list,
        PageRequestDtoBase pageRequest)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(pageRequest);

        var totalCount = list.Count;
        var meta = pageRequest.Page;

        if (totalCount == 0)
        {
            return PageResultDtoBase<T>.Empty(meta.PageIndex, meta.PageSize);
        }

        var items = pageRequest.Behavior.DisablePaging
            ? list
            : [.. list.Skip(meta.Skip).Take(meta.Take)];

        return PageResultDtoBase<T>.Create(items, pageRequest, totalCount);
    }

    #endregion

    #region 排序扩展

    /// <summary>
    /// 应用排序
    /// </summary>
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, QuerySort sort)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sort);

        if (!sort.IsValid())
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, sort.Field);
        var lambda = Expression.Lambda(property, parameter);

        var methodName = sort.Direction == SortDirection.Ascending ? "OrderBy" : "OrderByDescending";
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type);

        return (IQueryable<T>)method.Invoke(null, [query, lambda])!;
    }

    /// <summary>
    /// 应用多字段排序
    /// </summary>
    public static IQueryable<T> ApplySorts<T>(this IQueryable<T> query, List<QuerySort> sorts)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (sorts == null || sorts.Count == 0)
        {
            return query;
        }

        // 按优先级排序
        var orderedSorts = sorts.Where(s => s.IsValid())
            .OrderBy(s => s.Priority)
            .ToList();

        if (orderedSorts.Count == 0)
        {
            return query;
        }

        // 应用第一个排序
        var result = query.ApplySort(orderedSorts[0]);

        // 应用后续排序（使用 ThenBy）
        for (var i = 1; i < orderedSorts.Count; i++)
        {
            result = result.ApplyThenBy(orderedSorts[i]);
        }

        return result;
    }

    /// <summary>
    /// 应用 ThenBy 排序
    /// </summary>
    private static IQueryable<T> ApplyThenBy<T>(this IQueryable<T> query, QuerySort sort)
    {
        if (!sort.IsValid())
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, sort.Field);
        var lambda = Expression.Lambda(property, parameter);

        var methodName = sort.Direction == SortDirection.Ascending ? "ThenBy" : "ThenByDescending";
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type);

        return (IQueryable<T>)method.Invoke(null, [query, lambda])!;
    }

    #endregion

    #region 过滤扩展

    /// <summary>
    /// 应用单个过滤条件
    /// </summary>
    public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> query, QueryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        if (!filter.IsValid())
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, filter.Field);
        Expression? predicate;
        switch (filter.Operator)
        {
            case QueryOperator.Equal:
                predicate = Expression.Equal(property, Expression.Constant(filter.Value, property.Type));
                break;

            case QueryOperator.NotEqual:
                predicate = Expression.NotEqual(property, Expression.Constant(filter.Value, property.Type));
                break;

            case QueryOperator.GreaterThan:
                predicate = Expression.GreaterThan(property, Expression.Constant(filter.Value, property.Type));
                break;

            case QueryOperator.GreaterThanOrEqual:
                predicate = Expression.GreaterThanOrEqual(property, Expression.Constant(filter.Value, property.Type));
                break;

            case QueryOperator.LessThan:
                predicate = Expression.LessThan(property, Expression.Constant(filter.Value, property.Type));
                break;

            case QueryOperator.LessThanOrEqual:
                predicate = Expression.LessThanOrEqual(property, Expression.Constant(filter.Value, property.Type));
                break;

            case QueryOperator.Contains:
                var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)])!;
                predicate = Expression.Call(property, containsMethod,
                    Expression.Constant(filter.Value, typeof(string)));
                break;

            case QueryOperator.StartsWith:
                var startsWithMethod = typeof(string).GetMethod("StartsWith", [typeof(string)])!;
                predicate = Expression.Call(property, startsWithMethod,
                    Expression.Constant(filter.Value, typeof(string)));
                break;

            case QueryOperator.EndsWith:
                var endsWithMethod = typeof(string).GetMethod("EndsWith", [typeof(string)])!;
                predicate = Expression.Call(property, endsWithMethod,
                    Expression.Constant(filter.Value, typeof(string)));
                break;

            case QueryOperator.IsNull:
                predicate = Expression.Equal(property, Expression.Constant(null, property.Type));
                break;

            case QueryOperator.IsNotNull:
                predicate = Expression.NotEqual(property, Expression.Constant(null, property.Type));
                break;

            default:
                return query;
        }

        var lambda = Expression.Lambda<Func<T, bool>>(predicate, parameter);
        return query.Where(lambda);
    }

    /// <summary>
    /// 应用多个过滤条件（AND 关系）
    /// </summary>
    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, List<QueryFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (filters == null || filters.Count == 0)
        {
            return query;
        }

        return filters.Where(f => f.IsValid())
            .Aggregate(query, (current, filter) => current.ApplyFilter(filter));
    }

    #endregion

    #region 关键字搜索扩展

    /// <summary>
    /// 应用关键字搜索（OR 关系）
    /// </summary>
    public static IQueryable<T> ApplyKeywordSearch<T>(
        this IQueryable<T> query,
        string? keyword,
        params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(keyword) || fields.Length == 0)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combinedPredicate = null;

        foreach (var field in fields)
        {
            var property = Expression.Property(parameter, field);
            var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)])!;
            var predicate = Expression.Call(property, containsMethod,
                Expression.Constant(keyword, typeof(string)));

            combinedPredicate = combinedPredicate == null
                ? predicate
                : Expression.OrElse(combinedPredicate, predicate);
        }

        if (combinedPredicate == null)
        {
            return query;
        }

        var lambda = Expression.Lambda<Func<T, bool>>(combinedPredicate, parameter);
        return query.Where(lambda);
    }

    #endregion

    #region 完整查询应用

    /// <summary>
    /// 应用完整的分页请求（包括过滤、排序、关键字搜索）
    /// </summary>
    public static IQueryable<T> ApplyPageRequest<T>(this IQueryable<T> query, PageRequestDtoBase pageRequest)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(pageRequest);

        var cond = pageRequest.Conditions;
        if (cond.Filters.Count > 0)
        {
            query = query.ApplyFilters(cond.Filters);
        }

        if (!string.IsNullOrWhiteSpace(cond.Keyword?.Value) && (cond.Keyword?.Fields?.Count ?? 0) > 0)
        {
            query = query.ApplyKeywordSearch(cond.Keyword!.Value, [.. cond.Keyword.Fields]);
        }

        if (cond.Sorts.Count > 0)
        {
            query = query.ApplySorts(cond.Sorts);
        }

        if (!pageRequest.Behavior.DisablePaging)
        {
            query = query.ApplyPaging(pageRequest);
        }

        return query;
    }

    #endregion
}
