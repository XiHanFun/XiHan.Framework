#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SortCondition
// Guid:e449e1d2-457a-432e-a0f7-5f79cf12c2da
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 06:01:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Linq.Expressions;
using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Dtos;

/// <summary>
/// 排序条件
/// </summary>
public class SortCondition
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SortCondition()
    {
        Field = string.Empty;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="direction">排序方向</param>
    /// <param name="priority">排序优先级（数值越小优先级越高）</param>
    public SortCondition(string field, SortDirection direction = SortDirection.Asc, int priority = 0)
    {
        Field = field;
        Direction = direction;
        Priority = priority;
    }

    /// <summary>
    /// 字段名称
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// 排序方向，默认为升序
    /// </summary>
    public SortDirection Direction { get; set; } = SortDirection.Asc;

    /// <summary>
    /// 排序优先级，数值越小优先级越高，默认为0
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 创建升序排序条件
    /// </summary>
    public static SortCondition Ascending(string field, int priority = 0) =>
        new(field, SortDirection.Asc, priority);

    /// <summary>
    /// 创建降序排序条件
    /// </summary>
    public static SortCondition Descending(string field, int priority = 0) =>
        new(field, SortDirection.Desc, priority);
}

/// <summary>
/// 泛型排序条件（支持 Lambda 表达式）
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public class SortCondition<T> : SortCondition
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public SortCondition() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="direction">排序方向</param>
    /// <param name="priority">排序优先级</param>
    public SortCondition(string field, SortDirection direction = SortDirection.Asc, int priority = 0)
        : base(field, direction, priority)
    {
    }

    /// <summary>
    /// 构造函数（使用 Lambda 表达式）
    /// </summary>
    /// <param name="selector">属性选择器</param>
    /// <param name="direction">排序方向</param>
    /// <param name="priority">排序优先级</param>
    public SortCondition(Expression<Func<T, object>> selector, SortDirection direction = SortDirection.Asc, int priority = 0)
    {
        Field = GetPropertyName(selector);
        Direction = direction;
        Priority = priority;
    }

    /// <summary>
    /// 创建升序排序条件
    /// </summary>
    public static SortCondition<T> Ascending(Expression<Func<T, object>> selector, int priority = 0) =>
        new(selector, SortDirection.Asc, priority);

    /// <summary>
    /// 创建降序排序条件
    /// </summary>
    public static SortCondition<T> Descending(Expression<Func<T, object>> selector, int priority = 0) =>
        new(selector, SortDirection.Desc, priority);

    /// <summary>
    /// 获取属性名称
    /// </summary>
    /// <param name="selector">属性选择器</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private static string GetPropertyName(Expression<Func<T, object>> selector)
    {
        if (selector.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        if (selector.Body is UnaryExpression { Operand: MemberExpression unaryMember })
        {
            return unaryMember.Member.Name;
        }

        throw new ArgumentException("无效的属性选择器", nameof(selector));
    }
}
