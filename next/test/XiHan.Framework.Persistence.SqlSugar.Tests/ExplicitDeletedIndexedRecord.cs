// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 软删除实体上显式把删除标记写进列的唯一索引。
/// </summary>
/// <remarks>
/// 应用的显式选择：普通唯一索引（编码、删除标记），同一编码最多一行已删除；不需要部分索引，不支持部分索引的数据库上也能用。
/// </remarks>
[SugarTable("explicit_deleted_indexed_record")]
[EntityIndex(nameof(Code), nameof(IsDeleted), IsUnique = true)]
public sealed class ExplicitDeletedIndexedRecord : IEntity<Guid>, ISoftDeletable
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 编码。
    /// </summary>
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }
}
