#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SqlSugarPagingExtensions
// Guid:1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/2 20:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.Linq.Expressions;
using XiHan.Framework.Domain.Shared.Paging.Builders;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Data.SqlSugar.Extensions;

/// <summary>
/// SqlSugar 分页扩展方法
/// </summary>
public static class SqlSugarPagingExtensions
{
    #region 自动查询扩展

    /// <summary>
    /// 自动查询并返回分页结果（从 PageRequestDtoBase 或普通 DTO）
    /// </summary>
    public static async Task<PageResultDtoBase<T>> ToPageResultAutoAsync<T>(
        this ISugarQueryable<T> query,
        object queryDto,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        PageRequestDtoBase request;

        // 如果 queryDto 本身就是 PageRequestDtoBase，直接使用
        if (queryDto is PageRequestDtoBase pageRequest)
        {
            request = pageRequest;
        }
        else
        {
            // 否则通过 AutoQueryBuilder 构建
            request = AutoQueryBuilder.BuildFrom(queryDto);
        }

        // 应用查询条件
        query = query.ApplyPageRequest(request);

        // 执行分页查询
        return await query.ToPageResultAsync(request, cancellationToken);
    }

    /// <summary>
    /// 自动查询并返回分页结果（同步版本）
    /// </summary>
    public static PageResultDtoBase<T> ToPageResultAuto<T>(
        this ISugarQueryable<T> query,
        object queryDto) where T : class, new()
    {
        PageRequestDtoBase request;

        // 如果 queryDto 本身就是 PageRequestDtoBase，直接使用
        if (queryDto is PageRequestDtoBase pageRequest)
        {
            request = pageRequest;
        }
        else
        {
            // 否则通过 AutoQueryBuilder 构建
            request = AutoQueryBuilder.BuildFrom(queryDto);
        }

        query = query.ApplyPageRequest(request);
        return query.ToPageResult(request);
    }

    #endregion

    #region 应用查询条件

    /// <summary>
    /// 应用完整的分页请求（过滤+排序+分页）
    /// </summary>
    public static ISugarQueryable<T> ApplyPageRequest<T>(
        this ISugarQueryable<T> query,
        PageRequestDtoBase request) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        var cond = request.Conditions;
        if (cond.Filters.Count > 0)
        {
            query = query.ApplyFilters(cond.Filters);
        }

        if (!string.IsNullOrWhiteSpace(cond.Keyword?.Value) && (cond.Keyword?.Fields?.Count ?? 0) > 0)
        {
            query = query.ApplyKeywordSearch(cond.Keyword!.Value, cond.Keyword.Fields ?? []);
        }

        if (cond.Sorts.Count > 0)
        {
            query = query.ApplySorts(cond.Sorts);
        }

