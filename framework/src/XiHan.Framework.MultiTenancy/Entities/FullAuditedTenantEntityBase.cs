#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FullAuditedTenantEntityBase
// Guid:stu12345-1234-1234-1234-123456789stu
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/12 16:42:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.MultiTenancy.Entities.Abstracts;

namespace XiHan.Framework.MultiTenancy.Entities;

/// <summary>
/// 完整审计租户实体基类
/// </summary>
public abstract class FullAuditedTenantEntityBase : FullAuditedEntityBase, IMultiTenant
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// 完整审计租户实体基类（带用户）
/// 包含创建、修改、删除的所有审计信息和对应的用户唯一标识
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class FullAuditedTenantEntityBase<TKey> : FullAuditedEntityBase<TKey>, IMultiTenant
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}
