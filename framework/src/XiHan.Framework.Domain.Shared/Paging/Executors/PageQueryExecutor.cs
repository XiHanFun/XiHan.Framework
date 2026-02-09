#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageQueryExecutor
// Guid:7a8b9c0d-1e2f-3a4b-5c6d-7e8f9a0b1c2d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/2 16:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;
using XiHan.Framework.Domain.Shared.Paging.Reflection;
using XiHan.Framework.Domain.Shared.Paging.Validators;

namespace XiHan.Framework.Domain.Shared.Paging.Executors;

/// <summary>
/// 分页查询执行器 - 自动根据 Attribute 配置执行查询
/// </summary>
public class PageQueryExecutor<T> where T : class
{
    private readonly Dictionary<string, QueryFieldInfo> _queryFields;
    private readonly Type _entityType;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PageQueryExecutor()
    {
        _entityType = typeof(T);
        _queryFields = AttributeReader.GetQueryFields<T>();
    }

    /// <summary>
    /// 执行分页查询（带验证）
    /// </summary>
    public PageResultDtoBase<T> Execute(IQueryable<T> query, PageRequestDtoBase request, bool validate = true)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        // 1. 验证请求
        if (validate)
        {
            var validationResult = AttributeBasedValidator.ValidatePageRequest<T>(request);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"分页请求验证失败: {validationResult.GetErrorMessage()}");
            }
        }

        var q = request.QueryMetadata ?? new QueryMetadata();
        var meta = request.PageRequestMetadata;

        if (!string.IsNullOrWhiteSpace(q.Keyword) && q.KeywordFields.Count == 0)
        {
            q.KeywordFields = AttributeReader.GetDefaultKeywordFields<T>();
        }

        query = ApplyFilters(query, q.Filters);
        query = ApplyKeywordSearch(query, q.Keyword, q.KeywordFields);
        query = ApplySorts(query, q.Sorts);

        var totalCount = query.Count();

        if (totalCount == 0)
        {
            return PageResultDtoBase<T>.Empty(meta.PageIndex, meta.PageSize);
        }

        if (!q.DisablePaging)
        {
            query = query.Skip(meta.Skip).Take(meta.Take);
        }

        // 8. 执行查询
        var items = query.ToList();

        return PageResultDtoBase<T>.Create(items, request, totalCount);
    }

    /// <summary>
    /// 异步执行分页查询
    /// </summary>
    public async Task<PageResultDtoBase<T>> ExecuteAsync(IQueryable<T> query, PageRequestDtoBase request,
        bool validate = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);

        // 验证和准备
        if (validate)
        {
            var validationResult = AttributeBasedValidator.ValidatePageRequest<T>(request);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"分页请求验证失败: {validationResult.GetErrorMessage()}");
            }
        }

        var q = request.QueryMetadata ?? new QueryMetadata();
        var meta = request.PageRequestMetadata;

        if (!string.IsNullOrWhiteSpace(q.Keyword) && q.KeywordFields.Count == 0)
        {
            q.KeywordFields = AttributeReader.GetDefaultKeywordFields<T>();
        }

        query = ApplyFilters(query, q.Filters);
        query = ApplyKeywordSearch(query, q.Keyword, q.KeywordFields);
        query = ApplySorts(query, q.Sorts);

        var totalCount = query.Count();

        if (totalCount == 0)
        {
            return PageResultDtoBase<T>.Empty(meta.PageIndex, meta.PageSize);
        }

        if (!q.DisablePaging)
        {
            query = query.Skip(meta.Skip).Take(meta.Take);
        }

        var items = query.ToList();

        return PageResultDtoBase<T>.Create(items, request, totalCount);
    }

    /// <summary>
    /// 获取查询字段信息
    /// </summary>
    public Dictionary<string, QueryFieldInfo> GetQueryFields() => _queryFields;

    /// <summary>
    /// 获取默认关键字搜索字段
    /// </summary>
    public List<string> GetDefaultKeywordFields() => AttributeReader.GetDefaultKeywordFields<T>();

    /// <summary>
    /// 应用单个过滤条件
    /// </summary>
    private static IQueryable<T> ApplySingleFilter(IQueryable<T> query, string fieldName, QueryFilter filter)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, fieldName);
        try
        {
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
                    predicate = Expression.GreaterThanOrEqual(property,
                        Expression.Constant(filter.Value, property.Type));
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
    /// 应用单个排序
    /// </summary>
    private static IQueryable<T> ApplySingleSort(IQueryable<T> query, string fieldName, SortDirection direction,
        bool isFirst)
    {
        try
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, fieldName);
            var lambda = Expression.Lambda(property, parameter);

            var methodName = isFirst
                ? direction == SortDirection.Ascending ? "OrderBy" : "OrderByDescending"
                : direction == SortDirection.Ascending ? "ThenBy" : "ThenByDescending";
            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);

            return (IQueryable<T>)method.Invoke(null, [query, lambda])!;
        }
        catch
        {
            return query;
        }
    }

    /// <summary>
    /// 应用过滤条件（解析别名）
    /// </summary>
    private IQueryable<T> ApplyFilters(IQueryable<T> query, List<QueryFilter> filters)
    {
        foreach (var filter in filters.Where(f => f.IsValid()))
        {
            // 解析字段名（处理别名）
            var actualFieldName = ResolveFieldName(filter.Field);
            if (string.IsNullOrEmpty(actualFieldName))
            {
                continue;
            }

            query = PageQueryExecutor<T>.ApplySingleFilter(query, actualFieldName, filter);
        }

        return query;
    }

    /// <summary>
    /// 应用关键字搜索（根据 KeywordSearchAttribute 配置）
    /// </summary>
    private IQueryable<T> ApplyKeywordSearch(IQueryable<T> query, string? keyword, List<string> keywordFields)
    {
        if (string.IsNullOrWhiteSpace(keyword) || keywordFields.Count == 0)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combinedPredicate = null;

        foreach (var field in keywordFields)
        {
            var actualFieldName = ResolveFieldName(field);
            if (string.IsNullOrEmpty(actualFieldName))
            {
                continue;
            }

            var fieldInfo = _queryFields.GetValueOrDefault(actualFieldName);
            if (fieldInfo?.PropertyType != typeof(string))
            {
                continue;
            }

            try
            {
                var property = Expression.Property(parameter, actualFieldName);
                Expression predicate;

                // 根据 KeywordMatchMode 选择匹配方式
                var matchMode = fieldInfo.KeywordSearchEnabled
                    ? fieldInfo.KeywordMatchMode
                    : KeywordMatchMode.Contains;

                switch (matchMode)
                {
                    case KeywordMatchMode.Contains:
                        var containsMethod = typeof(string).GetMethod("Contains", [typeof(string)])!;
                        predicate = Expression.Call(property, containsMethod,
                            Expression.Constant(keyword, typeof(string)));
                        break;

                    case KeywordMatchMode.StartsWith:
                        var startsWithMethod = typeof(string).GetMethod("StartsWith", [typeof(string)])!;
                        predicate = Expression.Call(property, startsWithMethod,
                            Expression.Constant(keyword, typeof(string)));
                        break;

                    case KeywordMatchMode.EndsWith:
                        var endsWithMethod = typeof(string).GetMethod("EndsWith", [typeof(string)])!;
                        predicate = Expression.Call(property, endsWithMethod,
                            Expression.Constant(keyword, typeof(string)));
                        break;

                    case KeywordMatchMode.Exact:
                        predicate = Expression.Equal(property, Expression.Constant(keyword, typeof(string)));
                        break;

                    default:
                        continue;
                }

                combinedPredicate = combinedPredicate == null
                    ? predicate
                    : Expression.OrElse(combinedPredicate, predicate);
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
    /// 应用排序（解析别名）
    /// </summary>
    private IQueryable<T> ApplySorts(IQueryable<T> query, List<QuerySort> sorts)
    {
        var validSorts = sorts.Where(s => s.IsValid())
            .OrderBy(s => s.Priority)
            .ToList();

        if (validSorts.Count == 0)
        {
            return query;
        }

        // 应用第一个排序
        var firstSort = validSorts[0];
        var actualFieldName = ResolveFieldName(firstSort.Field);
        if (!string.IsNullOrEmpty(actualFieldName))
        {
            query = PageQueryExecutor<T>.ApplySingleSort(query, actualFieldName, firstSort.Direction, true);
        }

        // 应用后续排序
        for (var i = 1; i < validSorts.Count; i++)
        {
            actualFieldName = ResolveFieldName(validSorts[i].Field);
            if (!string.IsNullOrEmpty(actualFieldName))
            {
                query = PageQueryExecutor<T>.ApplySingleSort(query, actualFieldName, validSorts[i].Direction, false);
            }
        }

        return query;
    }

    /// <summary>
    /// 解析字段名（处理别名）
    /// </summary>
    private string? ResolveFieldName(string fieldName)
    {
        if (_queryFields.TryGetValue(fieldName, out var fieldInfo))
        {
            return fieldInfo.PropertyName;
        }

        return null;
    }
}
