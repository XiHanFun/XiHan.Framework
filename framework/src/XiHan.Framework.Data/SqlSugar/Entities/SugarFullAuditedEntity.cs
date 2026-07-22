// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using SqlSugar;
using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar 完整审计实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
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
    [SugarColumn(ColumnName = "Row_Version", ColumnDescription = "版本控制标识，用于处理并发", IsEnableUpdateVersionValidation = true)]
    public override long RowVersion { get; set; }

    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(ColumnName = "Basic_Id", IsPrimaryKey = true, IsIdentity = false, ColumnDescription = "主键")]
    public override TKey BasicId { get; protected set; } = default!;

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnName = "Created_Time", IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    public override DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 创建者唯一标识
    /// </summary>
    [SugarColumn(ColumnName = "Created_Id", IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建者唯一标识")]
    public override TKey? CreatedId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    [SugarColumn(ColumnName = "Created_By", IsNullable = true, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建人")]
    public override string? CreatedBy { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(ColumnName = "Modified_Time", IsNullable = true, ColumnDescription = "修改时间")]
    public override DateTimeOffset? ModifiedTime { get; set; }

    /// <summary>
    /// 修改者唯一标识
    /// </summary>
    [SugarColumn(ColumnName = "Modified_Id", IsNullable = true, ColumnDescription = "修改者唯一标识")]
    public override TKey? ModifiedId { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    [SugarColumn(ColumnName = "Modified_By", IsNullable = true, ColumnDescription = "修改人")]
    public override string? ModifiedBy { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    [SugarColumn(ColumnName = "Is_Deleted", IsNullable = false, ColumnDescription = "软删除标记")]
    public override bool IsDeleted { get; set; } = false;

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
