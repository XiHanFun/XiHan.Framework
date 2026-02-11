#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarMultiTenantEntityWithIdentity
// Guid:80f4f02b-1f8f-492f-9c84-cfe6056f203a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 多租户实体基类（自增主键）
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarMultiTenantEntityWithIdentity<TKey> : SugarEntityWithIdentity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarMultiTenantEntityWithIdentity() : base()
    {
    }

    /// <summary>
    /// 租户ID
    /// </summary>
    [SugarColumn(ColumnDescription = "租户ID", IsNullable = true)]
    public virtual long? TenantId { get; set; }
}
