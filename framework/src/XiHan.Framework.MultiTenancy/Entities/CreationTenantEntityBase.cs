#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CreationTenantEntityBase
// Guid:d3c3c3ee-6719-4e6b-8a92-c54dda3937c3
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/13 3:01:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.MultiTenancy.Entities.Abstracts;

namespace XiHan.Framework.MultiTenancy.Entities;

/// <summary>
/// 创建审计租户实体基类
/// </summary>
public abstract class CreationTenantEntityBase : CreationEntityBase, IMultiTenant
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// 创建审计租户实体基类（带创建者）
/// </summary>
public abstract class CreationTenantEntityBase<TKey> : CreationEntityBase<TKey>, IMultiTenant
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}
