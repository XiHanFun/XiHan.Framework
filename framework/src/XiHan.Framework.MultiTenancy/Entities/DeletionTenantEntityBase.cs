#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DeletionTenantEntityBase
// Guid:pqr12345-1234-1234-1234-123456789pqr
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:40:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.MultiTenancy.Entities.Abstracts;

namespace XiHan.Framework.MultiTenancy.Entities;

/// <summary>
/// 删除审计租户实体基类
/// </summary>
public abstract class DeletionTenantEntityBase : DeletionEntityBase, IMultiTenant
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// 删除审计租户实体基类（带删除者）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class DeletionTenantEntityBase<TKey> : DeletionEntityBase<TKey>, IMultiTenant
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}
