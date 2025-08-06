#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SugarEntity
// Guid:d9af90d5-fc39-4ef1-bc1c-d6e35acef2a1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2023-11-15 9:05:42
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using SqlSugar;

namespace XiHan.Framework.Data.SqlSugar.Entities;

/// <summary>
/// SqlSugar实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarEntity<TKey> where TKey : struct
{
    /// <summary>
    /// 主键
    /// </summary>
    [SugarColumn(IsPrimaryKey = true)]
    public virtual TKey Id { get; set; }
}

/// <summary>
/// SqlSugar实体基类(自增主键)
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarEntityWithIdentity<TKey> where TKey : struct
{
    /// <summary>
    /// 主键(自增)
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public virtual TKey Id { get; set; }
}

/// <summary>
/// SqlSugar全功能审计实体基类
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarEntityWithAudit<TKey> : SugarEntity<TKey> where TKey : struct
{
    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnDescription = "创建时间")]
    public virtual DateTime CreationTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 创建者ID
    /// </summary>
    [SugarColumn(ColumnDescription = "创建者ID", IsNullable = true)]
    public virtual Guid? CreatorId { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    [SugarColumn(ColumnDescription = "最后修改时间", IsNullable = true)]
    public virtual DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// 最后修改者ID
    /// </summary>
    [SugarColumn(ColumnDescription = "最后修改者ID", IsNullable = true)]
    public virtual Guid? LastModifierId { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    [SugarColumn(ColumnDescription = "软删除标记")]
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    [SugarColumn(ColumnDescription = "删除时间", IsNullable = true)]
    public virtual DateTime? DeletionTime { get; set; }

    /// <summary>
    /// 删除者ID
    /// </summary>
    [SugarColumn(ColumnDescription = "删除者ID", IsNullable = true)]
    public virtual Guid? DeleterId { get; set; }
}

/// <summary>
/// SqlSugar全功能审计实体基类(自增主键)
/// </summary>
/// <typeparam name="TKey">主键类型</typeparam>
public abstract class SugarEntityWithIdentityAndAudit<TKey> : SugarEntityWithIdentity<TKey> where TKey : struct
{
    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(ColumnDescription = "创建时间")]
    public virtual DateTime CreationTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 创建者ID
    /// </summary>
    [SugarColumn(ColumnDescription = "创建者ID", IsNullable = true)]
    public virtual Guid? CreatorId { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    [SugarColumn(ColumnDescription = "最后修改时间", IsNullable = true)]
    public virtual DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// 最后修改者ID
    /// </summary>
    [SugarColumn(ColumnDescription = "最后修改者ID", IsNullable = true)]
    public virtual Guid? LastModifierId { get; set; }

    /// <summary>
    /// 软删除标记
    /// </summary>
    [SugarColumn(ColumnDescription = "软删除标记")]
    public virtual bool IsDeleted { get; set; }

    /// <summary>
    /// 删除时间
    /// </summary>
    [SugarColumn(ColumnDescription = "删除时间", IsNullable = true)]
    public virtual DateTime? DeletionTime { get; set; }

    /// <summary>
    /// 删除者ID
    /// </summary>
    [SugarColumn(ColumnDescription = "删除者ID", IsNullable = true)]
    public virtual Guid? DeleterId { get; set; }
}
