#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExpressionExtensions
// Guid:d4719e30-70b9-4e8b-a552-6dd8cde96518
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 4:57:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;

namespace XiHan.Framework.Utils.Linq.Expressions;

/// <summary>
/// 表达式扩展方法
/// </summary>
public static class ExpressionExtensions
{
    /// <summary>
    /// 合并两个表达式，支持 AndAlso 和 OrElse 操作符
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <param name="merge"></param>
    /// <returns></returns>
    public static Expression<Func<T, bool>> Combine<T>(this Expression<Func<T, bool>>? first, Expression<Func<T, bool>> second, Func<Expression, Expression, BinaryExpression> merge)
    {
        if (first is null)
        {
            return second;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var body = merge(
            Expression.Invoke(first, parameter),
            Expression.Invoke(second, parameter)
        );

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary>
    /// 使用 AndAlso 合并两个表达式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    public static Expression<Func<T, bool>> AndAlso<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Combine(second, Expression.AndAlso);
    }

    /// <summary>
    /// 使用 OrElse 合并两个表达式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <returns></returns>
    public static Expression<Func<T, bool>> OrElse<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Combine(second, Expression.OrElse);
    }

    /// <summary>
    /// 根据属性名称动态创建过滤表达式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    /// <param name="comparison"></param>
    /// <returns></returns>
    public static Expression<Func<T, bool>> CreateFilter<T>(string propertyName, object value, Func<Expression, Expression, BinaryExpression> comparison)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.PropertyOrField(parameter, propertyName);
        var constant = Expression.Constant(value);

        var body = comparison(property, constant);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary>
    /// 动态生成 Equal 过滤表达式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static Expression<Func<T, bool>> EqualFilter<T>(string propertyName, object value)
    {
        return CreateFilter<T>(propertyName, value, Expression.Equal);
    }

    /// <summary>
    /// 动态生成 GreaterThan 过滤表达式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static Expression<Func<T, bool>> GreaterThanFilter<T>(string propertyName, object value)
    {
        return CreateFilter<T>(propertyName, value, Expression.GreaterThan);
    }

    /// <summary>
    /// 动态生成 LessThan 过滤表达式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static Expression<Func<T, bool>> LessThanFilter<T>(string propertyName, object value)
    {
        return CreateFilter<T>(propertyName, value, Expression.LessThan);
    }

    /// <summary>
    /// 根据属性名称动态生成排序表达式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="propertyName"></param>
    /// <param name="ascending"></param>
    /// <returns></returns>
    public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, string propertyName, bool ascending = true)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.PropertyOrField(parameter, propertyName);
        var keySelector = Expression.Lambda(property, parameter);

        var methodName = ascending ? "OrderBy" : "OrderByDescending";
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type);

        var typeObject = method.Invoke(null, [source, keySelector]);
        return typeObject is null
            ? throw new InvalidOperationException("OrderBy 方法调用失败。")
            : (IOrderedQueryable<T>)typeObject;
    }

    /// <summary>
    /// 根据属性名称动态生成 ThenBy 表达式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="propertyName"></param>
    /// <param name="ascending"></param>
    /// <returns></returns>
    public static IOrderedQueryable<T> ThenBy<T>(this IOrderedQueryable<T> source, string propertyName, bool ascending = true)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.PropertyOrField(parameter, propertyName);
        var keySelector = Expression.Lambda(property, parameter);

        var methodName = ascending ? "ThenBy" : "ThenByDescending";
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), property.Type);

        var typeObject = method.Invoke(null, [source, keySelector]);
        return typeObject is null
            ? throw new InvalidOperationException("ThenBy 方法调用失败。")
            : (IOrderedQueryable<T>)typeObject;
    }

    /// <summary>
    /// 动态生成 Select 投影表达式
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="propertyNames"></param>
    /// <returns></returns>
    public static IQueryable<dynamic> SelectProperties<T>(this IQueryable<T> source, params string[] propertyNames)
    {
        var parameter = Expression.Parameter(typeof(T), "x");

        var bindings = propertyNames.Select(name =>
        {
            var property = Expression.PropertyOrField(parameter, name);
            return Expression.Bind(property.Member, property);
        });

        var newExpression = Expression.MemberInit(Expression.New(typeof(object)), bindings);
        var lambda = Expression.Lambda<Func<T, object>>(newExpression, parameter);

        return source.Select(lambda);
    }
}
