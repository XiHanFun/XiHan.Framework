#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CollectionPropertySelector
// Guid:55cd8449-d498-4e81-b086-96fd9bd9dc1e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/28 4:32:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Utils.DataFilter.Enums;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 集合属性选择器
/// </summary>
public static class CollectionPropertySelector<T>
{
    /// <summary>
    /// 默认参数表达式
    /// </summary>
    public static ParameterExpression Parameter { get; } = Expression.Parameter(typeof(T), "x");

    /// <summary>
    /// 根据选择比较枚举获取属性比较器
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    /// <param name="compareEnum">选择比较枚举</param>
    /// <param name="value">比较值</param>
    /// <returns>属性比较器表达式</returns>
    public static Expression<Func<T, bool>> GetPropertyComparator(string propertyName, SelectCompareEnum compareEnum, object value)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, propertyName);
        var constant = Expression.Constant(value);

        Expression comparison = compareEnum switch
        {
            SelectCompareEnum.Contains => Expression.Call(property, typeof(string).GetMethod("Contains", [typeof(string)])!, constant),
            SelectCompareEnum.Equal => Expression.Equal(property, constant),
            SelectCompareEnum.Greater => Expression.GreaterThan(property, constant),
            SelectCompareEnum.GreaterEqual => Expression.GreaterThanOrEqual(property, constant),
            SelectCompareEnum.Less => Expression.LessThan(property, constant),
            SelectCompareEnum.LessEqual => Expression.LessThanOrEqual(property, constant),
            SelectCompareEnum.NotEqual => Expression.NotEqual(property, constant),
            _ => throw new NotSupportedException($"SelectCompareEnum '{compareEnum}' is not supported.")
        };

        return Expression.Lambda<Func<T, bool>>(comparison, parameter);
    }
}