        return query;
    }

    /// <summary>
    /// 应用过滤条件
    /// </summary>
    public static ISugarQueryable<T> ApplyFilters<T>(
        this ISugarQueryable<T> query,
        List<QueryFilter> filters) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(query);

        if (filters == null || filters.Count == 0)
        {
            return query;
        }

        foreach (var filter in filters.Where(f => f.IsValid()))
        {
            query = query.ApplyFilter(filter);
        }

        return query;
    }

    /// <summary>
    /// 应用单个过滤条件
    /// </summary>
    public static ISugarQueryable<T> ApplyFilter<T>(
        this ISugarQueryable<T> query,
        QueryFilter filter) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filter);

        if (!filter.IsValid())
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, filter.Field);
        Expression? predicate = null;

        try
        {
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
                    if (property.Type == typeof(string))
                    {
                        var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)])!;
                        predicate = Expression.Call(property, containsMethod, Expression.Constant(filter.Value, typeof(string)));
                    }
                    break;

                case QueryOperator.StartsWith:
                    if (property.Type == typeof(string))
                    {
                        var startsWithMethod = typeof(string).GetMethod("StartsWith", [typeof(string)])!;
                        predicate = Expression.Call(property, startsWithMethod, Expression.Constant(filter.Value, typeof(string)));
                    }
                    break;

                case QueryOperator.EndsWith:
                    if (property.Type == typeof(string))
                    {
                        var endsWithMethod = typeof(string).GetMethod("EndsWith", [typeof(string)])!;
                        predicate = Expression.Call(property, endsWithMethod, Expression.Constant(filter.Value, typeof(string)));
                    }
                    break;

                case QueryOperator.In:
                case QueryOperator.NotIn:
                    if (filter.Values is { Length: > 0 })
                    {
                        var containsMethod = typeof(Enumerable).GetMethods()
                            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                            .MakeGenericMethod(property.Type);

                        var convertedValues = filter.Values.Select(v => Convert.ChangeType(v, property.Type)).ToList();
                        var valuesExpr = Expression.Constant(convertedValues);
                        var containsPredicate = Expression.Call(containsMethod, valuesExpr, property);

                        predicate = filter.Operator == QueryOperator.In
                            ? containsPredicate
                            : Expression.Not(containsPredicate);
                    }
                    break;

                case QueryOperator.Between:
                    if (filter.Values is { Length: 2 })
                    {
                        var start = Expression.Constant(Convert.ChangeType(filter.Values[0], property.Type), property.Type);
                        var end = Expression.Constant(Convert.ChangeType(filter.Values[1], property.Type), property.Type);
                        predicate = Expression.AndAlso(
                            Expression.GreaterThanOrEqual(property, start),
                            Expression.LessThanOrEqual(property, end));
                    }
                    break;

                case QueryOperator.IsNull:
                    predicate = Expression.Equal(property, Expression.Constant(null, property.Type));
                    break;

                case QueryOperator.IsNotNull:
                    predicate = Expression.NotEqual(property, Expression.Constant(null, property.Type));
                    break;
            }

            if (predicate != null)
            {
                var lambda = Expression.Lambda<Func<T, bool>>(predicate, parameter);
                query = query.Where(lambda);
            }
        }
        catch
        {
            // 忽略无效的过滤条件
        }

        return query;
    }

    /// <summary>
    /// 应用关键字搜索
    /// </summary>
    public static ISugarQueryable<T> ApplyKeywordSearch<T>(
        this ISugarQueryable<T> query,
        string? keyword,
        List<string> fields) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(keyword) || fields.Count == 0)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combinedPredicate = null;

        foreach (var field in fields)
        {
            try
            {
                var property = Expression.Property(parameter, field);
                if (property.Type == typeof(string))
                {
                    var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)])!;
                    var predicate = Expression.Call(property, containsMethod, Expression.Constant(keyword, typeof(string)));

                    combinedPredicate = combinedPredicate == null
                        ? predicate
                        : Expression.OrElse(combinedPredicate, predicate);
                }
            }
            catch
            {
                // 忽略无效字段
            }
        }

        if (combinedPredicate != null)
        {
            var lambda = Expression.Lambda<Func<T, bool>>(combinedPredicate, parameter);
            query = query.Where(lambda);
        }

        return query;
    }

    /// <summary>
    /// 应用排序条件
    /// </summary>
    public static ISugarQueryable<T> ApplySorts<T>(
        this ISugarQueryable<T> query,
        List<QuerySort> sorts) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(query);

        if (sorts == null || sorts.Count == 0)
        {
            return query;
        }

        var orderedSorts = sorts.Where(s => s.IsValid())
            .OrderBy(s => s.Priority)
            .ToList();

        if (orderedSorts.Count == 0)
        {
            return query;
        }

        // 构建排序表达式字符串
        var orderByFields = orderedSorts.Select(s =>
            $"{s.Field} {(s.Direction == SortDirection.Ascending ? "ASC" : "DESC")}"
        );

        var orderByString = string.Join(", ", orderByFields);

        try
        {
            query = query.OrderBy(orderByString);
            Console.WriteLine(query.ToSqlString());
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[SqlSugar.ApplySorts] OrderBy failed: {ex.Message}");
            throw;
        }

        return query;
    }

    #endregion

    #region 转换为分页结果

    /// <summary>
    /// 转换为分页结果（异步）
    /// </summary>
    public static async Task<PageResultDtoBase<T>> ToPageResultAsync<T>(
        this ISugarQueryable<T> query,
        PageRequestDtoBase request,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        var totalCount = await query.CountAsync(cancellationToken);
        var meta = request.Page;

        if (totalCount == 0)
        {
            return PageResultDtoBase<T>.Empty(meta.PageIndex, meta.PageSize);
        }

        var skip = (meta.PageIndex - 1) * meta.PageSize;
        var items = request.Behavior.DisablePaging
            ? await query.ToListAsync(cancellationToken)
            : await query.Skip(skip).Take(meta.PageSize).ToListAsync(cancellationToken);

        return PageResultDtoBase<T>.Create(items, request, totalCount);
    }

    /// <summary>
    /// 转换为分页结果（同步）
    /// </summary>
    public static PageResultDtoBase<T> ToPageResult<T>(
        this ISugarQueryable<T> query,
        PageRequestDtoBase request) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        var totalCount = query.Count();
        var meta = request.Page;

        if (totalCount == 0)
        {
            return PageResultDtoBase<T>.Empty(meta.PageIndex, meta.PageSize);
        }

        var skip = (meta.PageIndex - 1) * meta.PageSize;
        var items = request.Behavior.DisablePaging
            ? query.ToList()
            : query.Skip(skip).Take(meta.PageSize).ToList();

        return PageResultDtoBase<T>.Create(items, request, totalCount);
    }

    /// <summary>
    /// 应用分页（只分页，不返回结果）
    /// </summary>
    public static ISugarQueryable<T> ApplyPaging<T>(
        this ISugarQueryable<T> query,
        PageRequestDtoBase request) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Behavior.DisablePaging)
        {
            return query;
        }

        var meta = request.Page;
        var skip = (meta.PageIndex - 1) * meta.PageSize;
        return query.Skip(skip).Take(meta.PageSize);
    }

    #endregion
}
