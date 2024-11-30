#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SelectConditionDto
// Guid:67b10bd1-1623-4f95-af56-19a45b4390c2
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 6:33:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Utils.DataFilter.Enums;

namespace XiHan.Framework.Utils.DataFilter.Dtos;

/// <summary>
/// 通用选择条件基类
/// </summary>
public class SelectConditionDto
{
    /// <summary>
    /// 构造一个选择字段名称和选择值的选择条件
    /// </summary>
    /// <param name="selectField">字段名称</param>
    /// <param name="criteriaValue">条件值</param>
    public SelectConditionDto(string selectField, object criteriaValue)
    {
        SelectField = selectField;
        CriteriaValue = criteriaValue;
    }

    /// <summary>
    /// 构造一个选择字段名称和选择值的选择条件
    /// </summary>
    /// <param name="selectField">字段名称</param>
    /// <param name="criteriaValue">条件值</param>
    /// <param name="selectCompare">选择比较</param>
    public SelectConditionDto(string selectField, object criteriaValue, SelectCompareEnum selectCompare)
    {
        SelectField = selectField;
        CriteriaValue = criteriaValue;
        SelectCompare = selectCompare;
    }

    /// <summary>
    /// 构造一个选择字段名称和选择值的选择条件
    /// </summary>
    /// <param name="isKeywords">是否关键字</param>
    /// <param name="selectField">字段名称</param>
    /// <param name="criteriaValue">条件值</param>
    /// <param name="selectCompare">选择比较</param>
    public SelectConditionDto(bool isKeywords, string selectField, object criteriaValue, SelectCompareEnum selectCompare)
    {
        IsKeywords = isKeywords;
        SelectField = selectField;
        CriteriaValue = criteriaValue;
        SelectCompare = selectCompare;
    }

    /// <summary>
    /// 是否关键字
    /// </summary>
    public bool IsKeywords { get; set; } = false;

    /// <summary>
    /// 选择字段
    /// </summary>
    public string SelectField { get; set; }

    /// <summary>
    /// 条件值
    /// </summary>
    public object CriteriaValue { get; set; }

    /// <summary>
    /// 选择比较，默认为等于
    /// </summary>
    public SelectCompareEnum SelectCompare { get; set; } = SelectCompareEnum.Equal;
}

/// <summary>
/// 通用选择条件泛型基类
/// </summary>
/// <typeparam name="T">列表元素类型</typeparam>
/// <typeparam name="TV">条件值类型</typeparam>
public class SelectConditionDto<T, TV> : SelectConditionDto
{
    /// <summary>
    /// 使用选择字段名称和选择值，初始化一个<see cref="SelectConditionDto"/>类型的新实例
    /// </summary>
    /// <param name="selectField">字段名称</param>
    /// <param name="criteriaValue">条件值</param>
    public SelectConditionDto(string selectField, TV criteriaValue)
        : base(selectField, criteriaValue!)
    {
        CriteriaValue = criteriaValue;
    }

    /// <summary>
    /// 使用选择字段名称和选择值，初始化一个<see cref="SelectConditionDto"/>类型的新实例
    /// </summary>
    /// <param name="selectField">字段名称</param>
    /// <param name="criteriaValue">条件值</param>
    /// <param name="selectCompare">选择比较</param>
    public SelectConditionDto(string selectField, TV criteriaValue, SelectCompareEnum selectCompare)
        : base(selectField, criteriaValue!, selectCompare)
    {
        CriteriaValue = criteriaValue;
    }

    /// <summary>
    /// 使用选择字段名称和选择值，初始化一个<see cref="SelectConditionDto"/>类型的新实例
    /// </summary>
    /// <param name="isKeywords">是否关键字</param>
    /// <param name="selectField">字段名称</param>
    /// <param name="criteriaValue">条件值</param>
    /// <param name="selectCompare">选择比较</param>
    public SelectConditionDto(bool isKeywords, string selectField, TV criteriaValue, SelectCompareEnum selectCompare)
        : base(isKeywords, selectField, criteriaValue!, selectCompare)
    {
        CriteriaValue = criteriaValue;
    }

    /// <summary>
    /// 字段值（泛型）
    /// </summary>
    public new TV CriteriaValue
    {
        get => (TV)base.CriteriaValue;
        set => base.CriteriaValue = value ??
            throw new ArgumentNullException(nameof(value));
    }
}
