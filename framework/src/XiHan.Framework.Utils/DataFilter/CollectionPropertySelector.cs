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
using XiHan.Framework.Utils.System;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 集合属性选择器
/// </summary>
public static class CollectionPropertySelector<T>
{
    /// <summary>
    /// 对 IEnumerable 集合根据属性名称进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="propertyValue">比较值</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, string propertyName, object propertyValue, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var keySelector = KeySelector<T>.GetKeySelectorExpression(propertyName);
        return Where(source, keySelector, propertyValue, selectCompare);
    }

    /// <summary>
    /// 对 IEnumerable 集合进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, Expression<Func<T, object>> keySelector, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var propertyName = ((MemberExpression)keySelector.Body).Member.Name;
        var propertyValue = ((MemberExpression)keySelector.Body).Member.GetPropertyValue(propertyName);
        var predicate = ExpressionParser<T>.Parse(propertyName, selectCompare, keySelector.Compile());
        return source.Where(predicate.Compile());
    }

    /// <summary>
    /// 对 IQueryable 集合根据属性名称进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, string propertyName, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var keySelector = KeySelector<T>.GetKeySelectorExpression(propertyName);
        return Where(source, keySelector, selectCompare);
    }

    /// <summary>
    /// 对 IQueryable 集合进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, Expression<Func<T, object>> keySelector, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var propertyName = ((MemberExpression)keySelector.Body).Member.Name;
        var predicate = ExpressionParser<T>.Parse(propertyName, selectCompare, keySelector.Compile());
        return source.Where(predicate);
    }
}
