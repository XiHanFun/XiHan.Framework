#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:KeySelector
// Guid:03f8c708-54fe-4b89-9368-97ddbbda5b7a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/28 4:40:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using System.Reflection;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 属性选择器
/// </summary>
public static class KeySelector<T>
{
    /// <summary>
    /// 获取属性选择器
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static Func<T, object> GetKeySelector(string propertyName)
    {
        var propertyInfo = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance) ??
            throw new ArgumentException($"在类型 {typeof(T).Name} 中没有发现属性 {propertyName}。");

        return item =>
        {
            var value = propertyInfo.GetValue(item);
            return value ?? throw new InvalidOperationException($"在类型 {typeof(T).Name} 中的属性 {propertyName} 为空。");
        };
    }

    /// <summary>
    /// 获取属性选择器表达式
    /// </summary>
    /// <param name="propertyName"></param>
    /// <returns></returns>
    public static Expression<Func<T, object>> GetKeySelectorExpression(string propertyName)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyName);
        return Expression.Lambda<Func<T, object>>(Expression.Convert(property, typeof(object)), parameter);
    }

    /// <summary>
    /// 从泛型委托获取属性名
    /// </summary>
    /// <param name="keySelector"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static string GetPropertyName(Expression<Func<T, object>> keySelector)
    {
        return keySelector.Body is MemberExpression memberExpression
            ? memberExpression.Member.Name
            : keySelector.Body is UnaryExpression unaryExpression
            ? ((MemberExpression)unaryExpression.Operand).Member.Name
            : throw new ArgumentException("字段表达式不正确。");
    }

    /// <summary>
    /// 从泛型委托获取属性值
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="keySelector"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static object GetPropertyValue(T obj, Expression<Func<T, object>> keySelector)
    {
        var propertyName = GetPropertyName(keySelector);
        var propertyInfo = typeof(T).GetProperty(propertyName) ??
            throw new ArgumentException($"在类型 {typeof(T).Name} 中没有发现属性 {propertyName}。");

        return propertyInfo.GetValue(obj) ??
            throw new InvalidOperationException($"在类型 {typeof(T).Name} 中的属性 {propertyName} 为空。");
    }
}
