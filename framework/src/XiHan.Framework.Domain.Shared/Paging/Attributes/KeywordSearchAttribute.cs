#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:KeywordSearchAttribute
// Guid:882955c6-a490-4153-ac37-157c69824fa3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/02 13:08:50
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Paging.Attributes;

/// <summary>
/// 关键字搜索属性
/// </summary>
/// <remarks>
/// 用于标记实体属性是否参与关键字搜索以及搜索方式
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class KeywordSearchAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public KeywordSearchAttribute()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="matchMode">匹配方式</param>
    public KeywordSearchAttribute(KeywordMatchMode matchMode)
    {
        MatchMode = matchMode;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="matchMode">匹配方式</param>
    /// <param name="priority">搜索优先级</param>
    public KeywordSearchAttribute(KeywordMatchMode matchMode, int priority)
    {
        MatchMode = matchMode;
        Priority = priority;
    }

    /// <summary>
    /// 是否启用关键字搜索
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 匹配方式，默认包含匹配
    /// </summary>
    public KeywordMatchMode MatchMode { get; init; } = KeywordMatchMode.Contains;

    /// <summary>
    /// 搜索优先级（数值越小优先级越高）
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// 是否参与"默认关键字搜索"（当未指定搜索字段时）
    /// </summary>
    public bool IncludeInDefault { get; init; } = true;

    /// <summary>
    /// 搜索字段别名（用于前端指定搜索字段）
    /// </summary>
    public string Alias { get; init; } = string.Empty;
}
