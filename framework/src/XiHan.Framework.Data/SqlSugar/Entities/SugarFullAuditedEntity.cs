#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarFullAuditedEntity
// Guid:9dc2cd8e-23bf-4e06-b68f-d3d160f4ce8e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/25 05:27:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 完整审计实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
[SugarIndex("IX_CreatedTime", nameof(CreatedTime), OrderByType.Asc)]
[SugarIndex("IX_IsDeleted", nameof(IsDeleted), OrderByType.Asc)]
public abstract class SugarFullAuditedEntity<TKey> : FullAuditedEntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarFullAuditedEntity() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected SugarFullAuditedEntity(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="createdId">创建者ID</param>
    protected SugarFullAuditedEntity(TKey basicId, TKey createdId) : base(basicId, createdId)
    {
    }

    /// <summary>
    /// 版本控制标识，用于处理并发
    /// </summary>
    [SugarColumn(ColumnDescription = "版本控制标识，用于处理并发")]
    public override long RowVersion { get; set; }

    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = false, ColumnDescription = "主键")]
    public override TKey BasicId { get; protected set; } = default!;

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    public override DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 创建者唯一标识
    /// </summary>
    [SugarColumn(IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建者唯一标识")]
    public override TKey? CreatedId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    [SugarColumn(IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建人")]
    public override string? CreatedBy { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    public override DateTimeOffset? ModifiedTime { get; set; }

    /// <summary>
    /// 修改者唯一标识
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改者唯一标识")]
    public override TKey? ModifiedId { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改人")]
    public override string? ModifiedBy { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "软删除标记")]
    public override bool IsDeleted { get; set; } = false;

    /// <summary>
    /// 删除时间
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "删除时间")]
    public override DateTimeOffset? DeletedTime { get; set; }

    /// <summary>
    /// 删除者唯一标识
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "删除者唯一标识")]
    public override TKey? DeletedId { get; set; }

    /// <summary>
    /// 删除者
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "删除者")]
    public override string? DeletedBy { get; set; }
}
