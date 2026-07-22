// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using XiHan.Framework.Domain.Shared.Paging.Builders;
using XiHan.Framework.Domain.Shared.Paging.Dtos;
using XiHan.Framework.Domain.Shared.Paging.Enums;
using XiHan.Framework.Domain.Shared.Paging.Models;
using XiHan.Framework.Utils.Logging;

namespace XiHan.Framework.Data.SqlSugar.Extensions;

/// <summary>
/// SqlSugar 分页扩展方法
/// </summary>
public static class PagingExtensions
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

        // 大小写不敏感解析属性；未知字段安全跳过（避免 Expression.Property 抛异常）
        var propertyInfo = typeof(T).GetProperty(filter.Field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (propertyInfo is null)
        {
            LogHelper.Warn($"[SqlSugar.ApplyFilter] 忽略未知过滤字段：{filter.Field}");
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyInfo);
        var propertyType = propertyInfo.PropertyType;
        Expression? predicate = null;

        try
        {
            switch (filter.Operator)
            {
                case QueryOperator.Equal:
                    predicate = BuildComparison(property, propertyType, filter.Value, Expression.Equal);
                    break;

                case QueryOperator.NotEqual:
                    predicate = BuildComparison(property, propertyType, filter.Value, Expression.NotEqual);
                    break;

                case QueryOperator.GreaterThan:
                    predicate = BuildComparison(property, propertyType, filter.Value, Expression.GreaterThan);
                    break;

                case QueryOperator.GreaterThanOrEqual:
                    predicate = BuildComparison(property, propertyType, filter.Value, Expression.GreaterThanOrEqual);
                    break;

                case QueryOperator.LessThan:
                    predicate = BuildComparison(property, propertyType, filter.Value, Expression.LessThan);
                    break;

                case QueryOperator.LessThanOrEqual:
                    predicate = BuildComparison(property, propertyType, filter.Value, Expression.LessThanOrEqual);
                    break;

                case QueryOperator.Contains:
                case QueryOperator.StartsWith:
                case QueryOperator.EndsWith:
                    if (propertyType == typeof(string) && filter.Value is not null)
                    {
                        var methodName = filter.Operator switch
                        {
                            QueryOperator.StartsWith => "StartsWith",
                            QueryOperator.EndsWith => "EndsWith",
                            _ => "Contains"
                        };
                        var stringMethod = typeof(string).GetMethod(methodName, [typeof(string)])!;
                        var keyword = CoerceValue(filter.Value, typeof(string)) as string;
                        if (keyword is not null)
                        {
                            predicate = Expression.Call(property, stringMethod, Expression.Constant(keyword, typeof(string)));
                        }
                    }
                    break;

                case QueryOperator.In:
                case QueryOperator.NotIn:
                    if (filter.Values is { Length: > 0 })
                    {
                        // 构造与属性类型一致的强类型数组（修复旧版 List<object> 与 Contains<T> 类型不匹配导致静默失效的问题）
                        var typedArray = Array.CreateInstance(propertyType, filter.Values.Length);
                        for (var index = 0; index < filter.Values.Length; index++)
                        {
                            typedArray.SetValue(CoerceValue(filter.Values[index], propertyType), index);
                        }
                        var containsMethod = typeof(Enumerable).GetMethods()
                            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                            .MakeGenericMethod(propertyType);
                        var containsPredicate = Expression.Call(containsMethod, Expression.Constant(typedArray), property);
                        predicate = filter.Operator == QueryOperator.In ? containsPredicate : Expression.Not(containsPredicate);
                    }
                    break;

                case QueryOperator.Between:
                    if (filter.Values is { Length: 2 })
                    {
                        var lower = BuildComparison(property, propertyType, filter.Values[0], Expression.GreaterThanOrEqual);
                        var upper = BuildComparison(property, propertyType, filter.Values[1], Expression.LessThanOrEqual);
                        predicate = Expression.AndAlso(lower, upper);
                    }
                    break;

                case QueryOperator.IsNull:
                    predicate = Expression.Equal(property, Expression.Constant(null, propertyType));
                    break;

                case QueryOperator.IsNotNull:
                    predicate = Expression.NotEqual(property, Expression.Constant(null, propertyType));
                    break;
            }

            if (predicate != null)
            {
                var lambda = Expression.Lambda<Func<T, bool>>(predicate, parameter);
                query = query.Where(lambda);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Warn($"[SqlSugar.ApplyFilter] 忽略无效过滤条件 {filter.Field} {filter.Operator}：{ex.Message}");
        }

        return query;
    }

    /// <summary>
    /// 构造单值比较表达式（结果恒为 bool）。可空值类型属性用 HasValue 守卫，避免 bool? 致 Lambda&lt;Func&lt;T,bool&gt;&gt; 构造失败。
    /// </summary>
    private static Expression BuildComparison(MemberExpression property, Type propertyType, object? rawValue, Func<Expression, Expression, BinaryExpression> compare)
    {
        var underlying = Nullable.GetUnderlyingType(propertyType);
        if (underlying is not null)
        {
            var valueAccess = Expression.Property(property, "Value");
            var hasValue = Expression.Property(property, "HasValue");
            var constant = Expression.Constant(CoerceValue(rawValue, underlying), underlying);
            return Expression.AndAlso(hasValue, compare(valueAccess, constant));
        }

        var nonNullConstant = Expression.Constant(CoerceValue(rawValue, propertyType), propertyType);
        return compare(property, nonNullConstant);
    }

    /// <summary>
    /// 把过滤值（可能来自 JSON 的 JsonElement / 字符串 / 数字）健壮地强转为目标 CLR 类型。
    /// 支持 DateTimeOffset/DateTime/枚举/Guid/bool/基础类型，替代过弱的 Convert.ChangeType（其对这些类型会抛异常被吞掉，致过滤静默失效）。
    /// </summary>
    private static object? CoerceValue(object? value, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            value = JsonElementToRaw(element);
            if (value is null)
            {
                return null;
            }
        }

        if (underlying.IsInstanceOfType(value))
        {
            return value;
        }

        if (underlying.IsEnum)
        {
            return value is string enumName
                ? Enum.Parse(underlying, enumName, ignoreCase: true)
                : Enum.ToObject(underlying, Convert.ChangeType(value, Enum.GetUnderlyingType(underlying), CultureInfo.InvariantCulture));
        }

        if (underlying == typeof(DateTimeOffset))
        {
            return value is string sdto
                ? DateTimeOffset.Parse(sdto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                : (object)new DateTimeOffset(Convert.ToDateTime(value, CultureInfo.InvariantCulture));
        }

        if (underlying == typeof(DateTime))
        {
            return value is string sdt
                ? DateTime.Parse(sdt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        }

        if (underlying == typeof(Guid))
        {
            return Guid.Parse(value.ToString()!);
        }

        if (underlying == typeof(bool))
        {
            return value is string sb ? bool.Parse(sb) : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 取 JsonElement 的原始 CLR 值（字符串/数字/布尔/Null）。
    /// </summary>
    private static object? JsonElementToRaw(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
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

        // 将外部传入的排序字段（C# 属性名）解析为实体属性，再通过 OrderByPropertyName 映射为物理列名。
        // 不能把 s.Field 直接拼进 OrderBy(string)：那会绕过列名映射（列名标准化为 snake_case 后属性名 ≠ 列名，
        // 会触发“column does not exist”），且原始字符串拼接存在 SQL 注入风险。
        try
        {
            foreach (var sort in orderedSorts)
            {
                var property = typeof(T).GetProperty(sort.Field,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                // 未知字段直接忽略，与过滤/关键字搜索的容错策略保持一致
                if (property is null)
                {
                    LogHelper.Warn($"[SqlSugar.ApplySorts] 忽略未知排序字段：{sort.Field}");
                    continue;
                }

                var orderByType = sort.Direction == SortDirection.Ascending ? OrderByType.Asc : OrderByType.Desc;
                query = query.OrderByPropertyName(property.Name, orderByType);
            }
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
        var items = await query.Skip(skip).Take(meta.PageSize).ToListAsync(cancellationToken);

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
        var items = query.Skip(skip).Take(meta.PageSize).ToList();

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

        var meta = request.Page;
        var skip = (meta.PageIndex - 1) * meta.PageSize;
        return query.Skip(skip).Take(meta.PageSize);
    }

    #endregion
}
