#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:QueryBehavior
// Guid:f9a6b8c7-5d4e-3f2a-1b0c-9e8d7f6a5b4c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/2/10 7:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Domain.Shared.Paging.Models;

/// <summary>
/// 查询行为控制（只影响 Queryable 管道，永远不允许进入 Expression）
/// </summary>
public sealed class QueryBehavior
{
    /// <summary>
    /// 是否禁用分页（返回所有数据）
    /// </summary>
    public bool DisablePaging { get; set; } = false;

    /// <summary>
    /// 是否禁用默认排序
    /// </summary>
    public bool DisableDefaultSort { get; set; } = false;

    /// <summary>
    /// 是否忽略多租户过滤
    /// </summary>
    public bool IgnoreTenant { get; set; } = false;

    /// <summary>
    /// 是否忽略软删除过滤
    /// </summary>
    public bool IgnoreSoftDelete { get; set; } = false;

    /// <summary>
    /// 是否启用查询追踪（EF Core）
    /// </summary>
    public bool EnableTracking { get; set; } = false;

    /// <summary>
    /// 是否启用拆分查询（避免笛卡尔积）
    /// </summary>
    public bool EnableSplitQuery { get; set; } = false;

    /// <summary>
    /// 查询超时时间（秒）
    /// </summary>
    public int? QueryTimeout { get; set; }

    /// <summary>
    /// 是否使用默认配置（所有选项都是默认值）
    /// </summary>
    public bool IsDefault =>
        DisablePaging == false &&
        DisableDefaultSort == false &&
        IgnoreTenant == false &&
        IgnoreSoftDelete == false &&
        EnableTracking == false &&
        EnableSplitQuery == false &&
        QueryTimeout is null;

    /// <summary>
    /// 克隆当前查询行为
    /// </summary>
    public QueryBehavior Clone()
    {
        return new QueryBehavior
        {
            DisablePaging = DisablePaging,
            DisableDefaultSort = DisableDefaultSort,
            IgnoreTenant = IgnoreTenant,
            IgnoreSoftDelete = IgnoreSoftDelete,
            EnableTracking = EnableTracking,
            EnableSplitQuery = EnableSplitQuery,
            QueryTimeout = QueryTimeout
        };
    }
}
