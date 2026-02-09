#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:PageRequestDtoBase
// Guid:aec9055a-15c5-48d1-a835-05a28e11cdf3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/11/27 07:11:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Shared.Paging.Models;

namespace XiHan.Framework.Domain.Shared.Paging.Dtos;

/// <summary>
/// 分页请求基类
/// </summary>
public class PageRequestDtoBase
{
    /// <summary>
    /// 查询条件（描述"查什么"）
    /// </summary>
    public QueryConditions Conditions { get; set; } = new();

    /// <summary>
    /// 查询行为控制（影响查询管道行为）
    /// </summary>
    public QueryBehavior Behavior { get; set; } = new();

    /// <summary>
    /// 分页参数
    /// </summary>
    public PageRequestMetadata Page { get; set; } = new();
}
