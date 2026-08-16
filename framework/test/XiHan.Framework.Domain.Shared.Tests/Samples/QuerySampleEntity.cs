// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Domain.Shared.Paging.Attributes;
using XiHan.Framework.Domain.Shared.Paging.Enums;

namespace XiHan.Framework.Domain.Shared.Tests.Samples;

/// <summary>
/// 查询样例实体，覆盖查询字段、操作符限制与关键字搜索等特性配置
/// </summary>
public class QuerySampleEntity
{
    /// <summary>
    /// 带别名的字符串字段，允许过滤与排序
    /// </summary>
    [QueryField(Alias = "userName", AllowFilter = true, AllowSort = true, Priority = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 不允许过滤的整数字段
    /// </summary>
    [QueryField(AllowFilter = false)]
    public int Age { get; set; }

    /// <summary>
    /// 不允许排序的日期字段
    /// </summary>
    [QueryField(AllowSort = false)]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 仅支持等于与包含操作符的字符串字段
    /// </summary>
    [QueryOperatorSupport(QueryOperator.Equal, QueryOperator.Contains)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 前缀匹配的关键字搜索字段
    /// </summary>
    [KeywordSearch(KeywordMatchMode.StartsWith, Priority = 1)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 无特性配置的布尔字段
    /// </summary>
    public bool IsActive { get; set; }
}
