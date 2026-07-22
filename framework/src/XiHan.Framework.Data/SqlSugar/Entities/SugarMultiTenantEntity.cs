// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Domain.Entities.Abstracts;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 多租户实体基类（泛型主键）
/// </summary>
/// <remarks>
/// 必须实现 <see cref="IMultiTenantEntity"/>：全局租户 QueryFilter 经 <c>AddTableFilter&lt;IMultiTenantEntity&gt;</c> 注册，
/// SqlSugar 仅对可赋值给该接口的实体套用；只声明 <c>TenantId</c> 列而不实现接口，会使租户行过滤对本实体静默失效。
/// </remarks>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarMultiTenantEntity<TKey> : SugarEntity<TKey>, IMultiTenantEntity
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarMultiTenantEntity() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected SugarMultiTenantEntity(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 租户ID（0=平台租户；业务租户从 1 开始；非空确保 UNIQUE(TenantId, XxCode) 等复合唯一索引对全局记录生效）
    /// </summary>
    [SugarColumn(ColumnName = "Tenant_Id", ColumnDescription = "租户ID", IsOnlyIgnoreUpdate = true)]
    public virtual long TenantId { get; set; }
}
