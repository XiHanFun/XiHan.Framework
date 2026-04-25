#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MultiTenantAggregateRootBase
// Guid:52292b9e-4887-4b4b-aae2-f6de551b0c97
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Aggregates;

/// <summary>
/// 多租户聚合根基类（泛型主键）
/// </summary>
/// <remarks>
/// 约定：TenantId 非空（long），默认值 0。
/// - 0 代表平台租户（全局实体统一使用），业务租户从 1 开始分配
/// - 实现 IMultiTenantEntity 以确保全局 QueryFilter 能命中聚合根
/// </remarks>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class MultiTenantAggregateRootBase<TKey> : AggregateRootBase<TKey>, IMultiTenantEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 租户ID（0=平台租户；业务租户从 1 开始）
    /// </summary>
    public virtual long TenantId { get; set; }
}
