#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarMultiTenantAggregateRoot
// Guid:12a49409-f41d-4ddd-9142-2f7c573caa0d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Aggregates;

/// <summary>
/// SqlSugar 多租户聚合根基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
[SugarIndex("IX_{table}_TenantId", nameof(TenantId), OrderByType.Asc)]
public abstract class SugarMultiTenantAggregateRoot<TKey> : SugarAggregateRoot<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarMultiTenantAggregateRoot() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected SugarMultiTenantAggregateRoot(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 租户ID
    /// </summary>
    [SugarColumn(ColumnDescription = "租户ID", IsNullable = true)]
    public virtual long? TenantId { get; set; }
}
