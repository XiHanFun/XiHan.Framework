#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QuerySort
// Guid:e449e1d2-457a-432e-a0f7-5f79cf12c2da
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 06:01:47
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Models;

/// <summary>
/// 查询排序条件
/// </summary>
public sealed class QuerySort
{
    private string _field = string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    public QuerySort()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="field">字段名称</param>
    /// <param name="direction">排序方向</param>
    /// <param name="priority">排序优先级</param>
    public QuerySort(string field, SortDirection direction = SortDirection.Ascending, int priority = 0)
    {
        Field = field;
        Direction = direction;
        Priority = priority;
    }

    /// <summary>
    /// 字段名称
    /// </summary>
    public string Field
    {
        get => _field;
        set => _field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("字段名称不能为空", nameof(Field))
            : value.Trim();
    }

    /// <summary>
    /// 排序方向，默认为升序
    /// </summary>
    public SortDirection Direction { get; set; } = SortDirection.Ascending;

    /// <summary>
    /// 排序优先级，数值越小优先级越高，默认为0
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 创建升序排序
    /// </summary>
    public static QuerySort Ascending(string field, int priority = 0) => new(field, SortDirection.Ascending, priority);

    /// <summary>
    /// 创建降序排序
    /// </summary>
    public static QuerySort Descending(string field, int priority = 0) => new(field, SortDirection.Descending, priority);

    /// <summary>
    /// 验证排序条件是否有效
    /// </summary>
    public bool IsValid() => !string.IsNullOrWhiteSpace(Field);
}
