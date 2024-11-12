#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryableExtensions
// Guid:6a2cf889-e401-4e28-ab96-bf4b3b868708
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/23 19:13:34
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Utils.Extensions.System.Linq;

/// <summary>
/// 查询接口<see cref="IQueryable{T}"/>的扩展方法
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// 用于分页，可以作为Skip(…).Take(…)链的替代方法
    /// </summary>
    public static IQueryable<T> PageBy<T>(this IQueryable<T> query, int skipCount, int maxResultCount)
    {
        CheckHelper.NotNull(query, nameof(query));

        return query.Skip(skipCount).Take(maxResultCount);
    }

    /// <summary>
    /// 用于分页，可以作为Skip(…).Take(…)链的替代方法
    /// </summary>
    public static TQueryable PageBy<T, TQueryable>(this TQueryable query, int skipCount, int maxResultCount)
        where TQueryable : IQueryable<T>
    {
        CheckHelper.NotNull(query, nameof(query));

        return (TQueryable)query.Skip(skipCount).Take(maxResultCount);
    }

    /// <summary>
    /// 如果给定条件为真，则根据给定谓词筛选<see cref="IQueryable{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="condition"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition, Expression<Func<T, bool>> predicate)
    {
        CheckHelper.NotNull(query, nameof(query));

        return condition
            ? query.Where(predicate)
            : query;
    }

    /// <summary>
    /// 如果给定条件为真，则根据给定谓词筛选<see cref="IQueryable{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TQueryable"></typeparam>
    /// <param name="query"></param>
    /// <param name="condition"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static TQueryable WhereIf<T, TQueryable>(this TQueryable query, bool condition, Expression<Func<T, bool>> predicate)
        where TQueryable : IQueryable<T>
    {
        CheckHelper.NotNull(query, nameof(query));

        return condition
            ? (TQueryable)query.Where(predicate)
            : query;
    }

    /// <summary>
    /// 如果给定条件为真，则根据给定谓词筛选<see cref="IQueryable{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="query"></param>
    /// <param name="condition"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition, Expression<Func<T, int, bool>> predicate)
    {
        CheckHelper.NotNull(query, nameof(query));

        return condition
            ? query.Where(predicate)
            : query;
    }

    /// <summary>
    /// 如果给定条件为真，则根据给定谓词筛选<see cref="IQueryable{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TQueryable"></typeparam>
    /// <param name="query"></param>
    /// <param name="condition"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static TQueryable WhereIf<T, TQueryable>(this TQueryable query, bool condition, Expression<Func<T, int, bool>> predicate)
        where TQueryable : IQueryable<T>
    {
        CheckHelper.NotNull(query, nameof(query));

        return condition
            ? (TQueryable)query.Where(predicate)
            : query;
    }

    /// <summary>
    /// 如果给定条件为真，则根据给定谓词排序<see cref="IQueryable{T}"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TQueryable"></typeparam>
    /// <param name="query"></param>
    /// <param name="condition"></param>
    /// <param name="sorting"></param>
    /// <returns></returns>
    public static TQueryable OrderByIf<T, TQueryable>(this TQueryable query, bool condition, string sorting)
        where TQueryable : IQueryable<T>
    {
        if (!condition || string.IsNullOrEmpty(sorting))
        {
            return query;
        }

        ParameterExpression? parameter = Expression.Parameter(typeof(T), "x");
        MemberExpression? property = Expression.Property(parameter, sorting);
        LambdaExpression? lambda = Expression.Lambda(property, parameter);

        string? methodName = "OrderBy";
        Type[]? types = new Type[]
        {
            query.ElementType, property.Type
        };
        MethodCallExpression? resultExpression = Expression.Call(typeof(Queryable), methodName, types, query.Expression, Expression.Quote(lambda));

        return (TQueryable)query.Provider.CreateQuery<T>(resultExpression);
    }
}
