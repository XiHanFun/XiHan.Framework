#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ExpressionBuilder
// Guid:f1e2a3b4-c5d6-7e8f-9a0b-1c2d3e4f5a6b
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/01/13 05:30:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using System.Reflection;

namespace XiHan.Framework.Utils.Linq.Expressions;

/// <summary>
/// 泛型表达式构建器
/// 提供流式 API 来动态构建 Lambda 表达式
/// </summary>
/// <typeparam name="T">目标类型</typeparam>
public class ExpressionBuilder<T>
{
    private readonly ParameterExpression _parameter;
    private Expression? _body;

    /// <summary>
    /// 构造函数
    /// </summary>
    public ExpressionBuilder()
    {
        _parameter = Expression.Parameter(typeof(T), "x");
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="parameterName">参数名称</param>
    public ExpressionBuilder(string parameterName)
    {
        _parameter = Expression.Parameter(typeof(T), parameterName);
    }

    #region 属性访问

    /// <summary>
    /// 访问属性
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> Property(string propertyName)
    {
        var property = Expression.PropertyOrField(_parameter, propertyName);
        _body = property;
        return this;
    }

    /// <summary>
    /// 访问嵌套属性
    /// </summary>
    /// <param name="propertyPath">属性路径，如 "User.Profile.Name"</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> NestedProperty(string propertyPath)
    {
        var properties = propertyPath.Split('.');
        Expression current = _parameter;

        foreach (var propertyName in properties)
        {
            current = Expression.PropertyOrField(current, propertyName);
        }

        _body = current;
        return this;
    }

    #endregion

    #region 比较操作

    /// <summary>
    /// 等于比较
    /// </summary>
    /// <param name="value">比较值</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> Equal(object? value)
    {
        var constant = Expression.Constant(value, _body?.Type ?? typeof(object));
        _body = Expression.Equal(_body ?? _parameter, constant);
        return this;
    }

    /// <summary>
    /// 不等于比较
    /// </summary>
    /// <param name="value">比较值</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> NotEqual(object? value)
    {
        var constant = Expression.Constant(value, _body?.Type ?? typeof(object));
        _body = Expression.NotEqual(_body ?? _parameter, constant);
        return this;
    }

    /// <summary>
    /// 大于比较
    /// </summary>
    /// <param name="value">比较值</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> GreaterThan(object value)
    {
        var constant = Expression.Constant(value, _body?.Type ?? typeof(object));
        _body = Expression.GreaterThan(_body ?? _parameter, constant);
        return this;
    }

    /// <summary>
    /// 大于等于比较
    /// </summary>
    /// <param name="value">比较值</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> GreaterThanOrEqual(object value)
    {
        var constant = Expression.Constant(value, _body?.Type ?? typeof(object));
        _body = Expression.GreaterThanOrEqual(_body ?? _parameter, constant);
        return this;
    }

    /// <summary>
    /// 小于比较
    /// </summary>
    /// <param name="value">比较值</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> LessThan(object value)
    {
        var constant = Expression.Constant(value, _body?.Type ?? typeof(object));
        _body = Expression.LessThan(_body ?? _parameter, constant);
        return this;
    }

    /// <summary>
    /// 小于等于比较
    /// </summary>
    /// <param name="value">比较值</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> LessThanOrEqual(object value)
    {
        var constant = Expression.Constant(value, _body?.Type ?? typeof(object));
        _body = Expression.LessThanOrEqual(_body ?? _parameter, constant);
        return this;
    }

    #endregion

    #region 字符串操作

    /// <summary>
    /// 字符串包含
    /// </summary>
    /// <param name="value">包含的值</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> Contains(string value, bool ignoreCase = false)
    {
        if (_body?.Type != typeof(string))
        {
            throw new InvalidOperationException("Contains 操作只能应用于字符串类型");
        }

        var constant = Expression.Constant(value);
        MethodInfo? method;

        if (ignoreCase)
        {
            var stringComparison = Expression.Constant(StringComparison.OrdinalIgnoreCase);
            method = typeof(string).GetMethod("Contains", [typeof(string), typeof(StringComparison)]);
            _body = Expression.Call(_body, method!, constant, stringComparison);
        }
        else
        {
            method = typeof(string).GetMethod("Contains", [typeof(string)]);
            _body = Expression.Call(_body, method!, constant);
        }

        return this;
    }

    /// <summary>
    /// 字符串开始于
    /// </summary>
    /// <param name="value">前缀值</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> StartsWith(string value, bool ignoreCase = false)
    {
        if (_body?.Type != typeof(string))
        {
            throw new InvalidOperationException("StartsWith 操作只能应用于字符串类型");
        }

        var constant = Expression.Constant(value);
        MethodInfo? method;

        if (ignoreCase)
        {
            var stringComparison = Expression.Constant(StringComparison.OrdinalIgnoreCase);
            method = typeof(string).GetMethod("StartsWith", [typeof(string), typeof(StringComparison)]);
            _body = Expression.Call(_body, method!, constant, stringComparison);
        }
        else
        {
            method = typeof(string).GetMethod("StartsWith", [typeof(string)]);
            _body = Expression.Call(_body, method!, constant);
        }

        return this;
    }

    /// <summary>
    /// 字符串结束于
    /// </summary>
    /// <param name="value">后缀值</param>
    /// <param name="ignoreCase">是否忽略大小写</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> EndsWith(string value, bool ignoreCase = false)
    {
        if (_body?.Type != typeof(string))
        {
            throw new InvalidOperationException("EndsWith 操作只能应用于字符串类型");
        }

        var constant = Expression.Constant(value);
        MethodInfo? method;

        if (ignoreCase)
        {
            var stringComparison = Expression.Constant(StringComparison.OrdinalIgnoreCase);
            method = typeof(string).GetMethod("EndsWith", [typeof(string), typeof(StringComparison)]);
            _body = Expression.Call(_body, method!, constant, stringComparison);
        }
        else
        {
            method = typeof(string).GetMethod("EndsWith", [typeof(string)]);
            _body = Expression.Call(_body, method!, constant);
        }

        return this;
    }

    #endregion

    #region 集合操作

    /// <summary>
    /// 集合包含
    /// </summary>
    /// <param name="values">值集合</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> In<TValue>(IEnumerable<TValue> values)
    {
        var valuesList = values.ToList();
        var constant = Expression.Constant(valuesList);
        var method = typeof(List<TValue>).GetMethod("Contains", [typeof(TValue)]);
        _body = Expression.Call(constant, method!, _body ?? _parameter);
        return this;
    }

    /// <summary>
    /// 集合不包含
    /// </summary>
    /// <param name="values">值集合</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> NotIn<TValue>(IEnumerable<TValue> values)
    {
        var valuesList = values.ToList();
        var constant = Expression.Constant(valuesList);
        var method = typeof(List<TValue>).GetMethod("Contains", [typeof(TValue)]);
        var contains = Expression.Call(constant, method!, _body ?? _parameter);
        _body = Expression.Not(contains);
        return this;
    }

    #endregion

    #region 空值操作

    /// <summary>
    /// 为空检查
    /// </summary>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> IsNull()
    {
        _body = Expression.Equal(_body ?? _parameter, Expression.Constant(null));
        return this;
    }

    /// <summary>
    /// 不为空检查
    /// </summary>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> IsNotNull()
    {
        _body = Expression.NotEqual(_body ?? _parameter, Expression.Constant(null));
        return this;
    }

    /// <summary>
    /// 字符串为空或 null 检查
    /// </summary>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> IsNullOrEmpty()
    {
        if (_body?.Type != typeof(string))
        {
            throw new InvalidOperationException("IsNullOrEmpty 操作只能应用于字符串类型");
        }

        var method = typeof(string).GetMethod("IsNullOrEmpty", [typeof(string)]);
        _body = Expression.Call(null, method!, _body);
        return this;
    }

    /// <summary>
    /// 字符串不为空且不为 null 检查
    /// </summary>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> IsNotNullOrEmpty()
    {
        if (_body?.Type != typeof(string))
        {
            throw new InvalidOperationException("IsNotNullOrEmpty 操作只能应用于字符串类型");
        }

        var method = typeof(string).GetMethod("IsNullOrEmpty", [typeof(string)]);
        var isNullOrEmpty = Expression.Call(null, method!, _body);
        _body = Expression.Not(isNullOrEmpty);
        return this;
    }

    #endregion

    #region 逻辑操作

    /// <summary>
    /// 逻辑与操作
    /// </summary>
    /// <param name="other">另一个表达式构建器</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> And(ExpressionBuilder<T> other)
    {
        var otherExpression = other.Build();
        _body = _body == null ? otherExpression.Body : Expression.AndAlso(_body, otherExpression.Body);
        return this;
    }

    /// <summary>
    /// 逻辑与操作
    /// </summary>
    /// <param name="expression">Lambda 表达式</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> And(Expression<Func<T, bool>> expression)
    {
        var visitor = new ParameterReplacer(_parameter);
        var replacedBody = visitor.Visit(expression.Body);

        _body = _body == null ? replacedBody : Expression.AndAlso(_body, replacedBody);
        return this;
    }

    /// <summary>
    /// 逻辑或操作
    /// </summary>
    /// <param name="other">另一个表达式构建器</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> Or(ExpressionBuilder<T> other)
    {
        var otherExpression = other.Build();
        _body = _body == null ? otherExpression.Body : Expression.OrElse(_body, otherExpression.Body);
        return this;
    }

    /// <summary>
    /// 逻辑或操作
    /// </summary>
    /// <param name="expression">Lambda 表达式</param>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> Or(Expression<Func<T, bool>> expression)
    {
        var visitor = new ParameterReplacer(_parameter);
        var replacedBody = visitor.Visit(expression.Body);

        _body = _body == null ? replacedBody : Expression.OrElse(_body, replacedBody);
        return this;
    }

    /// <summary>
    /// 逻辑非操作
    /// </summary>
    /// <returns>当前构建器实例</returns>
    public ExpressionBuilder<T> Not()
    {
        _body = Expression.Not(_body ?? Expression.Constant(true));
        return this;
    }

    #endregion

    #region 构建方法

    /// <summary>
    /// 构建 Lambda 表达式
    /// </summary>
    /// <returns>Lambda 表达式</returns>
    public Expression<Func<T, bool>> Build()
    {
        return Expression.Lambda<Func<T, bool>>(_body ?? Expression.Constant(true), _parameter);
    }

    /// <summary>
    /// 构建并编译为委托
    /// </summary>
    /// <returns>编译后的委托</returns>
    public Func<T, bool> Compile()
    {
        return Build().Compile();
    }

    #endregion

    #region 静态工厂方法

    /// <summary>
    /// 创建新的表达式构建器
    /// </summary>
    /// <returns>表达式构建器实例</returns>
    public static ExpressionBuilder<T> Create()
    {
        return new ExpressionBuilder<T>();
    }

    /// <summary>
    /// 创建新的表达式构建器
    /// </summary>
    /// <param name="parameterName">参数名称</param>
    /// <returns>表达式构建器实例</returns>
    public static ExpressionBuilder<T> Create(string parameterName)
    {
        return new ExpressionBuilder<T>(parameterName);
    }

    /// <summary>
    /// 从现有表达式创建构建器
    /// </summary>
    /// <param name="expression">现有表达式</param>
    /// <returns>表达式构建器实例</returns>
    public static ExpressionBuilder<T> FromExpression(Expression<Func<T, bool>> expression)
    {
        var builder = new ExpressionBuilder<T>
        {
            _body = expression.Body
        };
        return builder;
    }

    #endregion

    #region 内部类

    /// <summary>
    /// 参数替换访问器
    /// </summary>
    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;

        public ParameterReplacer(ParameterExpression parameter)
        {
            _parameter = parameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node.Type == _parameter.Type ? _parameter : node;
        }
    }

    #endregion
}
