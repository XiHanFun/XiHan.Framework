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

using System.Linq.Expressions;
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
    /// <param name="value">字段值</param>
    /// <param name="selectCompare">选择比较</param>
    public SelectConditionDto(string selectField, object value, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
    {
        SelectField = selectField;
        Value = value;
        SelectCompare = selectCompare;
    }

    /// <summary>
    /// 选择字段
    /// </summary>
    public string SelectField { get; set; } = string.Empty;

    /// <summary>
    /// 字段值
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// 选择比较
    /// </summary>
    public SelectCompareEnum SelectCompare { get; set; }
}

/// <summary>
/// 通用选择条件泛型基类
/// </summary>
/// <typeparam name="T">列表元素类型</typeparam>
public class SelectConditionDto<T> : SelectConditionDto
{
    /// <summary>
    /// 使用选择字段名称和选择值，初始化一个<see cref="SelectConditionDto"/>类型的新实例
    /// </summary>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="tObject">泛型对象</param>
    /// <param name="selectCompare">选择比较</param>
    public SelectConditionDto(Expression<Func<T, object>> keySelector, T tObject, SelectCompareEnum selectCompare = SelectCompareEnum.Equal)
        : base(KeySelector<T>.GetPropertyName(keySelector), KeySelector<T>.GetPropertyValue(tObject, keySelector), selectCompare)
    {
    }
}
