#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:TenantEntityBase
// Guid:01fa143e-06f6-4ce4-a29b-bd2ee3a440b4
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/2/20 2:51:24
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.MultiTenancy.Entities.Abstracts;

namespace XiHan.Framework.MultiTenancy.Entities;

/// <summary>
/// 租户实体基类
/// </summary>
public abstract class TenantEntityBase : EntityBase, IMultiTenant
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// 泛型主键租户实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class TenantEntityBase<TKey> : EntityBase<TKey>, IMultiTenant
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}
