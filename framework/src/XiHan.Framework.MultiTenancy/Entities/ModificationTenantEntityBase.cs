#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ModificationTenantEntityBase
// Guid:0d428316-97ce-4820-926b-f07813a3a305
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/13 3:07:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Domain.Entities;
using XiHan.Framework.MultiTenancy.Entities.Abstracts;

namespace XiHan.Framework.MultiTenancy.Entities;

/// <summary>
/// 修改审计租户实体基类
/// </summary>
public abstract class ModificationTenantEntityBase : ModificationEntityBase, IMultiTenant
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// 修改审计租户实体基类（带修改者）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class ModificationTenantEntityBase<TKey> : ModificationEntityBase<TKey>, IMultiTenant
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 租户唯一标识
    /// </summary>
    public Guid? TenantId { get; set; }
}
