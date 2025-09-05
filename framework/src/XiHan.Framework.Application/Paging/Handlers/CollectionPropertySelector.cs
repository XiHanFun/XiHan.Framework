#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CollectionPropertySelector
// Guid:55cd8449-d498-4e81-b086-96fd9bd9dc1e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/28 4:32:39
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Application.Paging.Dtos;
using XiHan.Framework.Application.Paging.Enums;

namespace XiHan.Framework.Application.Paging.Handlers;

/// <summary>
/// 集合属性选择器
/// </summary>
public static class CollectionPropertySelector<T>
{
    #region IEnumerable

    /// <summary>
    /// 对 IEnumerable 集合根据属性名称进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="criteriaValue">条件值</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, string propertyName, object? criteriaValue, SelectCompare selectCompare)
    {
        var predicate = SelectConditionParser<T>.GetSelectConditionParser(propertyName, criteriaValue, selectCompare);
        return source.Where(predicate.Compile());
    }

    /// <summary>
    /// 对 IEnumerable 集合根据选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectCondition">选择条件</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, SelectCondition selectCondition)
    {
        var predicate = SelectConditionParser<T>.GetSelectConditionParser(selectCondition);
        return source.Where(predicate.Compile());
    }

    /// <summary>
    /// 对 IEnumerable 集合根据选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectCondition">选择条件</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, SelectConditionDto<T> selectCondition)
    {
        var predicate = SelectConditionParser<T>.GetSelectConditionParser(selectCondition);
        return source.Where(predicate.Compile());
    }

    /// <summary>
    /// 对 IEnumerable 集合根据多选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectConditions">多选择条件</param>
    /// <returns>排序后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, IEnumerable<SelectCondition> selectConditions)
    {
        foreach (var selectCondition in selectConditions)
        {
            var predicate = SelectConditionParser<T>.GetSelectConditionParser(selectCondition);
            source = source.Where(predicate.Compile());
        }

        return source;
    }

    /// <summary>
    /// 对 IEnumerable 集合根据多选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectConditions">多选择条件</param>
    /// <returns>排序后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, IEnumerable<SelectConditionDto<T>> selectConditions)
    {
        foreach (var selectCondition in selectConditions)
        {
            var predicate = SelectConditionParser<T>.GetSelectConditionParser(selectCondition);
            source = source.Where(predicate.Compile());
        }

        return source;
    }

    #endregion IEnumerable

    #region IQueryable

    /// <summary>
    /// 对 IQueryable 集合根据属性名称进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="criteriaValue">条件值</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, string propertyName, object? criteriaValue, SelectCompare selectCompare)
    {
        var predicate = SelectConditionParser<T>.GetSelectConditionParser(propertyName, criteriaValue, selectCompare);
        return source.Where(predicate);
    }

    /// <summary>
    /// 对 IQueryable 集合根据选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectCondition">选择条件</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, SelectCondition selectCondition)
    {
        var predicate = SelectConditionParser<T>.GetSelectConditionParser(selectCondition);
        return source.Where(predicate);
    }

    /// <summary>
    /// 对 IQueryable 集合根据选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectCondition">选择条件</param>
    /// <returns>选择后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, SelectConditionDto<T> selectCondition)
    {
        var predicate = SelectConditionParser<T>.GetSelectConditionParser(selectCondition);
        return source.Where(predicate);
    }

    /// <summary>
    /// 对 IQueryable 集合根据多选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectConditions">多选择条件</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, IEnumerable<SelectCondition> selectConditions)
    {
        foreach (var selectCondition in selectConditions)
        {
            var predicate = SelectConditionParser<T>.GetSelectConditionParser(selectCondition);
            source = source.Where(predicate);
        }

        return source;
    }

    /// <summary>
    /// 对 IQueryable 集合根据多选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectConditions">多选择条件</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, IEnumerable<SelectConditionDto<T>> selectConditions)
    {
        foreach (var selectCondition in selectConditions)
        {
            var predicate = SelectConditionParser<T>.GetSelectConditionParser(selectCondition);
            source = source.Where(predicate);
        }

        return source;
    }

    #endregion IQueryable
}
