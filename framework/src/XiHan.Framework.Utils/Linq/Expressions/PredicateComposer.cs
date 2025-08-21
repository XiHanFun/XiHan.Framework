#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PredicateComposer
// Guid:a1b2c3d4-e5f6-7890-abcd-ef1234567890
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/1/13 5:35:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;

namespace XiHan.Framework.Utils.Linq.Expressions;

/// <summary>
/// 断言组合器
/// 提供静态方法来组合和操作 Lambda 表达式断言
/// </summary>
public static class PredicateComposer
{
    #region 基础工厂方法

    /// <summary>
    /// 创建始终返回 true 的断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <returns>始终返回 true 的表达式</returns>
    public static Expression<Func<T, bool>> True<T>()
    {
        return _ => true;
    }

    /// <summary>
    /// 创建始终返回 false 的断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <returns>始终返回 false 的表达式</returns>
    public static Expression<Func<T, bool>> False<T>()
    {
        return _ => false;
    }

    #endregion

    #region 逻辑组合方法

    /// <summary>
    /// 使用逻辑与（AND）组合两个断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="first">第一个断言</param>
    /// <param name="second">第二个断言</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Compose(second, Expression.AndAlso);
    }

    /// <summary>
    /// 使用逻辑或（OR）组合两个断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="first">第一个断言</param>
    /// <param name="second">第二个断言</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        return first.Compose(second, Expression.OrElse);
    }

    /// <summary>
    /// 对断言取反（NOT）
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="expression">要取反的表达式</param>
    /// <returns>取反后的表达式</returns>
    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var body = Expression.Not(Expression.Invoke(expression, parameter));
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    #endregion

    #region 条件组合方法

    /// <summary>
    /// 根据条件决定是否应用断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="condition">条件</param>
    /// <param name="predicate">要应用的断言</param>
    /// <returns>如果条件为真则返回断言，否则返回 True</returns>
    public static Expression<Func<T, bool>> If<T>(bool condition, Expression<Func<T, bool>> predicate)
    {
        return condition ? predicate : True<T>();
    }

    /// <summary>
    /// 根据条件决定是否将断言与现有表达式组合
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="expression">现有表达式</param>
    /// <param name="condition">条件</param>
    /// <param name="predicate">要组合的断言</param>
    /// <returns>如果条件为真则与断言组合，否则返回原表达式</returns>
    public static Expression<Func<T, bool>> AndIf<T>(this Expression<Func<T, bool>> expression, bool condition, Expression<Func<T, bool>> predicate)
    {
        return condition ? expression.And(predicate) : expression;
    }

    /// <summary>
    /// 根据条件决定是否将断言与现有表达式组合
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="expression">现有表达式</param>
    /// <param name="condition">条件</param>
    /// <param name="predicate">要组合的断言</param>
    /// <returns>如果条件为真则与断言组合，否则返回原表达式</returns>
    public static Expression<Func<T, bool>> OrIf<T>(this Expression<Func<T, bool>> expression, bool condition, Expression<Func<T, bool>> predicate)
    {
        return condition ? expression.Or(predicate) : expression;
    }

    #endregion

    #region 批量组合方法

    /// <summary>
    /// 使用逻辑与组合多个断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="predicates">断言集合</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, bool>> AndAll<T>(params Expression<Func<T, bool>>[] predicates)
    {
        return AndAll(predicates.AsEnumerable());
    }

    /// <summary>
    /// 使用逻辑与组合多个断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="predicates">断言集合</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, bool>> AndAll<T>(IEnumerable<Expression<Func<T, bool>>> predicates)
    {
        var predicateList = predicates.Where(p => p != null).ToList();
        if (predicateList.Count == 0)
        {
            return True<T>();
        }

        var result = predicateList.First();
        for (int i = 1; i < predicateList.Count; i++)
        {
            result = result.And(predicateList[i]);
        }

        return result;
    }

    /// <summary>
    /// 使用逻辑或组合多个断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="predicates">断言集合</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, bool>> OrAll<T>(params Expression<Func<T, bool>>[] predicates)
    {
        return OrAll(predicates.AsEnumerable());
    }

    /// <summary>
    /// 使用逻辑或组合多个断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="predicates">断言集合</param>
    /// <returns>组合后的表达式</returns>
    public static Expression<Func<T, bool>> OrAll<T>(IEnumerable<Expression<Func<T, bool>>> predicates)
    {
        var predicateList = predicates.Where(p => p != null).ToList();
        if (predicateList.Count == 0)
        {
            return False<T>();
        }

        var result = predicateList.First();
        for (int i = 1; i < predicateList.Count; i++)
        {
            result = result.Or(predicateList[i]);
        }

        return result;
    }

    #endregion

    #region 动态构建方法

    /// <summary>
    /// 根据属性名和值创建等于断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">比较值</param>
    /// <returns>等于断言表达式</returns>
    public static Expression<Func<T, bool>> Equal<T>(string propertyName, object? value)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.PropertyOrField(parameter, propertyName);
        var constant = Expression.Constant(value, property.Type);
        var body = Expression.Equal(property, constant);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary>
    /// 根据属性名和值创建不等于断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">比较值</param>
    /// <returns>不等于断言表达式</returns>
    public static Expression<Func<T, bool>> NotEqual<T>(string propertyName, object? value)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.PropertyOrField(parameter, propertyName);
        var constant = Expression.Constant(value, property.Type);
        var body = Expression.NotEqual(property, constant);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary>
    /// 根据属性名和值创建大于断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">比较值</param>
    /// <returns>大于断言表达式</returns>
    public static Expression<Func<T, bool>> GreaterThan<T>(string propertyName, object value)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.PropertyOrField(parameter, propertyName);
        var constant = Expression.Constant(value, property.Type);
        var body = Expression.GreaterThan(property, constant);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary>
    /// 根据属性名和值创建小于断言
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">比较值</param>
    /// <returns>小于断言表达式</returns>
    public static Expression<Func<T, bool>> LessThan<T>(string propertyName, object value)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.PropertyOrField(parameter, propertyName);
        var constant = Expression.Constant(value, property.Type);
        var body = Expression.LessThan(property, constant);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary>
    /// 根据属性名和值创建包含断言（字符串）
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="value">包含的值</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>包含断言表达式</returns>
    public static Expression<Func<T, bool>> Contains<T>(string propertyName, string value, bool ignoreCase = false)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.PropertyOrField(parameter, propertyName);
        var constant = Expression.Constant(value);

        Expression body;
        if (ignoreCase)
        {
            var stringComparison = Expression.Constant(StringComparison.OrdinalIgnoreCase);
            var method = typeof(string).GetMethod("Contains", [typeof(string), typeof(StringComparison)])!;
            body = Expression.Call(property, method, constant, stringComparison);
        }
        else
        {
            var method = typeof(string).GetMethod("Contains", [typeof(string)])!;
            body = Expression.Call(property, method, constant);
        }

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary>
    /// 根据属性名和值集合创建包含断言（集合）
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="propertyName">属性名</param>
    /// <param name="values">值集合</param>
    /// <returns>包含断言表达式</returns>
    public static Expression<Func<T, bool>> In<T, TValue>(string propertyName, IEnumerable<TValue> values)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.PropertyOrField(parameter, propertyName);
        var valuesList = values.ToList();
        var constant = Expression.Constant(valuesList);
        var method = typeof(List<TValue>).GetMethod("Contains", [typeof(TValue)])!;
        var body = Expression.Call(constant, method, property);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    #endregion

    #region 转换方法

    /// <summary>
    /// 将表达式转换为另一种类型
    /// </summary>
    /// <typeparam name="TSource">源类型</typeparam>
    /// <typeparam name="TTarget">目标类型</typeparam>
    /// <param name="expression">源表达式</param>
    /// <param name="converter">类型转换器</param>
    /// <returns>转换后的表达式</returns>
    public static Expression<Func<TTarget, bool>> Convert<TSource, TTarget>(
        Expression<Func<TSource, bool>> expression,
        Expression<Func<TTarget, TSource>> converter)
    {
        var parameter = Expression.Parameter(typeof(TTarget), "x");
        var convertedParameter = Expression.Invoke(converter, parameter);
        var body = Expression.Invoke(expression, convertedParameter);
        return Expression.Lambda<Func<TTarget, bool>>(body, parameter);
    }

    #endregion

    #region 内部辅助方法

    /// <summary>
    /// 使用指定的二元操作符组合两个表达式
    /// </summary>
    /// <typeparam name="T">类型参数</typeparam>
    /// <param name="first">第一个表达式</param>
    /// <param name="second">第二个表达式</param>
    /// <param name="merge">合并操作</param>
    /// <returns>组合后的表达式</returns>
    private static Expression<Func<T, bool>> Compose<T>(
        this Expression<Func<T, bool>> first,
        Expression<Func<T, bool>> second,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        
        // 替换参数
        var firstBody = new ParameterReplacer(first.Parameters[0], parameter).Visit(first.Body);
        var secondBody = new ParameterReplacer(second.Parameters[0], parameter).Visit(second.Body);
        
        var body = merge(firstBody!, secondBody!);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    #endregion

    #region 内部类

    /// <summary>
    /// 参数替换访问器
    /// </summary>
    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : node;
        }
    }

    #endregion
}
