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

using XiHan.Framework.Utils.DataFilter.Dtos;
using XiHan.Framework.Utils.DataFilter.Enums;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 集合属性选择器
/// </summary>
public static class CollectionPropertySelector<T>
{
    #region IEnumerable

    /// <summary>
    /// 对 IEnumerable 集合根据键名称进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keyName">键名称</param>
    /// <param name="criteriaValue">条件值</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, string keyName, object criteriaValue, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var predicate = ExpressionParser<T>.Parse(keyName, criteriaValue, selectCompare);
        return source.Where(predicate.Compile());
    }

    /// <summary>
    /// 对 IEnumerable 集合根据选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectCondition">选择条件</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, SelectConditionDto selectCondition)
    {
        //TODO: 判断是否为关键字，如果是关键字，用 SelectCompareEnum.Equal 或 SelectCompareEnum.Contains 比较
        var predicate = ExpressionParser<T>.Parse(selectCondition.SelectField, selectCondition.CriteriaValue, selectCondition.SelectCompare);
        return source.Where(predicate.Compile());
    }

    /// <summary>
    /// 对 IEnumerable 集合根据选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectCondition">选择条件</param>
    /// <returns>选择后的数据</returns>
    public static IEnumerable<T> Where<TV>(IEnumerable<T> source, SelectConditionDto<T, TV> selectCondition)
    {
        //TODO: 判断是否为关键字，如果是关键字，用 SelectCompareEnum.Equal 或 SelectCompareEnum.Contains 比较
        var predicate = ExpressionParser<T>.Parse(selectCondition.SelectField, selectCondition.CriteriaValue, selectCondition.SelectCompare);
        return source.Where(predicate.Compile());
    }

    /// <summary>
    /// 对 IEnumerable 集合根据多选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectConditions">多选择条件</param>
    /// <returns>排序后的数据</returns>
    public static IEnumerable<T> Where(IEnumerable<T> source, IEnumerable<SelectConditionDto> selectConditions)
    {
        foreach (var selectCondition in selectConditions)
        {
            var predicate = ExpressionParser<T>.Parse(selectCondition.SelectField, selectCondition.CriteriaValue, selectCondition.SelectCompare);
            source = source.Where(predicate.Compile());
        }
        return source;
    }

    #endregion

    #region IQueryable

    /// <summary>
    /// 对 IQueryable 集合根据键名称进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="keyName">键名称</param>
    /// <param name="criteriaValue">条件值</param>
    /// <param name="selectCompare">选择比较</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, string keyName, object criteriaValue, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        var predicate = ExpressionParser<T>.Parse(keyName, criteriaValue, selectCompare);
        return source.Where(predicate);
    }

    /// <summary>
    /// 对 IQueryable 集合根据选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectCondition">选择条件</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, SelectConditionDto selectCondition)
    {
        //TODO: 判断是否为关键字，如果是关键字，用 SelectCompareEnum.Equal 或 SelectCompareEnum.Contains 比较
        var predicate = ExpressionParser<T>.Parse(selectCondition.SelectField, selectCondition.CriteriaValue, selectCondition.SelectCompare);
        return source.Where(predicate);
    }

    /// <summary>
    /// 对 IQueryable 集合根据多选择条件进行选择
    /// </summary>
    /// <param name="source">原始数据源</param>
    /// <param name="selectConditions">多选择条件</param>
    /// <returns>排序后的数据</returns>
    public static IQueryable<T> Where(IQueryable<T> source, IEnumerable<SelectConditionDto> selectConditions)
    {
        foreach (var selectCondition in selectConditions)
        {
            var predicate = ExpressionParser<T>.Parse(selectCondition.SelectField, selectCondition.CriteriaValue, selectCondition.SelectCompare);
            source = source.Where(predicate);
        }
        return source;
    }

    #endregion
}
