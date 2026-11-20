// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.ComponentModel.DataAnnotations;
using SqlSugar;
using XiHan.Framework.Domain.Entities;
using XiHan.Framework.Persistence.Abstractions;

namespace XiHan.Framework.Persistence.SqlSugar.Tests;

/// <summary>
/// 显式命名、键含可空列的软删除实体唯一索引
/// </summary>
/// <remarks>
/// 名字原样使用（ADR-0249）；未删除的行之间（编码、区域）唯一，区域为空的行不受约束，与 PostgreSQL 不约束含 NULL 的键一致
/// </remarks>
[SugarTable("named_soft_deleted_record")]
[EntityIndex(nameof(Code), nameof(Region), IsUnique = true, Name = "ux_named_soft_deleted_record_code_region")]
public sealed class NamedSoftDeletedRecord : IEntity<Guid>, ISoftDeletable
{
    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <summary>
    /// 编码
    /// </summary>
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 区域
    /// </summary>
    [StringLength(64)]
    public string? Region { get; set; }

    /// <inheritdoc />
    public bool IsDeleted { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }
}
