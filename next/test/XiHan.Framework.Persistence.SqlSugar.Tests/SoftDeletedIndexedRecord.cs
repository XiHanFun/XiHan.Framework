// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 软删除实体的唯一索引。
/// </summary>
/// <remarks>
/// 只声明了编码，删除标记由约定加在末尾：未删除的行之间编码唯一，已删除的行不占编码。
/// </remarks>
[SugarTable("soft_deleted_indexed_record")]
[EntityIndex(nameof(Code), IsUnique = true)]
public sealed class SoftDeletedIndexedRecord : IEntity<Guid>, ISoftDeletable
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
