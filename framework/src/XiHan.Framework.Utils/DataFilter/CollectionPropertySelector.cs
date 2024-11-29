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
    /// 对 IEnumerable 集合根据属性名称进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="propertyValue">比较值</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, string propertyName, object propertyValue, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var predicate = ExpressionParser<T>.Parse(propertyName, propertyValue, selectCompare);
        return source.Where(predicate.Compile());
    }

    /// <summary>
    /// 对 IEnumerable 集合进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="tObject">泛型对象</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, Expression<Func<T, object>> keySelector, T tObject, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var propertyName = KeySelector<T>.GetPropertyName(keySelector);
        var propertyValue = KeySelector<T>.GetPropertyValue(tObject, keySelector);
        return Where(source, propertyName, propertyValue, selectCompare);
    }

    /// <summary>
    /// 对 IQueryable 集合根据属性名称进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="propertyValue">比较值</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, string propertyName, object propertyValue, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var predicate = ExpressionParser<T>.Parse(propertyName, propertyValue, selectCompare);
        return source.Where(predicate);
    }

    /// <summary>
    /// 对 IQueryable 集合进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="tObject">泛型对象</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, Expression<Func<T, object>> keySelector, T tObject, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var propertyName = KeySelector<T>.GetPropertyName(keySelector);
        var propertyValue = KeySelector<T>.GetPropertyValue(tObject, keySelector);
        return Where(source, propertyName, propertyValue, selectCompare);
    }
}
