#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MultiTenantDeletionEntityBase
// Guid:4dd69dfd-0ec6-4fe8-9f3d-57cc02ba3ad3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 多租户删除审计实体基类（泛型主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class MultiTenantDeletionEntityBase<TKey> : DeletionEntityBase<TKey>, IMultiTenantEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 租户ID
    /// </summary>
    public virtual long? TenantId { get; set; }
}
