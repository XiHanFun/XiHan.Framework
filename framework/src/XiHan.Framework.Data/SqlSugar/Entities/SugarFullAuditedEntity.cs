#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarFullAuditedEntity
// Guid:9dc2cd8e-23bf-4e06-b68f-d3d160f4ce8e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/25 5:27:38
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;
using System.ComponentModel.DataAnnotations;
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
    /// <param name="basicId">主键</param>
    protected SugarFullAuditedEntity(TKey basicId) : base(basicId)
    {
        RowVersion = [];
        BasicId = basicId;
        CreationTime = DateTimeOffset.UtcNow;
        IsDeleted = false;
    }

    /// <summary>
    /// 版本控制标识，用于处理并发
    /// </summary>
    [ConcurrencyCheck]
    [Timestamp]
    [SugarColumn(IsOnlyIgnoreUpdate = true)]
    public override byte[] RowVersion { get; set; }

    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(IsPrimaryKey = true)]
    public override TKey BasicId { get; protected set; } = default!;

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnDescription = "创建时间")]
    public override DateTimeOffset CreationTime { get; set; }

    /// <summary>
    /// 创建者 Id
    /// </summary>
    [SugarColumn(ColumnDescription = "创建者Id", IsNullable = true)]
    public override TKey? CreatorId { get; set; }

    /// <summary>
    /// 创建人
    /// </summary>
    [SugarColumn(ColumnDescription = "创建人", IsNullable = true)]
    public override string? Creator { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(ColumnDescription = "修改时间", IsNullable = true)]
    public override DateTimeOffset? ModificationTime { get; set; }

    /// <summary>
    /// 修改者 Id
    /// </summary>
    [SugarColumn(ColumnDescription = "修改者Id", IsNullable = true)]
    public override TKey? ModifierId { get; set; }

    /// <summary>
    /// 修改人
    /// </summary>
    [SugarColumn(ColumnDescription = "修改人", IsNullable = true)]
    public override string? Modifier { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    [SugarColumn(ColumnDescription = "软删除标记")]
    public override bool IsDeleted { get; set; } = false;

    /// <summary>
    /// 删除时间
    /// </summary>
    [SugarColumn(ColumnDescription = "删除时间", IsNullable = true)]
    public override DateTimeOffset? DeletionTime { get; set; }

    /// <summary>
    /// 删除者 Id
    /// </summary>
    [SugarColumn(ColumnDescription = "删除者Id", IsNullable = true)]
    public override TKey? DeleterId { get; set; }

    /// <summary>
    /// 删除者
    /// </summary>
    [SugarColumn(ColumnDescription = "删除者", IsNullable = true)]
    public override string? Deleter { get; set; }
}
