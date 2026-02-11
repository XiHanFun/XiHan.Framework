#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MultiTenantEntityBase
// Guid:3ac2e5e0-da45-452b-8c7a-61f8f50edaf3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Domain.Entities;

/// <summary>
/// 多租户实体基类（泛型主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class MultiTenantEntityBase<TKey> : EntityBase<TKey>, IMultiTenantEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 租户ID
    /// </summary>
    public virtual long? TenantId { get; set; }
}
