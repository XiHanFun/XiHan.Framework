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
/// 只声明了编码：约定建成只约束未删除行的部分唯一索引，未删除的行之间编码唯一，已删除的行不受约束。
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
