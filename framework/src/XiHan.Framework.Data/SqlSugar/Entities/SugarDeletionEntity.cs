#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarDeletionEntity
// Guid:74421415-85d4-4c77-bfa6-38a89cb46830
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/25 05:27:05
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 删除审计实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarDeletionEntity<TKey> : DeletionEntityBase<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 构造函数
    /// </summary>
    protected SugarDeletionEntity() : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    protected SugarDeletionEntity(TKey basicId) : base(basicId)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="basicId">主键</param>
    /// <param name="deletedId">删除者ID</param>
    protected SugarDeletionEntity(TKey basicId, TKey deletedId) : base(basicId, deletedId)
    {
    }

    /// <summary>
    /// 版本控制标识，用于处理并发
    /// </summary>
    [SugarColumn(ColumnName = "Row_Version", ColumnDescription = "版本控制标识，用于处理并发")]
    public override long RowVersion { get; set; }

    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(ColumnName = "Basic_Id", IsPrimaryKey = true, IsIdentity = false, ColumnDescription = "主键")]
    public override TKey BasicId { get; protected set; } = default!;

    /// <summary>
    /// 软删除标记
    /// </summary>
    [SugarColumn(ColumnName = "Is_Deleted", IsNullable = false, ColumnDescription = "软删除标记")]
    public override bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    [SugarColumn(ColumnName = "Deleted_Time", IsNullable = true, ColumnDescription = "删除时间")]
    public override DateTimeOffset? DeletedTime { get; set; }

    /// <summary>
    /// 删除者唯一标识
    /// </summary>
    [SugarColumn(ColumnName = "Deleted_Id", IsNullable = true, ColumnDescription = "删除者唯一标识")]
    public override TKey? DeletedId { get; set; }

    /// <summary>
    /// 删除者
    /// </summary>
    [SugarColumn(ColumnName = "Deleted_By", IsNullable = true, ColumnDescription = "删除者")]
    public override string? DeletedBy { get; set; }
}
