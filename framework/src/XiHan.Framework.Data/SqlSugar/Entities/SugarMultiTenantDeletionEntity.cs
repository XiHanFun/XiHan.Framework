#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarMultiTenantDeletionEntity
// Guid:6332a4fe-5086-41ee-8e3d-bb9e359bb4fb
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 多租户删除审计实体基类
/// </summary>
/// <remarks>
/// 必须实现 <see cref="IMultiTenantEntity"/>：全局租户 QueryFilter 经 <c>AddTableFilter&lt;IMultiTenantEntity&gt;</c> 注册，
/// SqlSugar 仅对可赋值给该接口的实体套用；只声明 <c>TenantId</c> 列而不实现接口，会使租户行过滤对本实体静默失效。
/// </remarks>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarMultiTenantDeletionEntity<TKey> : SugarDeletionEntity<TKey>, IMultiTenantEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarMultiTenantDeletionEntity() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected SugarMultiTenantDeletionEntity(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="deletedId">删除者ID</param>
    protected SugarMultiTenantDeletionEntity(TKey basicId, TKey deletedId) : base(basicId, deletedId)
    {
    }

    /// <summary>
    /// 租户ID（0=平台租户；业务租户从 1 开始）
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", ColumnDescription = "租户ID", IsOnlyIgnoreUpdate = true)]
    public virtual long TenantId { get; set; }
}
