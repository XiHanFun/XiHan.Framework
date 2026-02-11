#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryFieldAttribute
// Guid:982ded57-6737-4b61-9df9-d5b3b6f2192a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 13:04:45
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Shared.Paging.Attributes;

/// <summary>
/// 查询字段属性
/// </summary>
/// <remarks>
/// 用于标记实体属性是否可以进行查询过滤和排序
/// 如需配置关键字搜索，请使用 <see cref="KeywordSearchAttribute"/>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class QueryFieldAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public QueryFieldAttribute()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="alias">字段别名</param>
    public QueryFieldAttribute(string alias)
    {
        Alias = alias;
    }

    /// <summary>
    /// 别名（用于映射前端传入的字段名到实际属性名）
    /// </summary>
    public string Alias { get; init; } = string.Empty;

    /// <summary>
    /// 是否允许过滤
    /// </summary>
    public bool AllowFilter { get; init; } = true;

    /// <summary>
    /// 是否允许排序
    /// </summary>
    public bool AllowSort { get; init; } = true;

    /// <summary>
    /// 过滤条件的优先级（数值越小优先级越高）
    /// </summary>
    public int Priority { get; init; } = 0;
}
