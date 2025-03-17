#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SortConditionDto
// Guid:e449e1d2-457a-432e-a0f7-5f79cf12c2da
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 6:01:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Utils.DataFilter.Paging.Enums;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.DataFilter.Paging.Dtos;

/// <summary>
/// 通用排序条件基类
/// </summary>
[Serializable]
public class SortConditionDto
{
    /// <summary>
    /// 构造一个排序字段名称和排序方式的排序条件
    /// </summary>
    /// <param name="sortField">字段名称</param>
    public SortConditionDto(string sortField)
    {
        SortField = sortField;
    }

    /// <summary>
    /// 构造一个排序字段名称和排序方式的排序条件
    /// </summary>
    /// <param name="sortField">字段名称</param>
    /// <param name="sortPriority">排序优先级</param>
    public SortConditionDto(string sortField, int sortPriority)
    {
        SortField = sortField;
        SortPriority = sortPriority;
    }

    /// <summary>
    /// 构造一个排序字段名称和排序方式的排序条件
    /// </summary>
    /// <param name="sortField">字段名称</param>
    /// <param name="sortDirection">排序方式</param>
    public SortConditionDto(string sortField, SortDirectionEnum sortDirection)
    {
        SortField = sortField;
        SortDirection = sortDirection;
    }

    /// <summary>
    /// 构造一个排序字段名称和排序方式的排序条件
    /// </summary>
    /// <param name="sortField">字段名称</param>
    /// <param name="sortPriority">排序优先级</param>
    /// <param name="sortDirection">排序方式</param>
    public SortConditionDto(string sortField, int sortPriority, SortDirectionEnum sortDirection)
    {
        SortField = sortField;
        SortPriority = sortPriority;
        SortDirection = sortDirection;
    }

    /// <summary>
    /// 排序字段名称
    /// </summary>
    public string SortField { get; set; } = string.Empty;

    /// <summary>
    /// 排序优先级，数值越小优先级越高，默认为0，即最高优先级
    /// </summary>
    public int SortPriority { get; set; } = 0;

    /// <summary>
    /// 排序方向，默认为升序
    /// </summary>
    public SortDirectionEnum SortDirection { get; set; } = SortDirectionEnum.Asc;
}

/// <summary>
/// 通用排序条件泛型基类
/// </summary>
/// <typeparam name="T">列表元素类型</typeparam>
[Serializable]
public class SortConditionDto<T> : SortConditionDto
{
    /// <summary>
    /// 使用排序字段与排序方式，初始化一个<see cref="SortConditionDto"/>类型的新实例
    /// </summary>
    /// <param name="keySelector">属性选择器</param>
    public SortConditionDto(Expression<Func<T, object>> keySelector)
        : base(keySelector.GetPropertyName())
    {
    }

    /// <summary>
    /// 使用排序字段与排序方式，初始化一个<see cref="SortConditionDto"/>类型的新实例
    /// </summary>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="sortPriority">排序优先级</param>
    public SortConditionDto(Expression<Func<T, object>> keySelector, int sortPriority)
        : base(keySelector.GetPropertyName(), sortPriority)
    {
    }

    /// <summary>
    /// 使用排序字段与排序方式，初始化一个<see cref="SortConditionDto"/>类型的新实例
    /// </summary>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="sortDirection">排序方式</param>
    public SortConditionDto(Expression<Func<T, object>> keySelector, SortDirectionEnum sortDirection)
        : base(keySelector.GetPropertyName(), sortDirection)
    {
    }

    /// <summary>
    /// 使用排序字段与排序方式，初始化一个<see cref="SortConditionDto"/>类型的新实例
    /// </summary>
    /// <param name="keySelector">属性选择器</param>
    /// <param name="sortPriority">排序优先级</param>
    /// <param name="sortDirection">排序方式</param>
    public SortConditionDto(Expression<Func<T, object>> keySelector, int sortPriority, SortDirectionEnum sortDirection)
        : base(keySelector.GetPropertyName(), sortPriority, sortDirection)
    {
    }
}
