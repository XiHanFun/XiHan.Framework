#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarMultiTenantModificationEntity
// Guid:698b9a4d-ab6e-4693-8412-15432f517e76
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/02/12 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 多租户修改审计实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarMultiTenantModificationEntity<TKey> : SugarModificationEntity<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarMultiTenantModificationEntity() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected SugarMultiTenantModificationEntity(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="modifiedId">修改者ID</param>
    protected SugarMultiTenantModificationEntity(TKey basicId, TKey modifiedId) : base(basicId, modifiedId)
    {
    }

    /// <summary>
    /// 租户ID
    /// </summary>
    [SugarColumn(ColumnDescription = "租户ID", IsOnlyIgnoreUpdate = true, IsNullable = true)]
    public virtual long? TenantId { get; set; }
}
