﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SortConditionDto
// Guid:e449e1d2-457a-432e-a0f7-5f79cf12c2da
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 6:01:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Utils.DataFilter.Enums;

namespace XiHan.Framework.Utils.DataFilter;

/// <summary>
/// 通用排序条件基类
/// </summary>
public class SortConditionDto
{
    /// <summary>
    /// 排序字段名称
    /// </summary>
    public string SortField { get; set; }

    /// <summary>
    /// 排序方向
    /// </summary>
    public SortDirectionEnum SortDirection { get; set; }

    /// <summary>
    /// 构造一个排序字段名称和排序方式的排序条件
    /// </summary>
    /// <param name="sortField">字段名称</param>
    /// <param name="sortDirection">排序方式</param>
    public SortConditionDto(string sortField, SortDirectionEnum sortDirection = SortDirectionEnum.Asc)
    {
        SortField = sortField;
        SortDirection = sortDirection;
    }
}

/// <summary>
/// 通用排序条件泛型基类
/// </summary>
/// <typeparam name="T">列表元素类型</typeparam>
public class SortConditionDto<T> : SortConditionDto
{
    /// <summary>
    /// 使用排序字段与排序方式 初始化一个<see cref="SortConditionDto"/>类型的新实例
    /// </summary>
    public SortConditionDto(Expression<Func<T, object>> keySelector, SortDirectionEnum sortDirection = SortDirectionEnum.Asc) : base(GetPropertyName(keySelector), sortDirection)
    {
    }

    /// <summary>
    /// 从泛型委托获取属性名
    /// </summary>
    private static string GetPropertyName(Expression<Func<T, object>> keySelector)
    {
        var param = keySelector.Parameters.First().Name ?? throw new ArgumentException("参数不能为空", nameof(keySelector));

        string operand = ((dynamic)keySelector.Body).Operand.ToString();
        operand = operand.Substring(param.Length + 1, operand.Length - param.Length - 1);
        return operand;
    }
}